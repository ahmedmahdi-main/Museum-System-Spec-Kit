using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Photography.Contracts;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class AppendImagesToPhotographySetUseCase(
    PhotographyUploadPersistenceService persistence,
    IArtifactImageProcessor imageProcessor,
    PhotographyUploadFingerprintService fingerprintService,
    PhotographyUploadConsistencyService uploadConsistencyService,
    PhotographyResponseMapper responseMapper,
    IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<PhotographyUploadOperationResultDto>> AppendImagesToPhotographySet(
        AppendImagesToPhotographySetCommand command,
        CancellationToken cancellationToken = default)
    {
        var requestValidation = PhotographyUploadUseCaseSupport.ValidateAppendCommand(command);
        if (requestValidation is not null)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(requestValidation);
        }

        var trustedActor = PhotographyUploadUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (trustedActor.Failure is not null)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(trustedActor.Failure);
        }

        var set = await persistence.FindPhotographySetAsync(command.PhotographySetId, cancellationToken);
        if (set is null)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(new ValidationIssue("PhotographySet.NotFound", "Photography set was not found.", nameof(command.PhotographySetId)));
        }

        if (command.ArtifactIdConfirmation.HasValue && command.ArtifactIdConfirmation.Value != set.ArtifactId)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(new ValidationIssue("PhotographySet.ArtifactConflict", "Artifact confirmation does not match the Photography Set.", nameof(command.ArtifactIdConfirmation)));
        }

        if (command.PurposeConfirmation.HasValue && command.PurposeConfirmation.Value != set.Purpose)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(new ValidationIssue("PhotographySet.PurposeConflict", "Purpose confirmation does not match the Photography Set.", nameof(command.PurposeConfirmation)));
        }

        await using var preparedFiles = await PhotographyUploadUseCaseSupport.PrepareFilesAsync(
            command.Files,
            imageProcessor,
            fingerprintService,
            cancellationToken);

        var requestFingerprint = fingerprintService.ComputeRequestFingerprint(new PhotographyUploadFingerprintInput(
            set.ArtifactId,
            PhotographyUploadOperationKind.AppendToSetUpload,
            set.PhotographySetId,
            set.Purpose,
            set.PhotographyDate,
            set.PhotographerUserId,
            preparedFiles.Files.Select(file => file.FingerprintInput).ToArray()));

        var operationResult = await PhotographyUploadUseCaseSupport.GetOrStartOperationAsync(
            persistence,
            responseMapper,
            trustedActor.UserId!,
            PhotographyUploadOperationKind.AppendToSetUpload,
            command.IdempotencyKey,
            requestFingerprint,
            set.ArtifactId,
            set.PhotographySetId,
            cancellationToken);

        if (operationResult.ReplayResult is not null)
        {
            return operationResult.ReplayResult;
        }

        var operation = operationResult.Operation!;
        foreach (var file in preparedFiles.Files)
        {
            if (operation.FileOutcomes.Any(outcome => outcome.ClientFileOrdinal == file.Input.ClientFileOrdinal))
            {
                continue;
            }

            await uploadConsistencyService.ProcessFileAsync(
                operation,
                file,
                set.ArtifactId,
                set.Purpose,
                set.PhotographyDate,
                set.PhotographerUserId,
                trustedActor.UserId!,
                existingSet: set,
                createSetWhenSuccessful: false,
                cancellationToken);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
        }

        await persistence.FinalizeOperationAsync(operation.PhotographyUploadOperationId, preparedFiles.Files.Count, cancellationToken);
        var snapshot = await persistence.LoadAuthoritativeSnapshotAsync(operation.PhotographyUploadOperationId, cancellationToken);
        return UseCaseResult<PhotographyUploadOperationResultDto>.Success(responseMapper.ToUploadOperationResult(snapshot));
    }
}

public sealed record AppendImagesToPhotographySetCommand(
    Guid PhotographySetId,
    string IdempotencyKey,
    IReadOnlyList<PhotographyUploadFileInput> Files,
    Guid? ArtifactIdConfirmation = null,
    PhotographyPurpose? PurposeConfirmation = null);

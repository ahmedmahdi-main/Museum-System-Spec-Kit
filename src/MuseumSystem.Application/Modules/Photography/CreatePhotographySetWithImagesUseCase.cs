using System.Security.Cryptography;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Photography.Contracts;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class CreatePhotographySetWithImagesUseCase(
    PhotographyUploadPersistenceService persistence,
    IArtifactImageProcessor imageProcessor,
    PhotographyUploadFingerprintService fingerprintService,
    PhotographyUploadConsistencyService uploadConsistencyService,
    PhotographyResponseMapper responseMapper,
    IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<PhotographyUploadOperationResultDto>> CreatePhotographySetWithImages(
        CreatePhotographySetWithImagesCommand command,
        CancellationToken cancellationToken = default)
    {
        var requestValidation = PhotographyUploadUseCaseSupport.ValidateCreateCommand(command);
        if (requestValidation is not null)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(requestValidation);
        }

        var trustedActor = PhotographyUploadUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (trustedActor.Failure is not null)
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(trustedActor.Failure);
        }

        if (!await persistence.ArtifactExistsAsync(command.ArtifactId, cancellationToken))
        {
            return UseCaseResult<PhotographyUploadOperationResultDto>.Failure(new ValidationIssue("Artifact.NotFound", "Artifact was not found.", nameof(command.ArtifactId)));
        }

        await using var preparedFiles = await PhotographyUploadUseCaseSupport.PrepareFilesAsync(
            command.Files,
            imageProcessor,
            fingerprintService,
            cancellationToken);

        var requestFingerprint = fingerprintService.ComputeRequestFingerprint(new PhotographyUploadFingerprintInput(
            command.ArtifactId,
            PhotographyUploadOperationKind.CreateSetUpload,
            null,
            command.Purpose,
            command.PhotographyDate,
            command.PhotographerUserId,
            preparedFiles.Files.Select(file => file.FingerprintInput).ToArray()));

        var operationResult = await PhotographyUploadUseCaseSupport.GetOrStartOperationAsync(
            persistence,
            responseMapper,
            trustedActor.UserId!,
            PhotographyUploadOperationKind.CreateSetUpload,
            command.IdempotencyKey,
            requestFingerprint,
            command.ArtifactId,
            null,
            cancellationToken);

        if (operationResult.ReplayResult is not null)
        {
            return operationResult.ReplayResult;
        }

        var operation = operationResult.Operation!;
        PhotographySet? establishedSet = operation.PhotographySetId.HasValue
            ? await persistence.FindPhotographySetAsync(operation.PhotographySetId.Value, cancellationToken)
            : null;
        foreach (var file in preparedFiles.Files)
        {
            if (operation.FileOutcomes.Any(outcome => outcome.ClientFileOrdinal == file.Input.ClientFileOrdinal))
            {
                continue;
            }

            establishedSet = await uploadConsistencyService.ProcessFileAsync(
                operation,
                file,
                command.ArtifactId,
                command.Purpose,
                command.PhotographyDate,
                command.PhotographerUserId.Trim(),
                trustedActor.UserId!,
                existingSet: establishedSet,
                createSetWhenSuccessful: establishedSet is null,
                cancellationToken);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
            establishedSet = operation.PhotographySetId.HasValue
                ? await persistence.FindPhotographySetAsync(operation.PhotographySetId.Value, cancellationToken)
                : establishedSet;
        }

        await persistence.FinalizeOperationAsync(operation.PhotographyUploadOperationId, preparedFiles.Files.Count, cancellationToken);
        var snapshot = await persistence.LoadAuthoritativeSnapshotAsync(operation.PhotographyUploadOperationId, cancellationToken);
        return UseCaseResult<PhotographyUploadOperationResultDto>.Success(responseMapper.ToUploadOperationResult(snapshot));
    }
}

public sealed record CreatePhotographySetWithImagesCommand(
    Guid ArtifactId,
    PhotographyPurpose Purpose,
    DateOnly PhotographyDate,
    string PhotographerUserId,
    string IdempotencyKey,
    IReadOnlyList<PhotographyUploadFileInput> Files,
    Guid? PhotographyRequestId = null);


public sealed record PhotographyUploadFileInput(
    int ClientFileOrdinal,
    string OriginalFilename,
    Stream Content,
    long LengthBytes);

internal static class PhotographyUploadUseCaseSupport
{
    public static ValidationIssue? ValidateCreateCommand(CreatePhotographySetWithImagesCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var common = ValidateCommon(command.IdempotencyKey, command.Files);
        if (common is not null)
        {
            return common;
        }

        if (command.ArtifactId == Guid.Empty)
        {
            return new ValidationIssue("Artifact.Required", "Artifact is required.", nameof(command.ArtifactId));
        }

        if (command.Purpose is not (PhotographyPurpose.GeneralDocumentation or PhotographyPurpose.PreMaintenance or PhotographyPurpose.DuringMaintenance or PhotographyPurpose.PostMaintenance))
        {
            return new ValidationIssue("Photography.PurposeInvalid", "Photography purpose is not supported.", nameof(command.Purpose));
        }

        if (command.PhotographyDate == default)
        {
            return new ValidationIssue("Photography.DateRequired", "Photography date is required.", nameof(command.PhotographyDate));
        }

        if (string.IsNullOrWhiteSpace(command.PhotographerUserId))
        {
            return new ValidationIssue("Photography.PhotographerRequired", "Photographer is required.", nameof(command.PhotographerUserId));
        }

        return null;
    }

    public static ValidationIssue? ValidateAppendCommand(AppendImagesToPhotographySetCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var common = ValidateCommon(command.IdempotencyKey, command.Files);
        if (common is not null)
        {
            return common;
        }

        if (command.PhotographySetId == Guid.Empty)
        {
            return new ValidationIssue("PhotographySet.Required", "Photography set is required.", nameof(command.PhotographySetId));
        }

        if (command.ArtifactIdConfirmation == Guid.Empty)
        {
            return new ValidationIssue("Artifact.ConfirmationInvalid", "Artifact confirmation is invalid.", nameof(command.ArtifactIdConfirmation));
        }

        if (command.PurposeConfirmation.HasValue
            && command.PurposeConfirmation.Value is not (PhotographyPurpose.GeneralDocumentation or PhotographyPurpose.PreMaintenance or PhotographyPurpose.DuringMaintenance or PhotographyPurpose.PostMaintenance))
        {
            return new ValidationIssue("Photography.PurposeInvalid", "Photography purpose confirmation is not supported.", nameof(command.PurposeConfirmation));
        }

        return null;
    }

    public static async Task<PreparedPhotographyUploadFiles> PrepareFilesAsync(
        IReadOnlyList<PhotographyUploadFileInput> files,
        IArtifactImageProcessor imageProcessor,
        PhotographyUploadFingerprintService fingerprintService,
        CancellationToken cancellationToken)
    {
        var preparedFiles = new List<PreparedPhotographyUploadFile>(files.Count);
        try
        {
            foreach (var file in files.OrderBy(file => file.ClientFileOrdinal))
            {
                var prepared = await PreparedPhotographyUploadFile.CreateAsync(file, imageProcessor, fingerprintService, cancellationToken);
                preparedFiles.Add(prepared);
            }

            return new PreparedPhotographyUploadFiles(preparedFiles);
        }
        catch
        {
            foreach (var prepared in preparedFiles)
            {
                await prepared.DisposeAsync();
            }

            throw;
        }
    }

    public static async Task<UploadOperationResolution> GetOrStartOperationAsync(
        PhotographyUploadPersistenceService persistence,
        PhotographyResponseMapper responseMapper,
        string actorUserId,
        PhotographyUploadOperationKind operationKind,
        string idempotencyKey,
        string requestFingerprint,
        Guid artifactId,
        Guid? photographySetId,
        CancellationToken cancellationToken)
    {
        var operation = await persistence.GetOrStartUploadOperationAsync(
            actorUserId.Trim(),
            operationKind,
            idempotencyKey.Trim(),
            requestFingerprint,
            artifactId,
            photographySetId,
            cancellationToken);

        if (!operation.MatchesFingerprint(requestFingerprint))
        {
            return new UploadOperationResolution(null, UseCaseResult<PhotographyUploadOperationResultDto>.Conflict("The idempotency key was already used for a different upload request."));
        }

        operation = await persistence.MarkOperationSeenAndReloadAsync(operation.PhotographyUploadOperationId, cancellationToken);
        if (operation.Status != PhotographyUploadOperationStatus.InProgress)
        {
            var existingSnapshot = await persistence.LoadAuthoritativeSnapshotAsync(operation.PhotographyUploadOperationId, cancellationToken);
            return new UploadOperationResolution(null, UseCaseResult<PhotographyUploadOperationResultDto>.Success(responseMapper.ToUploadOperationResult(existingSnapshot)));
        }

        return new UploadOperationResolution(operation, null);
    }

    public static TrustedUploadActorResolution GetTrustedActorUserId(IAuditActorContext actorContext)
    {
        ArgumentNullException.ThrowIfNull(actorContext);
        var actor = actorContext.CurrentActor;
        return actor.IsAuthenticated && !string.IsNullOrWhiteSpace(actor.UserId)
            ? new TrustedUploadActorResolution(actor.UserId.Trim(), null)
            : new TrustedUploadActorResolution(null, new ValidationIssue("Photography.ActorRequired", "Authenticated actor is required."));
    }
    private static ValidationIssue? ValidateCommon(string idempotencyKey, IReadOnlyList<PhotographyUploadFileInput> files)
    {

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new ValidationIssue("Photography.IdempotencyKeyRequired", "Idempotency key is required.", nameof(idempotencyKey));
        }

        if (files is null || files.Count == 0)
        {
            return new ValidationIssue("Photography.FilesRequired", "At least one file is required.", nameof(files));
        }

        var ordinals = new HashSet<int>();
        foreach (var file in files)
        {
            if (file.ClientFileOrdinal < 0)
            {
                return new ValidationIssue("Photography.FileOrdinalInvalid", "File ordinal cannot be negative.", nameof(file.ClientFileOrdinal));
            }

            if (!ordinals.Add(file.ClientFileOrdinal))
            {
                return new ValidationIssue("Photography.FileOrdinalDuplicate", "File ordinals must be unique.", nameof(file.ClientFileOrdinal));
            }

            if (string.IsNullOrWhiteSpace(file.OriginalFilename))
            {
                return new ValidationIssue("Photography.FileNameRequired", "Original filename is required.", nameof(file.OriginalFilename));
            }

            if (file.Content is null)
            {
                return new ValidationIssue("Photography.FileContentRequired", "File content is required.", nameof(file.Content));
            }

            if (file.LengthBytes <= 0)
            {
                return new ValidationIssue("Photography.FileLengthInvalid", "File length must be greater than zero.", nameof(file.LengthBytes));
            }
        }

        return null;
    }

}


public sealed record TrustedUploadActorResolution(string? UserId, ValidationIssue? Failure);

internal sealed record UploadOperationResolution(
    PhotographyUploadOperation? Operation,
    UseCaseResult<PhotographyUploadOperationResultDto>? ReplayResult);

internal sealed class PreparedPhotographyUploadFiles(IReadOnlyList<PreparedPhotographyUploadFile> files) : IAsyncDisposable
{
    public IReadOnlyList<PreparedPhotographyUploadFile> Files { get; } = files;

    public async ValueTask DisposeAsync()
    {
        foreach (var file in Files)
        {
            await file.DisposeAsync();
        }
    }
}

internal sealed class PreparedPhotographyUploadFile : IAsyncDisposable
{
    private readonly Stream? ownedContent;
    private readonly long resetPosition;

    private PreparedPhotographyUploadFile(
        PhotographyUploadFileInput input,
        Stream content,
        Stream? ownedContent,
        long resetPosition,
        string contentHash,
        ArtifactImageValidationResult validation,
        PhotographyUploadFingerprintFileInput fingerprintInput,
        string fileFingerprint,
        IArtifactImageProcessor processor)
    {
        Input = input;
        Content = content;
        this.ownedContent = ownedContent;
        this.resetPosition = resetPosition;
        ContentHash = contentHash;
        Validation = validation;
        FingerprintInput = fingerprintInput;
        FileFingerprint = fileFingerprint;
        Processor = processor;
    }

    public PhotographyUploadFileInput Input { get; }
    public Stream Content { get; }
    public string ContentHash { get; }
    public ArtifactImageValidationResult Validation { get; }
    public PhotographyUploadFingerprintFileInput FingerprintInput { get; }
    public string FileFingerprint { get; }
    public IArtifactImageProcessor Processor { get; }

    public static async Task<PreparedPhotographyUploadFile> CreateAsync(
        PhotographyUploadFileInput input,
        IArtifactImageProcessor processor,
        PhotographyUploadFingerprintService fingerprintService,
        CancellationToken cancellationToken)
    {
        var content = input.Content;
        Stream? owned = null;
        long resetPosition;
        if (content.CanSeek)
        {
            resetPosition = content.Position;
        }
        else
        {
            owned = new MemoryStream();
            await content.CopyToAsync(owned, cancellationToken);
            owned.Position = 0;
            content = owned;
            resetPosition = 0;
        }

        var contentHash = await ComputeSha256Async(content, resetPosition, cancellationToken);
        var validation = await ValidateAsync(content, resetPosition, input, processor, cancellationToken);
        var fingerprintInput = ToFingerprintInput(input, contentHash);
        var fileFingerprint = fingerprintService.ComputeFileFingerprint(fingerprintInput);
        return new PreparedPhotographyUploadFile(input, content, owned, resetPosition, contentHash, validation, fingerprintInput, fileFingerprint, processor);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Content.Position = resetPosition;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        ownedContent?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task<string> ComputeSha256Async(Stream content, long resetPosition, CancellationToken cancellationToken)
    {
        content.Position = resetPosition;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = resetPosition;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<ArtifactImageValidationResult> ValidateAsync(
        Stream content,
        long resetPosition,
        PhotographyUploadFileInput input,
        IArtifactImageProcessor processor,
        CancellationToken cancellationToken)
    {
        content.Position = resetPosition;
        var result = await processor.ValidateAsync(content, input.OriginalFilename.Trim(), input.LengthBytes, cancellationToken);
        content.Position = resetPosition;
        return result;
    }

    private static PhotographyUploadFingerprintFileInput ToFingerprintInput(PhotographyUploadFileInput input, string contentHash) =>
        new(
            input.ClientFileOrdinal,
            input.LengthBytes,
            contentHash,
            "application/octet-stream",
            1,
            1,
            "raw-upload",
            ".upload",
            input.OriginalFilename);
}

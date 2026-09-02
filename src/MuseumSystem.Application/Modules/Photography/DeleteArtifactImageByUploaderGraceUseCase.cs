using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class DeleteArtifactImageByUploaderGraceUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    TimeProvider clock,
    ArtifactImageDeletionService deletionService)
{
    public async Task<UseCaseResult<ArtifactImageDeletionDto>> DeleteArtifactImageByUploaderGrace(
        DeleteArtifactImageByUploaderGraceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(actor.Failure);
        }

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyUpload))
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyUpload));
        }

        if (command.ArtifactImageId == Guid.Empty)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue("ArtifactImage.Required", "Artifact image is required.", nameof(command.ArtifactImageId)));
        }

        if (command.ExpectedConcurrencyToken < 0)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue("ArtifactImage.ConcurrencyTokenInvalid", "Expected concurrency token cannot be negative.", nameof(command.ExpectedConcurrencyToken)));
        }

        var image = await dbContext.ArtifactImages
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == command.ArtifactImageId, cancellationToken);
        if (image is null)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue("ArtifactImage.NotFound", "Artifact image was not found.", nameof(command.ArtifactImageId)));
        }

        if (image.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return ArtifactImageDeletionUseCaseSupport.StaleImage();
        }

        if (image.Status != ArtifactImageStatus.Available)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue("ArtifactImage.DeleteInvalidState", "Only available images can be deleted.", nameof(command.ArtifactImageId)));
        }

        var serverNow = clock.GetUtcNow();
        var isOriginalUploader = string.Equals(image.UploadedByUserId.Trim(), actor.UserId!.Trim(), StringComparison.Ordinal);
        if (!isOriginalUploader)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                "ArtifactImage.UploaderMismatch",
                "Only the original uploader may use grace-period deletion.",
                nameof(command.ArtifactImageId)));
        }

        if (!PhotographyRules.CanUseUploaderGraceDeletion(
            image.UploadedByUserId,
            actor.UserId!,
            image.UploadedAt,
            serverNow,
            currentUserHasUploadPermission: true))
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                "ArtifactImage.GracePeriodExpired",
                "The uploader grace period for this image has expired.",
                nameof(command.ArtifactImageId)));
        }

        var deletionResult = await deletionService.DeleteAsync(
            new AuthorizedArtifactImageDeletion(
                image.ArtifactImageId,
                command.ExpectedConcurrencyToken,
                ArtifactImageDeletionMode.UploaderGracePeriod,
                null,
                actor.UserId!,
                serverNow),
            cancellationToken);

        return await ArtifactImageDeletionUseCaseSupport.MapDeletionResultAsync(dbContext, deletionResult, cancellationToken);
    }
}

public sealed record DeleteArtifactImageByUploaderGraceCommand(
    Guid ArtifactImageId,
    int ExpectedConcurrencyToken);

internal static class ArtifactImageDeletionUseCaseSupport
{
    public static UseCaseResult<ArtifactImageDeletionDto> StaleImage() =>
        UseCaseResult<ArtifactImageDeletionDto>.Conflict("ArtifactImage.ConcurrencyConflict: Artifact image changed. Reload and review the latest image before deleting.");

    public static async Task<UseCaseResult<ArtifactImageDeletionDto>> MapDeletionResultAsync(
        IMuseumDbContext dbContext,
        ArtifactImageDeletionResult result,
        CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case ArtifactImageDeletionOutcome.Completed:
                var image = await dbContext.ArtifactImages
                    .AsNoTracking()
                    .FirstAsync(candidate => candidate.ArtifactImageId == result.ArtifactImageId, cancellationToken);
                return UseCaseResult<ArtifactImageDeletionDto>.Success(
                    ToDto(image),
                    "Artifact image permanently deleted.",
                    result.AuditReference);

            case ArtifactImageDeletionOutcome.Conflict:
                return StaleImage();

            case ArtifactImageDeletionOutcome.RecoveryRequired:
                return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                    "ArtifactImage.DeletionRecoveryRequired",
                    "Image deletion could not be completed. The operation was saved for internal processing and was not recorded as complete."));

            case ArtifactImageDeletionOutcome.FinalizationPending:
                return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                    "ArtifactImage.DeletionFinalizationPending",
                    "Image files were processed, but the final deletion record has not completed yet. The operation was saved for internal processing."));

            default:
                return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                    "ArtifactImage.DeleteInvalidState",
                    "Artifact image could not be deleted in its current state.",
                    "ArtifactImageId"));
        }
    }

    private static ArtifactImageDeletionDto ToDto(ArtifactImage image) =>
        new(
            image.ArtifactImageId,
            image.ArtifactId,
            image.Status,
            image.DeletionMode!.Value,
            image.DeletedAt,
            image.ConcurrencyToken);
}

public sealed record ArtifactImageDeletionDto(
    Guid ArtifactImageId,
    Guid ArtifactId,
    ArtifactImageStatus Status,
    ArtifactImageDeletionMode DeletionMode,
    DateTimeOffset? DeletedAt,
    int ConcurrencyToken);

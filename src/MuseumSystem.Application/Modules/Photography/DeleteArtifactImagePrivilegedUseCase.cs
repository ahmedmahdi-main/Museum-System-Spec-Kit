using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class DeleteArtifactImagePrivilegedUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    TimeProvider clock,
    ArtifactImageDeletionService deletionService)
{
    private const int DeletionReasonMaxLength = 1000;

    public async Task<UseCaseResult<ArtifactImageDeletionDto>> DeleteArtifactImagePrivileged(
        DeleteArtifactImagePrivilegedCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(actor.Failure);
        }

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyDelete))
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyDelete));
        }

        if (command.ArtifactImageId == Guid.Empty)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue("ArtifactImage.Required", "Artifact image is required.", nameof(command.ArtifactImageId)));
        }

        if (command.ExpectedConcurrencyToken < 0)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue("ArtifactImage.ConcurrencyTokenInvalid", "Expected concurrency token cannot be negative.", nameof(command.ExpectedConcurrencyToken)));
        }

        var normalizedReason = NormalizeOptional(command.DeletionReason);
        if (!PhotographyRules.HasRequiredDeletionReason(ArtifactImageDeletionMode.Privileged, normalizedReason))
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                "ArtifactImage.DeletionReasonRequired",
                "A deletion reason is required for privileged deletion.",
                nameof(command.DeletionReason)));
        }

        if (normalizedReason!.Length > DeletionReasonMaxLength)
        {
            return UseCaseResult<ArtifactImageDeletionDto>.Failure(new ValidationIssue(
                "ArtifactImage.DeletionReasonTooLong",
                "Deletion reason cannot exceed 1000 characters.",
                nameof(command.DeletionReason)));
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
        var deletionResult = await deletionService.DeleteAsync(
            new AuthorizedArtifactImageDeletion(
                image.ArtifactImageId,
                command.ExpectedConcurrencyToken,
                ArtifactImageDeletionMode.Privileged,
                normalizedReason,
                actor.UserId!,
                serverNow),
            cancellationToken);

        return await ArtifactImageDeletionUseCaseSupport.MapDeletionResultAsync(dbContext, deletionResult, cancellationToken);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record DeleteArtifactImagePrivilegedCommand(
    Guid ArtifactImageId,
    string? DeletionReason,
    int ExpectedConcurrencyToken);

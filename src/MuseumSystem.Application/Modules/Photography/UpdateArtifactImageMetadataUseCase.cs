using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class UpdateArtifactImageMetadataUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    IAuditWriter auditWriter,
    TimeProvider clock)
{
    private const int CaptionMaxLength = 1000;

    public async Task<UseCaseResult<ArtifactImageMetadataManagementDto>> UpdateArtifactImageMetadata(
        UpdateArtifactImageMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(actor.Failure);
        }

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyManage))
        {
            return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyManage));
        }

        if (command.ArtifactImageId == Guid.Empty)
        {
            return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(new ValidationIssue("ArtifactImage.Required", "Artifact image is required.", nameof(command.ArtifactImageId)));
        }

        if (command.ExpectedConcurrencyToken < 0)
        {
            return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(new ValidationIssue("ArtifactImage.ConcurrencyTokenInvalid", "Expected concurrency token cannot be negative.", nameof(command.ExpectedConcurrencyToken)));
        }

        var normalizedCaption = NormalizeOptional(command.Caption);
        if (normalizedCaption?.Length > CaptionMaxLength)
        {
            return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(new ValidationIssue("ArtifactImage.CaptionTooLong", "Caption cannot exceed 1000 characters.", nameof(command.Caption)));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var image = await dbContext.ArtifactImages
                .Include(candidate => candidate.Derivatives)
                .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == command.ArtifactImageId, cancellationToken);
            if (image is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(new ValidationIssue("ArtifactImage.NotFound", "Artifact image was not found.", nameof(command.ArtifactImageId)));
            }

            if (image.ConcurrencyToken != command.ExpectedConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ClearTrackedChanges();
                return MetadataConflict();
            }

            if (image.Status != ArtifactImageStatus.Available)
            {
                await transaction.RollbackAsync(cancellationToken);
                return UseCaseResult<ArtifactImageMetadataManagementDto>.Failure(new ValidationIssue("ArtifactImage.NotEditable", "Only available images can be edited.", nameof(command.ArtifactImageId)));
            }

            var previousCaption = image.Caption;
            if (string.Equals(previousCaption, normalizedCaption, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return UseCaseResult<ArtifactImageMetadataManagementDto>.Success(ToManagementDto(image), "Image metadata is already current.");
            }

            image.UpdateCaption(command.Caption);
            await dbContext.SaveChangesAsync(cancellationToken);

            var changedAt = clock.GetUtcNow();
            var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
                PhotographyAuditActions.ImageMetadataChange,
                "Photography",
                nameof(ArtifactImage),
                image.ArtifactImageId.ToString(),
                "Updated artifact image metadata.",
                $"ArtifactId={image.ArtifactId}; ArtifactImageId={image.ArtifactImageId}; PreviousCaption={FormatAuditValue(previousCaption)}; NewCaption={FormatAuditValue(image.Caption)}; ActingUserId={actor.UserId}; ChangedAtUtc={changedAt:O}"),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return UseCaseResult<ArtifactImageMetadataManagementDto>.Success(
                ToManagementDto(image),
                "Artifact image metadata updated.",
                auditReference);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return MetadataConflict();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }

    private static UseCaseResult<ArtifactImageMetadataManagementDto> MetadataConflict() =>
        UseCaseResult<ArtifactImageMetadataManagementDto>.Conflict("ArtifactImage.ConcurrencyConflict: Artifact image metadata changed. Reload and review the latest image before editing.");

    private static ArtifactImageMetadataManagementDto ToManagementDto(ArtifactImage image) =>
        new(
            image.ArtifactImageId,
            image.ArtifactId,
            image.PhotographySetId,
            image.Caption,
            image.Status,
            image.ConcurrencyToken);

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatAuditValue(string? value) => value is null ? "<null>" : value;
}

public sealed record UpdateArtifactImageMetadataCommand(
    Guid ArtifactImageId,
    string? Caption,
    int ExpectedConcurrencyToken);

public sealed record ArtifactImageMetadataManagementDto(
    Guid ArtifactImageId,
    Guid ArtifactId,
    Guid PhotographySetId,
    string? Caption,
    ArtifactImageStatus Status,
    int ConcurrencyToken);

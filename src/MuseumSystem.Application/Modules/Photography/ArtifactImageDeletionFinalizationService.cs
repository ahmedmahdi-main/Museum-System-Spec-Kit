using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class ArtifactImageDeletionFinalizationService(
    IMuseumDbContext dbContext,
    IAuditWriter auditWriter)
{
    public async Task<ArtifactImageDeletionFinalizationResult> FinalizeAsync(
        ArtifactImageDeletionFinalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArtifactImageId == Guid.Empty)
        {
            throw new ArgumentException("An artifact image is required.", nameof(request));
        }


        var image = await dbContext.ArtifactImages
            .Include(candidate => candidate.Derivatives)
            .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == request.ArtifactImageId, cancellationToken);

        if (image is null)
        {
            return ArtifactImageDeletionFinalizationResult.InvalidState(request.ArtifactImageId);
        }

        if (image.Status == ArtifactImageStatus.Deleted)
        {
            return ArtifactImageDeletionFinalizationResult.AlreadyFinalized(image.ArtifactImageId, image.ConcurrencyToken);
        }

        if (image.Status != ArtifactImageStatus.DeletePending)
        {
            return ArtifactImageDeletionFinalizationResult.InvalidState(image.ArtifactImageId);
        }

        if (image.DeletionMode != request.DeletionMode
            || string.IsNullOrWhiteSpace(image.DeletionRequestedByUserId)
            || image.DeletionRequestedAt is null)
        {
            return ArtifactImageDeletionFinalizationResult.InvalidState(image.ArtifactImageId);
        }

        if (request.ExpectedConcurrencyToken.HasValue && image.ConcurrencyToken != request.ExpectedConcurrencyToken.Value)
        {
            return ArtifactImageDeletionFinalizationResult.Conflict(image.ArtifactImageId);
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            image.MarkDeleted(request.DeletionMode);
            await dbContext.SaveChangesAsync(cancellationToken);

            var auditReference = await auditWriter.WriteAsync(BuildAuditRequest(image, request), cancellationToken);
            await ResolveDeleteCleanupRecoveriesAsync(image.ArtifactImageId, image.DeletedAt!.Value, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return ArtifactImageDeletionFinalizationResult.Completed(image.ArtifactImageId, image.ConcurrencyToken, auditReference);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            await TryRecordFinalizationRecoveryAsync(image, cancellationToken);
            return ArtifactImageDeletionFinalizationResult.FinalizationPending(request.ArtifactImageId);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }

    private static AuditWriteRequest BuildAuditRequest(ArtifactImage image, ArtifactImageDeletionFinalizationRequest request)
    {
        if (request.DeletionMode == ArtifactImageDeletionMode.Privileged)
        {
            return new AuditWriteRequest(
                PhotographyAuditActions.ImageDeletePrivileged,
                "Photography",
                nameof(ArtifactImage),
                image.ArtifactImageId.ToString(),
                "Permanently deleted artifact image under privileged authorization.",
                $"ArtifactId={image.ArtifactId}; ArtifactImageId={image.ArtifactImageId}; ActorUserId={image.DeletedByUserId}; DeletedAtUtc={image.DeletedAt:O}; Reason={image.DeletionReason}",
                image.DeletionRequestedByUserId);
        }

        return new AuditWriteRequest(
            PhotographyAuditActions.ImageDeleteByUploaderGrace,
            "Photography",
            nameof(ArtifactImage),
            image.ArtifactImageId.ToString(),
            "Permanently deleted artifact image under the uploader grace-period correction rule.",
            $"ArtifactId={image.ArtifactId}; ArtifactImageId={image.ArtifactImageId}; ActorUserId={image.DeletedByUserId}; DeletedAtUtc={image.DeletedAt:O}; Rule=UploaderGracePeriod",
            image.DeletionRequestedByUserId);
    }

    private async Task ResolveDeleteCleanupRecoveriesAsync(Guid artifactImageId, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        var recoveries = await dbContext.StorageOperationRecoveries
            .Where(recovery =>
                recovery.ArtifactImageId == artifactImageId
                && recovery.OperationType == StorageOperationRecoveryType.DeleteCleanup
                && recovery.Status != StorageOperationRecoveryStatus.Resolved)
            .ToListAsync(cancellationToken);

        if (recoveries.Count == 0)
        {
            return;
        }

        foreach (var recovery in recoveries)
        {
            recovery.MarkResolved(resolvedAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task TryRecordFinalizationRecoveryAsync(ArtifactImage image, CancellationToken cancellationToken)
    {
        try
        {
            var recovery = StorageOperationRecovery.Create(
                StorageOperationRecoveryType.DeleteCleanup,
                image.ArtifactId,
                [image.OriginalObjectKey, .. image.Derivatives.Select(derivative => derivative.ObjectKey)],
                "Storage objects were deleted but final deletion metadata could not be committed. The image remains pending deletion for retry.",
                image.ArtifactImageId);
            dbContext.StorageOperationRecoveries.Add(recovery);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            dbContext.ClearTrackedChanges();
        }
    }
}

public sealed record ArtifactImageDeletionFinalizationRequest(
    Guid ArtifactImageId,
    ArtifactImageDeletionMode DeletionMode,
    int? ExpectedConcurrencyToken = null);

public enum ArtifactImageDeletionFinalizationOutcome
{
    Completed = 1,
    AlreadyFinalized = 2,
    Conflict = 3,
    InvalidState = 4,
    FinalizationPending = 5
}

public sealed record ArtifactImageDeletionFinalizationResult(
    ArtifactImageDeletionFinalizationOutcome Outcome,
    Guid ArtifactImageId,
    int? ConcurrencyToken,
    string? AuditReference = null)
{
    public bool Succeeded =>
        Outcome is ArtifactImageDeletionFinalizationOutcome.Completed or ArtifactImageDeletionFinalizationOutcome.AlreadyFinalized;

    public static ArtifactImageDeletionFinalizationResult Completed(Guid artifactImageId, int concurrencyToken, string? auditReference = null) =>
        new(ArtifactImageDeletionFinalizationOutcome.Completed, artifactImageId, concurrencyToken, auditReference);

    public static ArtifactImageDeletionFinalizationResult AlreadyFinalized(Guid artifactImageId, int concurrencyToken) =>
        new(ArtifactImageDeletionFinalizationOutcome.AlreadyFinalized, artifactImageId, concurrencyToken);

    public static ArtifactImageDeletionFinalizationResult Conflict(Guid artifactImageId) =>
        new(ArtifactImageDeletionFinalizationOutcome.Conflict, artifactImageId, null);

    public static ArtifactImageDeletionFinalizationResult InvalidState(Guid artifactImageId) =>
        new(ArtifactImageDeletionFinalizationOutcome.InvalidState, artifactImageId, null);

    public static ArtifactImageDeletionFinalizationResult FinalizationPending(Guid artifactImageId) =>
        new(ArtifactImageDeletionFinalizationOutcome.FinalizationPending, artifactImageId, null);
}

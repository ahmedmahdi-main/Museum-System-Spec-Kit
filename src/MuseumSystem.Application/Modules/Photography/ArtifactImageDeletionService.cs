using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class ArtifactImageDeletionService(
    IMuseumDbContext dbContext,
    IAuditWriter auditWriter,
    IArtifactImageStorage storage,
    ArtifactImageDeletionFinalizationService finalizationService)
{
    public async Task<ArtifactImageDeletionResult> DeleteAsync(
        AuthorizedArtifactImageDeletion request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArtifactImageId == Guid.Empty)
        {
            throw new ArgumentException("An artifact image is required.", nameof(request));
        }

        if (request.ExpectedConcurrencyToken < 0)
        {
            throw new ArgumentException("Expected concurrency token cannot be negative.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ActorUserId))
        {
            throw new ArgumentException("An actor is required.", nameof(request));
        }

        var image = await dbContext.ArtifactImages
            .Include(candidate => candidate.Derivatives)
            .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == request.ArtifactImageId, cancellationToken);

        if (image is null)
        {
            return ArtifactImageDeletionResult.InvalidState(request.ArtifactImageId);
        }

        if (image.Status != ArtifactImageStatus.Available)
        {
            return ArtifactImageDeletionResult.InvalidState(image.ArtifactImageId);
        }

        if (image.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return ArtifactImageDeletionResult.Conflict(image.ArtifactImageId);
        }

        var originalObjectKey = image.OriginalObjectKey;
        var derivativeObjectKeys = image.Derivatives.Select(derivative => derivative.ObjectKey).ToArray();

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            image.MarkDeletePending(request.DeletionMode, request.ActorUserId, request.ServerNowUtc, request.DeletionReason);

            Guid? previousPrimaryImageId = null;
            var clearedPrimary = false;
            var state = await dbContext.ArtifactPhotographyStates
                .FirstOrDefaultAsync(candidate => candidate.ArtifactId == image.ArtifactId, cancellationToken);
            if (state is not null && state.PrimaryImageId == image.ArtifactImageId)
            {
                previousPrimaryImageId = state.PrimaryImageId;
                state.ClearPrimaryImage(request.ActorUserId);
                clearedPrimary = true;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (clearedPrimary)
            {
                await auditWriter.WriteAsync(new AuditWriteRequest(
                    PhotographyAuditActions.PrimaryImageChange,
                    "Photography",
                    nameof(ArtifactPhotographyState),
                    image.ArtifactId.ToString(),
                    "Cleared artifact Primary Image because the image entered permanent-deletion processing.",
                    $"ArtifactId={image.ArtifactId}; PreviousPrimaryImageId={FormatAuditValue(previousPrimaryImageId)}; NewPrimaryImageId=<null>; ActorUserId={request.ActorUserId}; ChangedAtUtc={request.ServerNowUtc:O}"),
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return ArtifactImageDeletionResult.Conflict(request.ArtifactImageId);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }

        var deleteResult = await storage.DeleteImageObjectsAsync(originalObjectKey, derivativeObjectKeys, cancellationToken);
        if (!deleteResult.Succeeded)
        {
            await CreateDeleteCleanupRecoveryAsync(image, originalObjectKey, derivativeObjectKeys, deleteResult, cancellationToken);
            return ArtifactImageDeletionResult.RecoveryRequired(image.ArtifactImageId, image.ConcurrencyToken);
        }

        var finalizationResult = await finalizationService.FinalizeAsync(
            new ArtifactImageDeletionFinalizationRequest(
                image.ArtifactImageId,
                request.DeletionMode,
                image.ConcurrencyToken),
            cancellationToken);

        return finalizationResult.Outcome switch
        {
            ArtifactImageDeletionFinalizationOutcome.Completed or ArtifactImageDeletionFinalizationOutcome.AlreadyFinalized =>
                ArtifactImageDeletionResult.Completed(finalizationResult.ArtifactImageId, finalizationResult.ConcurrencyToken ?? image.ConcurrencyToken, finalizationResult.AuditReference),
            _ => ArtifactImageDeletionResult.FinalizationPending(finalizationResult.ArtifactImageId, image.ConcurrencyToken)
        };
    }

    private async Task CreateDeleteCleanupRecoveryAsync(
        ArtifactImage image,
        ImageStorageObjectKey originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        ArtifactImageObjectsDeleteResult deleteResult,
        CancellationToken cancellationToken)
    {
        var objectKeys = new[] { originalObjectKey }.Concat(derivativeObjectKeys).ToArray();
        var failureSummary = SanitizeFailureSummary(
            deleteResult.Failure?.OperationalSummary
            ?? deleteResult.Failure?.StaffFacingMessage
            ?? "Image deletion object cleanup failed.");

        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup,
            image.ArtifactId,
            objectKeys,
            failureSummary,
            image.ArtifactImageId);

        dbContext.StorageOperationRecoveries.Add(recovery);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string SanitizeFailureSummary(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "Image deletion object cleanup failed." : value.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private static string FormatAuditValue(Guid? value) => value?.ToString() ?? "<null>";
}

public sealed record AuthorizedArtifactImageDeletion(
    Guid ArtifactImageId,
    int ExpectedConcurrencyToken,
    ArtifactImageDeletionMode DeletionMode,
    string? DeletionReason,
    string ActorUserId,
    DateTimeOffset ServerNowUtc);

public enum ArtifactImageDeletionOutcome
{
    Completed = 1,
    Conflict = 2,
    InvalidState = 3,
    RecoveryRequired = 4,
    FinalizationPending = 5
}

public sealed record ArtifactImageDeletionResult(
    ArtifactImageDeletionOutcome Outcome,
    Guid ArtifactImageId,
    int? ConcurrencyToken,
    string? AuditReference = null)
{
    public bool Succeeded => Outcome == ArtifactImageDeletionOutcome.Completed;

    public static ArtifactImageDeletionResult Completed(Guid artifactImageId, int concurrencyToken, string? auditReference = null) =>
        new(ArtifactImageDeletionOutcome.Completed, artifactImageId, concurrencyToken, auditReference);

    public static ArtifactImageDeletionResult Conflict(Guid artifactImageId) =>
        new(ArtifactImageDeletionOutcome.Conflict, artifactImageId, null);

    public static ArtifactImageDeletionResult InvalidState(Guid artifactImageId) =>
        new(ArtifactImageDeletionOutcome.InvalidState, artifactImageId, null);

    public static ArtifactImageDeletionResult RecoveryRequired(Guid artifactImageId, int concurrencyToken) =>
        new(ArtifactImageDeletionOutcome.RecoveryRequired, artifactImageId, concurrencyToken);

    public static ArtifactImageDeletionResult FinalizationPending(Guid artifactImageId, int concurrencyToken) =>
        new(ArtifactImageDeletionOutcome.FinalizationPending, artifactImageId, concurrencyToken);
}

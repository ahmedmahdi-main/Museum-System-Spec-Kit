using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

/// <summary>
/// Internal/system retry handler for <see cref="StorageOperationRecovery"/>. Not staff-facing:
/// no permission check, no sixth Photography permission, no MinIO-specific behavior.
/// </summary>
public sealed class StorageOperationRecoveryUseCase(
    IMuseumDbContext dbContext,
    IArtifactImageStorage storage,
    ArtifactImageDeletionFinalizationService finalizationService,
    IAuditWriter auditWriter,
    TimeProvider clock)
{
    public async Task<StorageOperationRecoveryRetryResult> RetryAsync(
        StorageOperationRecoveryRetryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.StorageOperationRecoveryId == Guid.Empty)
        {
            return StorageOperationRecoveryRetryResult.NotFound(command.StorageOperationRecoveryId);
        }

        var recovery = await dbContext.StorageOperationRecoveries
            .FirstOrDefaultAsync(candidate => candidate.StorageOperationRecoveryId == command.StorageOperationRecoveryId, cancellationToken);

        if (recovery is null)
        {
            return StorageOperationRecoveryRetryResult.NotFound(command.StorageOperationRecoveryId);
        }

        if (recovery.Status == StorageOperationRecoveryStatus.Resolved)
        {
            return StorageOperationRecoveryRetryResult.AlreadyResolved(recovery.StorageOperationRecoveryId);
        }

        if (!IsRetryableType(recovery.OperationType))
        {
            return await MarkFailedAsync(recovery, clock.GetUtcNow(), StorageOperationRecoveryRetryOutcome.UnsupportedRecoveryType, cancellationToken);
        }

        var attemptedAt = clock.GetUtcNow();
        var previousStatus = recovery.Status;
        recovery.MarkRetrying(attemptedAt);

        var retryingPersistFailure = await PersistTransitionAsync(
            recovery,
            PhotographyAuditActions.StorageRecoveryRetry,
            previousStatus,
            attemptedAt,
            cancellationToken);
        if (retryingPersistFailure is not null)
        {
            return retryingPersistFailure;
        }

        return recovery.OperationType switch
        {
            StorageOperationRecoveryType.UploadCleanup => await RetryUploadCleanupAsync(recovery, attemptedAt, cancellationToken),
            StorageOperationRecoveryType.DeleteCleanup => await RetryDeleteCleanupAsync(recovery, attemptedAt, cancellationToken),
            _ => await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.UnsupportedRecoveryType, cancellationToken)
        };
    }

    private async Task<StorageOperationRecoveryRetryResult> RetryUploadCleanupAsync(
        StorageOperationRecovery recovery,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        var allCleaned = true;
        foreach (var objectKey in recovery.ObjectKeys)
        {
            ArtifactImageStorageDeleteResult deleteResult;
            try
            {
                deleteResult = await storage.DeleteObjectAsync(objectKey, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken, UnexpectedStorageFailureSummary);
            }

            if (!IsCleanedOutcome(deleteResult.Kind))
            {
                allCleaned = false;
            }
        }

        return allCleaned
            ? await MarkResolvedAsync(recovery, attemptedAt, cancellationToken)
            : await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken);
    }

    private async Task<StorageOperationRecoveryRetryResult> RetryDeleteCleanupAsync(
        StorageOperationRecovery recovery,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        if (recovery.ArtifactImageId is null)
        {
            return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.InvalidState, cancellationToken);
        }

        foreach (var objectKey in recovery.ObjectKeys)
        {
            ArtifactImageStorageStatResult stat;
            try
            {
                stat = await storage.StatAsync(objectKey, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken, UnexpectedStorageFailureSummary);
            }

            if (stat.Exists)
            {
                ArtifactImageStorageDeleteResult deleteResult;
                try
                {
                    deleteResult = await storage.DeleteObjectAsync(objectKey, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken, UnexpectedStorageFailureSummary);
                }

                if (!IsCleanedOutcome(deleteResult.Kind))
                {
                    return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken);
                }
            }
            else if (stat.Kind != ArtifactImageStorageResultKind.NotFound)
            {
                return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken);
            }
        }

        return await FinalizeDeleteCleanupAsync(recovery, attemptedAt, cancellationToken);
    }

    private async Task<StorageOperationRecoveryRetryResult> FinalizeDeleteCleanupAsync(
        StorageOperationRecovery recovery,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        var image = await dbContext.ArtifactImages
            .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == recovery.ArtifactImageId!.Value, cancellationToken);

        if (image is null)
        {
            return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.InvalidState, cancellationToken);
        }

        if (image.Status == ArtifactImageStatus.Deleted)
        {
            return await MarkResolvedAsync(recovery, attemptedAt, cancellationToken);
        }

        if (image.Status != ArtifactImageStatus.DeletePending)
        {
            return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.InvalidState, cancellationToken);
        }

        if (image.DeletionMode is null
            || string.IsNullOrWhiteSpace(image.DeletionRequestedByUserId)
            || image.DeletionRequestedAt is null)
        {
            return await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.InvalidState, cancellationToken);
        }

        var finalizationResult = await finalizationService.FinalizeAsync(
            new ArtifactImageDeletionFinalizationRequest(image.ArtifactImageId, image.DeletionMode.Value, image.ConcurrencyToken),
            cancellationToken);

        return finalizationResult.Outcome switch
        {
            ArtifactImageDeletionFinalizationOutcome.Completed or ArtifactImageDeletionFinalizationOutcome.AlreadyFinalized =>
                await ReloadAndResolveAsync(recovery.StorageOperationRecoveryId, StorageOperationRecoveryStatus.Retrying, attemptedAt, cancellationToken),
            ArtifactImageDeletionFinalizationOutcome.Conflict =>
                await ReloadAfterFinalizationConflictAsync(recovery.StorageOperationRecoveryId, cancellationToken),
            ArtifactImageDeletionFinalizationOutcome.InvalidState =>
                await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.InvalidState, cancellationToken),
            _ => await MarkFailedAsync(recovery, attemptedAt, StorageOperationRecoveryRetryOutcome.RetryFailed, cancellationToken)
        };
    }

    /// <summary>
    /// Reloads the current row after a linked <see cref="ArtifactImageDeletionFinalizationService"/> call.
    /// That call may have already auto-resolved this exact row (its Completed path resolves every open
    /// DeleteCleanup row for the image) - in that case the row's real transition for THIS retry attempt was
    /// still Retrying -> Resolved, so <paramref name="previousStatusForThisAttempt"/> (always Retrying, the
    /// status persisted before finalization was invoked) is used for the audit rather than the already-mutated
    /// post-reload status, to avoid reporting a misleading Resolved -> Resolved transition.
    /// </summary>
    private async Task<StorageOperationRecoveryRetryResult> ReloadAndResolveAsync(
        Guid recoveryId,
        StorageOperationRecoveryStatus previousStatusForThisAttempt,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        var recovery = await dbContext.StorageOperationRecoveries
            .FirstOrDefaultAsync(candidate => candidate.StorageOperationRecoveryId == recoveryId, cancellationToken);

        if (recovery is null)
        {
            return StorageOperationRecoveryRetryResult.NotFound(recoveryId);
        }

        if (recovery.Status != StorageOperationRecoveryStatus.Resolved)
        {
            return await MarkResolvedAsync(recovery, attemptedAt, cancellationToken);
        }

        await WriteAuditAsync(PhotographyAuditActions.StorageRecoveryResolved, recovery, previousStatusForThisAttempt, attemptedAt, cancellationToken);
        return StorageOperationRecoveryRetryResult.Resolved(recoveryId);
    }

    private async Task<StorageOperationRecoveryRetryResult> ReloadAfterFinalizationConflictAsync(
        Guid recoveryId,
        CancellationToken cancellationToken)
    {
        dbContext.ClearTrackedChanges();
        var authoritative = await dbContext.StorageOperationRecoveries
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.StorageOperationRecoveryId == recoveryId, cancellationToken);

        if (authoritative is null)
        {
            return StorageOperationRecoveryRetryResult.NotFound(recoveryId);
        }

        return authoritative.Status == StorageOperationRecoveryStatus.Resolved
            ? StorageOperationRecoveryRetryResult.AlreadyResolved(recoveryId)
            : StorageOperationRecoveryRetryResult.Conflict(recoveryId);
    }

    private async Task<StorageOperationRecoveryRetryResult> MarkResolvedAsync(
        StorageOperationRecovery recovery,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        var previousStatus = recovery.Status;
        recovery.MarkResolved(attemptedAt);

        var failure = await PersistTransitionAsync(recovery, PhotographyAuditActions.StorageRecoveryResolved, previousStatus, attemptedAt, cancellationToken);
        return failure ?? StorageOperationRecoveryRetryResult.Resolved(recovery.StorageOperationRecoveryId);
    }

    private async Task<StorageOperationRecoveryRetryResult> MarkFailedAsync(
        StorageOperationRecovery recovery,
        DateTimeOffset attemptedAt,
        StorageOperationRecoveryRetryOutcome outcome,
        CancellationToken cancellationToken,
        string? domainSummaryOverride = null)
    {
        var previousStatus = recovery.Status;
        recovery.MarkFailedNeedsAttention(attemptedAt, domainSummaryOverride ?? DomainSummaryFor(recovery.OperationType));

        var failure = await PersistTransitionAsync(recovery, PhotographyAuditActions.StorageConsistencyIssue, previousStatus, attemptedAt, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return outcome switch
        {
            StorageOperationRecoveryRetryOutcome.UnsupportedRecoveryType => StorageOperationRecoveryRetryResult.UnsupportedRecoveryType(recovery.StorageOperationRecoveryId),
            StorageOperationRecoveryRetryOutcome.InvalidState => StorageOperationRecoveryRetryResult.InvalidState(recovery.StorageOperationRecoveryId),
            _ => StorageOperationRecoveryRetryResult.RetryFailed(recovery.StorageOperationRecoveryId)
        };
    }

    private async Task<StorageOperationRecoveryRetryResult?> PersistTransitionAsync(
        StorageOperationRecovery recovery,
        string auditActionName,
        StorageOperationRecoveryStatus previousStatus,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(auditActionName, recovery, previousStatus, attemptedAt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return await ReloadAfterFinalizationConflictAsync(recovery.StorageOperationRecoveryId, cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }

    private Task WriteAuditAsync(
        string actionName,
        StorageOperationRecovery recovery,
        StorageOperationRecoveryStatus previousStatus,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        var summary = actionName switch
        {
            PhotographyAuditActions.StorageRecoveryRetry => "Internal storage recovery retry attempted.",
            PhotographyAuditActions.StorageRecoveryResolved => "Internal storage recovery resolved.",
            _ => "Internal storage recovery requires manual attention."
        };

        var changeSummary =
            $"OperationType={recovery.OperationType}; ArtifactId={recovery.ArtifactId}; ArtifactImageId={FormatGuid(recovery.ArtifactImageId)}; " +
            $"PreviousStatus={previousStatus}; NewStatus={recovery.Status}; AttemptedAtUtc={attemptedAt:O}" +
            (recovery.ResolvedAt is not null ? $"; ResolvedAtUtc={recovery.ResolvedAt:O}" : string.Empty);

        return auditWriter.WriteAsync(
            new AuditWriteRequest(
                actionName,
                "Photography",
                nameof(StorageOperationRecovery),
                recovery.StorageOperationRecoveryId.ToString(),
                summary,
                changeSummary),
            cancellationToken);
    }

    private static bool IsRetryableType(StorageOperationRecoveryType type) =>
        type is StorageOperationRecoveryType.UploadCleanup or StorageOperationRecoveryType.DeleteCleanup;

    private static bool IsCleanedOutcome(ArtifactImageStorageResultKind kind) =>
        kind is ArtifactImageStorageResultKind.Success or ArtifactImageStorageResultKind.NotFound;

    private const string UnexpectedStorageFailureSummary =
        "Storage recovery operation failed unexpectedly and requires internal attention.";

    private static string DomainSummaryFor(StorageOperationRecoveryType operationType) => operationType switch
    {
        StorageOperationRecoveryType.UploadCleanup => "Upload cleanup retry did not confirm removal of all recorded objects.",
        StorageOperationRecoveryType.DeleteCleanup => "Delete cleanup retry did not complete storage or metadata verification.",
        _ => "Recovery type is not supported by the internal retry handler."
    };

    private static string FormatGuid(Guid? value) => value?.ToString() ?? "<null>";
}

public sealed record StorageOperationRecoveryRetryCommand(Guid StorageOperationRecoveryId);

public enum StorageOperationRecoveryRetryOutcome
{
    Resolved = 1,
    AlreadyResolved = 2,
    RetryFailed = 3,
    UnsupportedRecoveryType = 4,
    NotFound = 5,
    Conflict = 6,
    InvalidState = 7
}

public sealed record StorageOperationRecoveryRetryResult(
    StorageOperationRecoveryRetryOutcome Outcome,
    Guid? StorageOperationRecoveryId,
    string StaffFacingMessage)
{
    public bool Succeeded =>
        Outcome is StorageOperationRecoveryRetryOutcome.Resolved or StorageOperationRecoveryRetryOutcome.AlreadyResolved;

    public static StorageOperationRecoveryRetryResult Resolved(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.Resolved, recoveryId, "Storage recovery completed.");

    public static StorageOperationRecoveryRetryResult AlreadyResolved(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.AlreadyResolved, recoveryId, "Storage recovery completed.");

    public static StorageOperationRecoveryRetryResult RetryFailed(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.RetryFailed, recoveryId, "Image storage is temporarily unavailable for recovery.");

    public static StorageOperationRecoveryRetryResult UnsupportedRecoveryType(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.UnsupportedRecoveryType, recoveryId, "Recovery type is not supported by this internal handler.");

    public static StorageOperationRecoveryRetryResult NotFound(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.NotFound, recoveryId == Guid.Empty ? null : recoveryId, "Storage recovery record was not found.");

    public static StorageOperationRecoveryRetryResult Conflict(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.Conflict, recoveryId, "Storage recovery remains pending for internal attention.");

    public static StorageOperationRecoveryRetryResult InvalidState(Guid recoveryId) =>
        new(StorageOperationRecoveryRetryOutcome.InvalidState, recoveryId, "Storage recovery remains pending for internal attention.");
}

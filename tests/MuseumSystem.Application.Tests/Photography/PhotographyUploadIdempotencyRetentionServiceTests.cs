using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

/// <summary>
/// V0 prerequisite for T113: durable upload-recovery correlation and idempotency retention.
/// Proves PhotographyUploadIdempotencyRetentionService.CleanupExpiredAsync only purges
/// PhotographyUploadOperation rows that are expired, terminal-with-final-outcomes, and have no
/// unresolved StorageOperationRecovery correlated by PhotographyUploadOperationId (never by ArtifactId).
/// </summary>
public sealed class PhotographyUploadIdempotencyRetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);
    private const int RetentionDays = 7;
    private static readonly DateTimeOffset ExpiredLastSeenAt = Now - TimeSpan.FromDays(RetentionDays) - TimeSpan.FromMinutes(1);
    private static readonly DateTimeOffset RecentLastSeenAt = Now - TimeSpan.FromDays(1);

    // A-C. Expired terminal operation with final outcomes and no recoveries is removed.
    [Theory]
    [InlineData(PhotographyUploadOperationStatus.Completed)]
    [InlineData(PhotographyUploadOperationStatus.CompletedWithFailures)]
    [InlineData(PhotographyUploadOperationStatus.Failed)]
    public async Task A_C_expired_terminal_operation_with_final_outcomes_and_no_recoveries_is_removed(PhotographyUploadOperationStatus terminalStatus)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt, terminalStatus: terminalStatus);
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(1, removed);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(0, await db.PhotographyUploadFileOutcomes.CountAsync());
    }

    // H. Recent LastSeenAt is retained.
    [Fact]
    public async Task H_recent_last_seen_at_is_retained()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await SeedTerminalOperationAsync(db, artifact.ArtifactId, RecentLastSeenAt);
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
    }

    // D. RecoveryNeeded operation retained even when expired, final-outcome-only, and unblocked.
    [Fact]
    public async Task D_recovery_needed_operation_with_final_outcomes_is_retained()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt, terminalStatus: PhotographyUploadOperationStatus.Failed);
        ForceStatus(operation, PhotographyUploadOperationStatus.RecoveryNeeded);
        await db.SaveChangesAsync();
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        var retained = await db.PhotographyUploadOperations.Include(candidate => candidate.FileOutcomes).SingleAsync();
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, retained.Status);
        Assert.All(retained.FileOutcomes, outcome => Assert.False(outcome.IsUnresolved));
    }
    // I. InProgress operation retained.
    [Fact]
    public async Task I_in_progress_operation_retained()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifact.ArtifactId);
        db.PhotographyUploadOperations.Add(operation);
        ForceLastSeenAt(operation, ExpiredLastSeenAt);
        await db.SaveChangesAsync();
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
    }

    // J. Operation with unresolved file outcome retained.
    [Fact]
    public async Task J_operation_with_unresolved_file_outcome_retained()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifact.ArtifactId);
        db.PhotographyUploadOperations.Add(operation);
        var outcome = PhotographyUploadFileOutcome.RecoveryNeeded(operation.PhotographyUploadOperationId, 0, "a.jpg", "fp-a", "Recovery is required.");
        operation.AddFileOutcome(outcome);
        db.PhotographyUploadFileOutcomes.Add(outcome);
        operation.FinalizeBatch(1);
        ForceLastSeenAt(operation, ExpiredLastSeenAt);
        await db.SaveChangesAsync();
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db.PhotographyUploadFileOutcomes.CountAsync());
    }

    // K. Pending linked recovery blocks.
    [Fact]
    public async Task K_pending_linked_recovery_blocks_cleanup()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        await SeedLinkedRecoveryAsync(db, artifact.ArtifactId, operation.PhotographyUploadOperationId, StorageOperationRecoveryStatus.Pending);
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
    }

    // L. Retrying linked recovery blocks.
    [Fact]
    public async Task L_retrying_linked_recovery_blocks_cleanup()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        await SeedLinkedRecoveryAsync(db, artifact.ArtifactId, operation.PhotographyUploadOperationId, StorageOperationRecoveryStatus.Retrying);
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
    }

    // M. FailedNeedsAttention linked recovery blocks.
    [Fact]
    public async Task M_failed_needs_attention_linked_recovery_blocks_cleanup()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        await SeedLinkedRecoveryAsync(db, artifact.ArtifactId, operation.PhotographyUploadOperationId, StorageOperationRecoveryStatus.FailedNeedsAttention);
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
    }

    // N. Resolved linked recovery does not block when otherwise eligible.
    [Fact]
    public async Task N_resolved_linked_recovery_does_not_block_cleanup()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        await SeedLinkedRecoveryAsync(db, artifact.ArtifactId, operation.PhotographyUploadOperationId, StorageOperationRecoveryStatus.Resolved);
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(1, removed);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
    }

    // O. Unrelated unresolved recovery sharing ArtifactId does NOT block cleanup (proves the
    // correlation used is operation-specific, not an ArtifactId heuristic).
    [Fact]
    public async Task O_unrelated_unresolved_recovery_sharing_artifact_id_does_not_block_cleanup()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        // Same ArtifactId, but not correlated to this operation (no PhotographyUploadOperationId).
        var unrelatedRecovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/orphan.jpg")], "unrelated");
        db.StorageOperationRecoveries.Add(unrelatedRecovery);
        await db.SaveChangesAsync();
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(1, removed);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db.StorageOperationRecoveries.CountAsync());
    }

    // P. Resolved StorageOperationRecovery row is retained after idempotency purge.
    [Fact]
    public async Task P_resolved_recovery_row_is_retained_after_idempotency_purge()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        var recovery = await SeedLinkedRecoveryAsync(db, artifact.ArtifactId, operation.PhotographyUploadOperationId, StorageOperationRecoveryStatus.Resolved);
        var service = NewService(db, Now);

        await service.CleanupExpiredAsync();

        var retained = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(recovery.StorageOperationRecoveryId, retained.StorageOperationRecoveryId);
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, retained.Status);
    }

    // Q. Correlation IDs on retained recovery history remain unchanged after the operation/outcome
    // records that produced them are purged.
    [Fact]
    public async Task Q_correlation_ids_on_retained_recovery_remain_unchanged_after_purge()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var operation = await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        var originalOperationId = operation.PhotographyUploadOperationId;
        var originalOutcomeId = operation.FileOutcomes.Single().PhotographyUploadFileOutcomeId;
        var recovery = await SeedLinkedRecoveryAsync(db, artifact.ArtifactId, operation.PhotographyUploadOperationId, StorageOperationRecoveryStatus.Resolved, originalOutcomeId);
        var service = NewService(db, Now);

        await service.CleanupExpiredAsync();

        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        var retained = await db.StorageOperationRecoveries.SingleAsync(candidate => candidate.StorageOperationRecoveryId == recovery.StorageOperationRecoveryId);
        Assert.Equal(originalOperationId, retained.PhotographyUploadOperationId);
        Assert.Equal(originalOutcomeId, retained.PhotographyUploadFileOutcomeId);
    }

    // R. File outcomes are removed before/with the operation, without touching ArtifactImage or
    // PhotographySet rows.
    [Fact]
    public async Task R_file_outcomes_removed_without_touching_artifact_image_or_photography_set()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        await SeedTerminalOperationAsync(db, artifact.ArtifactId, ExpiredLastSeenAt);
        await db.SaveChangesAsync();
        var service = NewService(db, Now);

        var removed = await service.CleanupExpiredAsync();

        Assert.Equal(1, removed);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(0, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        var retainedImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(image.ArtifactImageId, retainedImage.ArtifactImageId);
    }

    private static PhotographyUploadIdempotencyRetentionService NewService(
        MuseumDbContext db,
        DateTimeOffset now,
        int retentionDays = RetentionDays,
        IMuseumDbContext? persistenceContext = null)
    {
        var context = persistenceContext ?? db;
        var clock = new FixedTimeProvider(now);
        var options = Options.Create(new PhotographyIdempotencyOptions { RetentionDays = retentionDays });
        return new PhotographyUploadIdempotencyRetentionService(context, clock, options);
    }

    private static async Task<PhotographyUploadOperation> SeedTerminalOperationAsync(
        MuseumDbContext db,
        Guid artifactId,
        DateTimeOffset lastSeenAt,
        int outcomeCount = 1,
        PhotographyUploadOperationStatus terminalStatus = PhotographyUploadOperationStatus.Failed)
    {
        var operation = terminalStatus switch
        {
            PhotographyUploadOperationStatus.Completed => SeedCompletedOperation(db, artifactId),
            PhotographyUploadOperationStatus.CompletedWithFailures => SeedCompletedWithFailuresOperation(db, artifactId),
            PhotographyUploadOperationStatus.Failed => SeedFailedOperation(db, artifactId, outcomeCount),
            _ => throw new ArgumentOutOfRangeException(nameof(terminalStatus), "Only terminal upload operation statuses can be seeded.")
        };

        ForceLastSeenAt(operation, lastSeenAt);
        await db.SaveChangesAsync();
        Assert.Equal(terminalStatus, operation.Status);
        return operation;
    }

    private static PhotographyUploadOperation SeedCompletedOperation(MuseumDbContext db, Guid artifactId)
    {
        var operation = SeedAppendOperationWithSuccessfulOutcome(db, artifactId, out _);
        operation.FinalizeBatch(1);
        return operation;
    }

    private static PhotographyUploadOperation SeedCompletedWithFailuresOperation(MuseumDbContext db, Guid artifactId)
    {
        var operation = SeedAppendOperationWithSuccessfulOutcome(db, artifactId, out _);
        var rejected = PhotographyUploadFileOutcome.Rejected(operation.PhotographyUploadOperationId, 1, "rejected.jpg", $"fp-{Guid.NewGuid():N}", "Unsupported file type.");
        operation.AddFileOutcome(rejected);
        db.PhotographyUploadFileOutcomes.Add(rejected);
        operation.FinalizeBatch(2);
        return operation;
    }

    private static PhotographyUploadOperation SeedFailedOperation(MuseumDbContext db, Guid artifactId, int outcomeCount)
    {
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifactId);
        db.PhotographyUploadOperations.Add(operation);
        for (var ordinal = 0; ordinal < outcomeCount; ordinal++)
        {
            var outcome = PhotographyUploadFileOutcome.Rejected(operation.PhotographyUploadOperationId, ordinal, $"file-{ordinal}.jpg", $"fp-{Guid.NewGuid():N}", "Unsupported file type.");
            operation.AddFileOutcome(outcome);
            db.PhotographyUploadFileOutcomes.Add(outcome);
        }

        operation.FinalizeBatch(outcomeCount);
        return operation;
    }

    private static PhotographyUploadOperation SeedAppendOperationWithSuccessfulOutcome(MuseumDbContext db, Guid artifactId, out ArtifactImage image)
    {
        var artifact = db.Artifacts.Local.Single(candidate => candidate.ArtifactId == artifactId);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.AppendToSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifactId, set.PhotographySetId);
        db.PhotographyUploadOperations.Add(operation);
        var outcome = PhotographyUploadFileOutcome.Succeeded(
            operation.PhotographyUploadOperationId,
            0,
            "front.jpg",
            $"fp-{Guid.NewGuid():N}",
            image.ArtifactImageId,
            image.OriginalObjectKey,
            []);
        operation.AddFileOutcome(outcome);
        db.PhotographyUploadFileOutcomes.Add(outcome);
        return operation;
    }

    private static async Task<StorageOperationRecovery> SeedLinkedRecoveryAsync(
        MuseumDbContext db,
        Guid artifactId,
        Guid photographyUploadOperationId,
        StorageOperationRecoveryStatus status,
        Guid? photographyUploadFileOutcomeId = null)
    {
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.UploadCleanup,
            artifactId,
            [ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/orphan.jpg")],
            "summary",
            artifactImageId: null,
            photographyUploadOperationId: photographyUploadOperationId,
            photographyUploadFileOutcomeId: photographyUploadFileOutcomeId ?? Guid.NewGuid());

        switch (status)
        {
            case StorageOperationRecoveryStatus.Retrying:
                recovery.MarkRetrying(ExpiredLastSeenAt);
                break;
            case StorageOperationRecoveryStatus.FailedNeedsAttention:
                recovery.MarkRetrying(ExpiredLastSeenAt);
                recovery.MarkFailedNeedsAttention(ExpiredLastSeenAt, "Previous attempt failed.");
                break;
            case StorageOperationRecoveryStatus.Resolved:
                recovery.MarkRetrying(ExpiredLastSeenAt);
                recovery.MarkResolved(ExpiredLastSeenAt);
                break;
            case StorageOperationRecoveryStatus.Pending:
            default:
                break;
        }

        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        return recovery;
    }

    private static void ForceLastSeenAt(PhotographyUploadOperation operation, DateTimeOffset lastSeenAt) =>
        SetOperationProperty(operation, nameof(PhotographyUploadOperation.LastSeenAt), lastSeenAt);

    private static void ForceStatus(PhotographyUploadOperation operation, PhotographyUploadOperationStatus status)
    {
        SetOperationProperty(operation, nameof(PhotographyUploadOperation.Status), status);
        if (status == PhotographyUploadOperationStatus.RecoveryNeeded)
        {
            SetOperationProperty<DateTimeOffset?>(operation, nameof(PhotographyUploadOperation.CompletedAt), null);
        }
    }

    private static void SetOperationProperty<T>(PhotographyUploadOperation operation, string propertyName, T value)
    {
        var property = typeof(PhotographyUploadOperation).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!;
        property.SetValue(operation, value);
    }
}

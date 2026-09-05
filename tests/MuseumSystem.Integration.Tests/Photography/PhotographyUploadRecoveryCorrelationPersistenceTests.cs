using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Photography;

/// <summary>
/// V0 prerequisite for T113: proves the durable upload-recovery correlation columns
/// (StorageOperationRecovery.PhotographyUploadOperationId/PhotographyUploadFileOutcomeId, added by the
/// AddPhotographyUploadRecoveryCorrelation migration) persist in real PostgreSQL, and that
/// PhotographyUploadIdempotencyRetentionService's retention/blocking behavior works against a real
/// database. This is intentionally the minimum prerequisite coverage - the full T113 persistence test
/// file (retry state transitions, audit persistence, etc.) is separate and still pending.
///
/// The PostgreSQL Photography collection shares one physical database across every test class, and
/// several existing tests assert global (unscoped) row counts on PhotographyUploadFileOutcomes/
/// StorageOperationRecoveries. Every test here therefore removes what it creates in a finally block so
/// it never pollutes those shared-table counts for other test classes.
/// </summary>
[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyUploadRecoveryCorrelationPersistenceTests(PostgresPhotographyTestFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);
    private const int RetentionDays = 7;
    private static readonly DateTimeOffset ExpiredLastSeenAt = Now - TimeSpan.FromDays(RetentionDays) - TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Correlation_ids_persist_across_fresh_dbcontext()
    {
        Guid operationId;
        Guid outcomeId;
        Guid recoveryId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "UR");
            var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifact.ArtifactId);
            seed.PhotographyUploadOperations.Add(operation);
            var outcome = PhotographyUploadFileOutcome.RecoveryNeeded(operation.PhotographyUploadOperationId, 0, "a.jpg", "fp-a", "Recovery is required.");
            operation.AddFileOutcome(outcome);
            seed.PhotographyUploadFileOutcomes.Add(outcome);
            var recovery = StorageOperationRecovery.Create(
                StorageOperationRecoveryType.UploadCleanup,
                artifact.ArtifactId,
                [ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/orphan.jpg")],
                "summary",
                artifactImageId: null,
                photographyUploadOperationId: operation.PhotographyUploadOperationId,
                photographyUploadFileOutcomeId: outcome.PhotographyUploadFileOutcomeId);
            seed.StorageOperationRecoveries.Add(recovery);
            await seed.SaveChangesAsync();

            operationId = operation.PhotographyUploadOperationId;
            outcomeId = outcome.PhotographyUploadFileOutcomeId;
            recoveryId = recovery.StorageOperationRecoveryId;
        }

        try
        {
            await using var reload = fixture.CreateContext();
            var reloadedRecovery = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(candidate => candidate.StorageOperationRecoveryId == recoveryId);

            Assert.Equal(operationId, reloadedRecovery.PhotographyUploadOperationId);
            Assert.Equal(outcomeId, reloadedRecovery.PhotographyUploadFileOutcomeId);
        }
        finally
        {
            await CleanupAsync(operationId, recoveryId);
        }
    }

    [Fact]
    public async Task Legacy_recovery_without_correlation_persists_null_ids()
    {
        Guid recoveryId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "UL");
            var recovery = StorageOperationRecovery.Create(
                StorageOperationRecoveryType.UploadCleanup,
                artifact.ArtifactId,
                [ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/legacy.jpg")],
                "summary");
            seed.StorageOperationRecoveries.Add(recovery);
            await seed.SaveChangesAsync();
            recoveryId = recovery.StorageOperationRecoveryId;
        }

        try
        {
            await using var reload = fixture.CreateContext();
            var reloaded = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(candidate => candidate.StorageOperationRecoveryId == recoveryId);

            Assert.Null(reloaded.PhotographyUploadOperationId);
            Assert.Null(reloaded.PhotographyUploadFileOutcomeId);
        }
        finally
        {
            await CleanupAsync(null, recoveryId);
        }
    }

    [Fact]
    public async Task Retention_stale_candidate_keeps_operation_and_file_outcomes_after_concurrency_rollback()
    {
        Guid operationId;
        Guid outcomeId;
        DateTimeOffset expiredLastSeenAt;
        var replayLastSeenAt = Now;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "RC");
            var operation = await SeedTerminalOperationAsync(seed, artifact.ArtifactId, ExpiredLastSeenAt);
            operationId = operation.PhotographyUploadOperationId;
            outcomeId = operation.FileOutcomes.Single().PhotographyUploadFileOutcomeId;
            expiredLastSeenAt = operation.LastSeenAt;
        }

        try
        {
            await using var staleContext = fixture.CreateContext();
            var staleOperation = await staleContext.PhotographyUploadOperations
                .Include(operation => operation.FileOutcomes)
                .SingleAsync(operation => operation.PhotographyUploadOperationId == operationId);

            await using (var concurrentContext = fixture.CreateContext())
            {
                var concurrentOperation = await concurrentContext.PhotographyUploadOperations
                    .SingleAsync(operation => operation.PhotographyUploadOperationId == operationId);
                concurrentOperation.MarkSeen();
                ForceLastSeenAt(concurrentOperation, replayLastSeenAt);
                await concurrentContext.SaveChangesAsync();
            }

            var service = NewService(staleContext);
            var deleted = await TryDeleteLoadedOperationAsync(service, staleOperation);

            Assert.False(deleted);

            await using var reload = fixture.CreateContext();
            var retainedOperation = await reload.PhotographyUploadOperations
                .AsNoTracking()
                .SingleAsync(operation => operation.PhotographyUploadOperationId == operationId);
            Assert.NotEqual(expiredLastSeenAt, retainedOperation.LastSeenAt);
            Assert.Equal(replayLastSeenAt, retainedOperation.LastSeenAt);
            Assert.True(await reload.PhotographyUploadFileOutcomes
                .AnyAsync(outcome => outcome.PhotographyUploadFileOutcomeId == outcomeId));
        }
        finally
        {
            await CleanupAsync(operationId, null);
        }
    }
    [Fact]
    public async Task Retention_service_deletes_expired_terminal_operation_with_no_recovery_in_postgresql()
    {
        Guid artifactId;
        Guid operationId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "RG");
            artifactId = artifact.ArtifactId;
            var operation = await SeedTerminalOperationAsync(seed, artifact.ArtifactId, ExpiredLastSeenAt);
            operationId = operation.PhotographyUploadOperationId;
        }

        try
        {
            await using (var cleanup = fixture.CreateContext())
            {
                var service = NewService(cleanup);
                var removed = await service.CleanupExpiredAsync();
                Assert.Equal(1, removed);
            }

            await using var reload = fixture.CreateContext();
            Assert.False(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == operationId));
            Assert.False(await reload.PhotographyUploadFileOutcomes.AnyAsync(outcome => outcome.PhotographyUploadOperationId == operationId));
            Assert.True(await reload.Artifacts.AnyAsync(artifact => artifact.ArtifactId == artifactId));
        }
        finally
        {
            await CleanupAsync(operationId, null);
        }
    }

    [Fact]
    public async Task Unresolved_linked_recovery_blocks_postgresql_cleanup()
    {
        Guid operationId;
        Guid recoveryId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "RB");
            var operation = await SeedTerminalOperationAsync(seed, artifact.ArtifactId, ExpiredLastSeenAt);
            operationId = operation.PhotographyUploadOperationId;

            var recovery = StorageOperationRecovery.Create(
                StorageOperationRecoveryType.UploadCleanup,
                artifact.ArtifactId,
                [ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/blocking.jpg")],
                "summary",
                artifactImageId: null,
                photographyUploadOperationId: operation.PhotographyUploadOperationId,
                photographyUploadFileOutcomeId: Guid.NewGuid());
            seed.StorageOperationRecoveries.Add(recovery);
            await seed.SaveChangesAsync();
            recoveryId = recovery.StorageOperationRecoveryId;
        }

        try
        {
            await using (var cleanup = fixture.CreateContext())
            {
                var service = NewService(cleanup);
                var removed = await service.CleanupExpiredAsync();
                Assert.Equal(0, removed);
            }

            await using var reload = fixture.CreateContext();
            Assert.True(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == operationId));
        }
        finally
        {
            await CleanupAsync(operationId, recoveryId);
        }
    }

    [Fact]
    public async Task Resolved_linked_recovery_permits_postgresql_cleanup_and_recovery_history_remains()
    {
        Guid operationId;
        Guid recoveryId;
        Guid outcomeId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "RP");
            var operation = await SeedTerminalOperationAsync(seed, artifact.ArtifactId, ExpiredLastSeenAt);
            operationId = operation.PhotographyUploadOperationId;
            outcomeId = operation.FileOutcomes.Single().PhotographyUploadFileOutcomeId;

            var recovery = StorageOperationRecovery.Create(
                StorageOperationRecoveryType.UploadCleanup,
                artifact.ArtifactId,
                [ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/resolved.jpg")],
                "summary",
                artifactImageId: null,
                photographyUploadOperationId: operation.PhotographyUploadOperationId,
                photographyUploadFileOutcomeId: outcomeId);
            recovery.MarkRetrying(ExpiredLastSeenAt);
            recovery.MarkResolved(ExpiredLastSeenAt);
            seed.StorageOperationRecoveries.Add(recovery);
            await seed.SaveChangesAsync();
            recoveryId = recovery.StorageOperationRecoveryId;
        }

        try
        {
            await using (var cleanup = fixture.CreateContext())
            {
                var service = NewService(cleanup);
                var removed = await service.CleanupExpiredAsync();
                Assert.Equal(1, removed);
            }

            await using var reload = fixture.CreateContext();
            Assert.False(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == operationId));
            var retainedRecovery = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(candidate => candidate.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, retainedRecovery.Status);
            Assert.Equal(operationId, retainedRecovery.PhotographyUploadOperationId);
            Assert.Equal(outcomeId, retainedRecovery.PhotographyUploadFileOutcomeId);
        }
        finally
        {
            await CleanupAsync(null, recoveryId);
        }
    }

    private static PhotographyUploadIdempotencyRetentionService NewService(MuseumDbContext context) =>
        new(context, new FixedTimeProvider(Now), Options.Create(new PhotographyIdempotencyOptions { RetentionDays = RetentionDays }));

    private static async Task<bool> TryDeleteLoadedOperationAsync(PhotographyUploadIdempotencyRetentionService service, PhotographyUploadOperation operation)
    {
        var method = typeof(PhotographyUploadIdempotencyRetentionService).GetMethod(
            "TryDeleteOperationAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var task = (Task<bool>)method.Invoke(service, [operation, CancellationToken.None])!;
        return await task;
    }
    private static async Task<PhotographyUploadOperation> SeedTerminalOperationAsync(MuseumDbContext context, Guid artifactId, DateTimeOffset lastSeenAt)
    {
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifactId);
        context.PhotographyUploadOperations.Add(operation);
        var outcome = PhotographyUploadFileOutcome.Rejected(operation.PhotographyUploadOperationId, 0, "a.jpg", $"fp-{Guid.NewGuid():N}", "Unsupported file type.");
        operation.AddFileOutcome(outcome);
        context.PhotographyUploadFileOutcomes.Add(outcome);
        operation.FinalizeBatch(1);
        ForceLastSeenAt(operation, lastSeenAt);
        await context.SaveChangesAsync();
        return operation;
    }

    private static void ForceLastSeenAt(PhotographyUploadOperation operation, DateTimeOffset lastSeenAt)
    {
        var property = typeof(PhotographyUploadOperation).GetProperty(nameof(PhotographyUploadOperation.LastSeenAt), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!;
        property.SetValue(operation, lastSeenAt);
    }

    /// <summary>
    /// Removes anything this test class created that would otherwise persist in the collection's shared
    /// database and skew other test classes' global row-count assertions. Safe to call even when the
    /// retention service already removed the operation/outcome (Any row already gone is simply skipped).
    /// </summary>
    private async Task CleanupAsync(Guid? operationId, Guid? recoveryId)
    {
        await using var cleanup = fixture.CreateContext();

        if (operationId is not null)
        {
            var outcomes = await cleanup.PhotographyUploadFileOutcomes
                .Where(outcome => outcome.PhotographyUploadOperationId == operationId.Value)
                .ToListAsync();
            if (outcomes.Count > 0)
            {
                cleanup.PhotographyUploadFileOutcomes.RemoveRange(outcomes);
                await cleanup.SaveChangesAsync();
            }

            var operation = await cleanup.PhotographyUploadOperations
                .FirstOrDefaultAsync(candidate => candidate.PhotographyUploadOperationId == operationId.Value);
            if (operation is not null)
            {
                cleanup.PhotographyUploadOperations.Remove(operation);
                await cleanup.SaveChangesAsync();
            }
        }

        if (recoveryId is not null)
        {
            var recovery = await cleanup.StorageOperationRecoveries
                .FirstOrDefaultAsync(candidate => candidate.StorageOperationRecoveryId == recoveryId.Value);
            if (recovery is not null)
            {
                cleanup.StorageOperationRecoveries.Remove(recovery);
                await cleanup.SaveChangesAsync();
            }
        }
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

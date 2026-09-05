using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Photography;

/// <summary>
/// T113: PostgreSQL persistence coverage for durable storage-operation recovery rows, restart-safe retry
/// transitions, idempotency-retention blocking, and safe audit metadata. The collection shares one physical
/// database, so every test removes the rows it creates.
/// </summary>
[Collection(PostgresPhotographyCollection.Name)]
public sealed class StorageOperationRecoveryPersistenceTests(PostgresPhotographyTestFixture fixture)
{
    private static readonly DateTimeOffset RetryAt = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondRetryAt = new(2026, 9, 20, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RetentionNow = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    private const int RetentionDays = 7;

    [Fact]
    public async Task Durable_upload_cleanup_recovery_row_roundtrips_all_persisted_fields()
    {
        var rows = new CreatedRows();
        var key = Key("roundtrip-original");
        var derivativeKey = Key("roundtrip-derivative");

        try
        {
            Guid recoveryId;
            Guid operationId;
            Guid outcomeId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "SR");
                var (operation, outcome) = await SeedRecoveryNeededOperationAsync(seed, artifact.ArtifactId, rows, "roundtrip");
                operationId = operation.PhotographyUploadOperationId;
                outcomeId = outcome.PhotographyUploadFileOutcomeId;

                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [key, derivativeKey],
                    "Storage cleanup could not be completed.",
                    artifactImageId: null,
                    photographyUploadOperationId: operationId,
                    photographyUploadFileOutcomeId: outcomeId);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
            }

            await using var reload = fixture.CreateContext();
            var persisted = await reload.StorageOperationRecoveries
                .AsNoTracking()
                .SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);

            Assert.Equal(StorageOperationRecoveryType.UploadCleanup, persisted.OperationType);
            Assert.Equal(rows.ArtifactIds.Single(), persisted.ArtifactId);
            Assert.Null(persisted.ArtifactImageId);
            Assert.Equal(operationId, persisted.PhotographyUploadOperationId);
            Assert.Equal(outcomeId, persisted.PhotographyUploadFileOutcomeId);
            Assert.Equal([key, derivativeKey], persisted.ObjectKeys);
            Assert.Equal(StorageOperationRecoveryStatus.Pending, persisted.Status);
            Assert.Equal("Storage cleanup could not be completed.", persisted.FailureSummary);
            Assert.NotEqual(default, persisted.CreatedAt);
            Assert.Null(persisted.LastAttemptedAt);
            Assert.Null(persisted.ResolvedAt);
            Assert.Equal(0, persisted.ConcurrencyToken);
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    [Fact]
    public async Task Fresh_context_retry_persists_retrying_then_resolved_with_safe_audit_metadata()
    {
        var rows = new CreatedRows();
        var orphanKey = Key("restart-resolve");

        try
        {
            Guid recoveryId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "RR");
                var (seedOperation, seedOutcome) = await SeedRecoveryNeededOperationAsync(seed, artifact.ArtifactId, rows, "restart-resolve");
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [orphanKey],
                    "Storage cleanup could not be completed.",
                    artifactImageId: null,
                    photographyUploadOperationId: seedOperation.PhotographyUploadOperationId,
                    photographyUploadFileOutcomeId: seedOutcome.PhotographyUploadFileOutcomeId);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
            }

            await using (var retry = fixture.CreateContext())
            {
                var (useCase, storage) = NewUseCase(retry, RetryAt, actorUserId: "recovery-worker-postgres");
                var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));

                Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
                Assert.True(result.Succeeded);
                Assert.Equal([orphanKey], storage.DeleteObjectCalls);
            }

            await using var reload = fixture.CreateContext();
            var persisted = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, persisted.Status);
            Assert.Equal(RetryAt, persisted.LastAttemptedAt);
            Assert.Equal(RetryAt, persisted.ResolvedAt);
            Assert.Equal(2, persisted.ConcurrencyToken);

            var outcome = await reload.PhotographyUploadFileOutcomes.AsNoTracking().SingleAsync(outcome => outcome.PhotographyUploadOperationId == rows.OperationIds.Single());
            Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, outcome.Status);
            Assert.Equal("Upload could not be completed. Storage cleanup was completed safely.", outcome.StaffFacingMessage);
            var operation = await reload.PhotographyUploadOperations.AsNoTracking().SingleAsync(operation => operation.PhotographyUploadOperationId == rows.OperationIds.Single());
            Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);

            var audits = await AuditsForRecoveryAsync(reload, recoveryId);
            var retryAudit = Assert.Single(audits, audit => audit.ActionName == PhotographyAuditActions.StorageRecoveryRetry);
            AssertAuditMetadata(retryAudit, recoveryId, "recovery-worker-postgres", "Pending", "Retrying", RetryAt, resolvedAt: null, forbiddenText: orphanKey.Value);
            var resolvedAudit = Assert.Single(audits, audit => audit.ActionName == PhotographyAuditActions.StorageRecoveryResolved);
            AssertAuditMetadata(resolvedAudit, recoveryId, "recovery-worker-postgres", "Retrying", "Resolved", RetryAt, RetryAt, orphanKey.Value);
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }
    [Fact]
    public async Task Failed_needs_attention_survives_reload_and_later_retry_resolves_with_consistency_audit()
    {
        var rows = new CreatedRows();
        var orphanKey = Key("retry-after-failure");

        try
        {
            Guid recoveryId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "RF");
                var (operation, outcome) = await SeedRecoveryNeededOperationAsync(seed, artifact.ArtifactId, rows, "retry-after-failure");
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [orphanKey],
                    "Initial cleanup failure.",
                    artifactImageId: null,
                    photographyUploadOperationId: operation.PhotographyUploadOperationId,
                    photographyUploadFileOutcomeId: outcome.PhotographyUploadFileOutcomeId);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
            }

            await using (var failureContext = fixture.CreateContext())
            {
                var failingStorage = new PostgresRecoveryFakeStorage();
                failingStorage.OverrideDelete(orphanKey, ArtifactImageStorageResultKind.PermanentFailure);
                var (useCase, _) = NewUseCase(failureContext, RetryAt, failingStorage, "recovery-worker-failure");

                var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));

                Assert.Equal(StorageOperationRecoveryRetryOutcome.RetryFailed, result.Outcome);
                Assert.False(result.Succeeded);
                AssertSafeText(result.StaffFacingMessage, orphanKey.Value);
            }

            await using (var failedReload = fixture.CreateContext())
            {
                var failed = await failedReload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
                Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, failed.Status);
                Assert.Equal(RetryAt, failed.LastAttemptedAt);
                Assert.Null(failed.ResolvedAt);
                Assert.Equal("Upload cleanup retry did not confirm removal of all recorded objects.", failed.FailureSummary);
                Assert.Equal(2, failed.ConcurrencyToken);

                var consistencyAudit = Assert.Single(await AuditsForRecoveryAsync(failedReload, recoveryId), audit => audit.ActionName == PhotographyAuditActions.StorageConsistencyIssue);
                AssertAuditMetadata(consistencyAudit, recoveryId, "recovery-worker-failure", "Retrying", "FailedNeedsAttention", RetryAt, resolvedAt: null, forbiddenText: orphanKey.Value);
            }

            await using (var secondRetryContext = fixture.CreateContext())
            {
                var (useCase, _) = NewUseCase(secondRetryContext, SecondRetryAt, actorUserId: "recovery-worker-second");
                var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));

                Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
            }

            await using var reload = fixture.CreateContext();
            var resolved = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, resolved.Status);
            Assert.Equal(SecondRetryAt, resolved.LastAttemptedAt);
            Assert.Equal(SecondRetryAt, resolved.ResolvedAt);
            Assert.Equal(4, resolved.ConcurrencyToken);
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    [Fact]
    public async Task PostgreSql_concurrency_token_rejects_stale_recovery_transition_without_overwrite()
    {
        var rows = new CreatedRows();

        try
        {
            Guid recoveryId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "RC");
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [Key("stale-transition")],
                    "Pending cleanup.");
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
            }

            await using var first = fixture.CreateContext();
            await using var stale = fixture.CreateContext();
            var firstRecovery = await first.StorageOperationRecoveries.SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            var staleRecovery = await stale.StorageOperationRecoveries.SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);

            firstRecovery.MarkRetrying(RetryAt);
            await first.SaveChangesAsync();

            staleRecovery.MarkFailedNeedsAttention(SecondRetryAt, "Stale failure should not win.");
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());

            await using var reload = fixture.CreateContext();
            var authoritative = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(StorageOperationRecoveryStatus.Retrying, authoritative.Status);
            Assert.Equal(RetryAt, authoritative.LastAttemptedAt);
            Assert.Equal("Pending cleanup.", authoritative.FailureSummary);
            Assert.Equal(1, authoritative.ConcurrencyToken);
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    [Theory]
    [InlineData(StorageOperationRecoveryStatus.Pending)]
    [InlineData(StorageOperationRecoveryStatus.Retrying)]
    [InlineData(StorageOperationRecoveryStatus.FailedNeedsAttention)]
    public async Task Unresolved_linked_recovery_blocks_expired_terminal_operation_cleanup(StorageOperationRecoveryStatus recoveryStatus)
    {
        var rows = new CreatedRows();

        try
        {
            Guid operationId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, $"B{(int)recoveryStatus}");
                var operation = await SeedTerminalRejectedOperationAsync(seed, artifact.ArtifactId, rows, $"block-{recoveryStatus}", ExpiredLastSeenAt());
                operationId = operation.PhotographyUploadOperationId;
                var outcomeId = operation.FileOutcomes.Single().PhotographyUploadFileOutcomeId;
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [Key($"block-{recoveryStatus}")],
                    "Cleanup remains unresolved.",
                    artifactImageId: null,
                    photographyUploadOperationId: operationId,
                    photographyUploadFileOutcomeId: outcomeId);
                ApplyStatus(recovery, recoveryStatus, RetryAt);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
            }

            await using (var cleanup = fixture.CreateContext())
            {
                var removed = await NewRetentionService(cleanup).CleanupExpiredAsync();
                Assert.Equal(0, removed);
            }

            await using var reload = fixture.CreateContext();
            Assert.True(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == operationId));
            Assert.True(await reload.PhotographyUploadFileOutcomes.AnyAsync(outcome => outcome.PhotographyUploadOperationId == operationId));
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }
    [Fact]
    public async Task Resolved_linked_recovery_does_not_block_cleanup_and_keeps_historical_correlation()
    {
        var rows = new CreatedRows();

        try
        {
            Guid operationId;
            Guid outcomeId;
            Guid recoveryId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "RP");
                var operation = await SeedTerminalRejectedOperationAsync(seed, artifact.ArtifactId, rows, "resolved-retention", ExpiredLastSeenAt());
                operationId = operation.PhotographyUploadOperationId;
                outcomeId = operation.FileOutcomes.Single().PhotographyUploadFileOutcomeId;
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [Key("resolved-retention")],
                    "Cleanup completed.",
                    artifactImageId: null,
                    photographyUploadOperationId: operationId,
                    photographyUploadFileOutcomeId: outcomeId);
                recovery.MarkRetrying(RetryAt);
                recovery.MarkResolved(RetryAt);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
            }

            await using (var cleanup = fixture.CreateContext())
            {
                var removed = await NewRetentionService(cleanup).CleanupExpiredAsync();
                Assert.Equal(1, removed);
            }

            await using var reload = fixture.CreateContext();
            Assert.False(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == operationId));
            Assert.False(await reload.PhotographyUploadFileOutcomes.AnyAsync(outcome => outcome.PhotographyUploadOperationId == operationId));
            var recoveryHistory = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, recoveryHistory.Status);
            Assert.Equal(operationId, recoveryHistory.PhotographyUploadOperationId);
            Assert.Equal(outcomeId, recoveryHistory.PhotographyUploadFileOutcomeId);
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    [Fact]
    public async Task Retention_blocks_only_the_correlated_operation_not_other_operations_for_same_artifact()
    {
        var rows = new CreatedRows();

        try
        {
            Guid blockedOperationId;
            Guid removedOperationId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "OC");
                var blockedOperation = await SeedTerminalRejectedOperationAsync(seed, artifact.ArtifactId, rows, "blocked-same-artifact", ExpiredLastSeenAt());
                var removableOperation = await SeedTerminalRejectedOperationAsync(seed, artifact.ArtifactId, rows, "removed-same-artifact", ExpiredLastSeenAt());
                blockedOperationId = blockedOperation.PhotographyUploadOperationId;
                removedOperationId = removableOperation.PhotographyUploadOperationId;
                var blockedOutcomeId = blockedOperation.FileOutcomes.Single().PhotographyUploadFileOutcomeId;
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [Key("operation-specific")],
                    "Only one operation is blocked.",
                    artifactImageId: null,
                    photographyUploadOperationId: blockedOperationId,
                    photographyUploadFileOutcomeId: blockedOutcomeId);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
            }

            await using (var cleanup = fixture.CreateContext())
            {
                var removed = await NewRetentionService(cleanup).CleanupExpiredAsync();
                Assert.Equal(1, removed);
            }

            await using var reload = fixture.CreateContext();
            Assert.True(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == blockedOperationId));
            Assert.True(await reload.PhotographyUploadFileOutcomes.AnyAsync(outcome => outcome.PhotographyUploadOperationId == blockedOperationId));
            Assert.False(await reload.PhotographyUploadOperations.AnyAsync(operation => operation.PhotographyUploadOperationId == removedOperationId));
            Assert.False(await reload.PhotographyUploadFileOutcomes.AnyAsync(outcome => outcome.PhotographyUploadOperationId == removedOperationId));
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    [Fact]
    public async Task Recovery_needed_operation_is_not_purged_even_when_expired_with_final_outcomes()
    {
        var rows = new CreatedRows();

        try
        {
            Guid operationId;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "RN");
                var operation = await SeedTerminalRejectedOperationAsync(seed, artifact.ArtifactId, rows, "recovery-needed-terminal-only", ExpiredLastSeenAt());
                operationId = operation.PhotographyUploadOperationId;
                ForceStatus(operation, PhotographyUploadOperationStatus.RecoveryNeeded);
                await seed.SaveChangesAsync();
            }

            await using (var cleanup = fixture.CreateContext())
            {
                var removed = await NewRetentionService(cleanup).CleanupExpiredAsync();
                Assert.Equal(0, removed);
            }

            await using var reload = fixture.CreateContext();
            var retainedOperation = await reload.PhotographyUploadOperations.AsNoTracking().SingleAsync(operation => operation.PhotographyUploadOperationId == operationId);
            Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, retainedOperation.Status);
            Assert.True(await reload.PhotographyUploadFileOutcomes.AnyAsync(outcome => outcome.PhotographyUploadOperationId == operationId));
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }
    [Fact]
    public async Task Already_resolved_retry_after_restart_is_idempotent_without_storage_or_duplicate_audits()
    {
        var rows = new CreatedRows();
        var orphanKey = Key("already-resolved");

        try
        {
            Guid recoveryId;
            int originalToken;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "AR");
                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [orphanKey],
                    "Cleanup completed.");
                recovery.MarkRetrying(RetryAt);
                recovery.MarkResolved(RetryAt);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
                originalToken = recovery.ConcurrencyToken;
            }

            await using (var retry = fixture.CreateContext())
            {
                var (useCase, storage) = NewUseCase(retry, SecondRetryAt);
                var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));

                Assert.Equal(StorageOperationRecoveryRetryOutcome.AlreadyResolved, result.Outcome);
                Assert.True(result.Succeeded);
                Assert.Empty(storage.DeleteObjectCalls);
                Assert.Empty(storage.StatCalls);
            }

            await using var reload = fixture.CreateContext();
            var persisted = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, persisted.Status);
            Assert.Equal(RetryAt, persisted.LastAttemptedAt);
            Assert.Equal(RetryAt, persisted.ResolvedAt);
            Assert.Equal(originalToken, persisted.ConcurrencyToken);
            Assert.Empty(await AuditsForRecoveryAsync(reload, recoveryId));
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    [Fact]
    public async Task Correlated_upload_cleanup_after_restart_resolves_already_failed_outcome_and_terminal_operation()
    {
        var rows = new CreatedRows();
        var orphanKey = Key("already-failed-restart");

        try
        {
            Guid recoveryId;
            string originalMessage;
            await using (var seed = fixture.CreateContext())
            {
                var artifact = await SeedArtifactAsync(seed, rows, "AF");
                var (seedOperation, seedOutcome) = await SeedRecoveryNeededOperationAsync(seed, artifact.ArtifactId, rows, "already-failed");
                seedOperation.FinalizeBatch(1);
                seedOutcome.ResolveToFailed("Already failed before restart.");
                seedOperation.FinalizeBatch(1);
                originalMessage = seedOutcome.StaffFacingMessage;

                var recovery = StorageOperationRecovery.Create(
                    StorageOperationRecoveryType.UploadCleanup,
                    artifact.ArtifactId,
                    [orphanKey],
                    "Cleanup still needs confirmation.",
                    artifactImageId: null,
                    photographyUploadOperationId: seedOperation.PhotographyUploadOperationId,
                    photographyUploadFileOutcomeId: seedOutcome.PhotographyUploadFileOutcomeId);
                seed.StorageOperationRecoveries.Add(recovery);
                await seed.SaveChangesAsync();
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);
                recoveryId = recovery.StorageOperationRecoveryId;
            }

            await using (var retry = fixture.CreateContext())
            {
                var (useCase, _) = NewUseCase(retry, RetryAt);
                var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));
                Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
            }

            await using var reload = fixture.CreateContext();
            var operation = await reload.PhotographyUploadOperations.AsNoTracking().SingleAsync(operation => operation.PhotographyUploadOperationId == rows.OperationIds.Single());
            var outcome = await reload.PhotographyUploadFileOutcomes.AsNoTracking().SingleAsync(outcome => outcome.PhotographyUploadOperationId == operation.PhotographyUploadOperationId);
            var recoveryHistory = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.StorageOperationRecoveryId == recoveryId);
            Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);
            Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, outcome.Status);
            Assert.Equal(originalMessage, outcome.StaffFacingMessage);
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, recoveryHistory.Status);
        }
        finally
        {
            await CleanupAsync(rows);
        }
    }

    private static (StorageOperationRecoveryUseCase UseCase, PostgresRecoveryFakeStorage Storage) NewUseCase(
        MuseumDbContext db,
        DateTimeOffset now,
        PostgresRecoveryFakeStorage? storage = null,
        string actorUserId = "recovery-worker")
    {
        storage ??= new PostgresRecoveryFakeStorage();
        var auditWriter = new AuditWriter(db, new TestAuditActorContext(actorUserId));
        var clock = new FixedTimeProvider(now);
        var finalizationService = new ArtifactImageDeletionFinalizationService(db, auditWriter, clock);
        return (new StorageOperationRecoveryUseCase(db, storage, finalizationService, auditWriter, clock), storage);
    }

    private static PhotographyUploadIdempotencyRetentionService NewRetentionService(MuseumDbContext db) =>
        new(db, new FixedTimeProvider(RetentionNow), Options.Create(new PhotographyIdempotencyOptions { RetentionDays = RetentionDays }));

    private static async Task<Artifact> SeedArtifactAsync(MuseumDbContext db, CreatedRows rows, string prefix)
    {
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, prefix);
        rows.ArtifactIds.Add(artifact.ArtifactId);
        return artifact;
    }
    private static async Task<(PhotographyUploadOperation Operation, PhotographyUploadFileOutcome Outcome)> SeedRecoveryNeededOperationAsync(
        MuseumDbContext db,
        Guid artifactId,
        CreatedRows rows,
        string suffix)
    {
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifactId);
        db.PhotographyUploadOperations.Add(operation);
        var outcome = PhotographyUploadFileOutcome.RecoveryNeeded(
            operation.PhotographyUploadOperationId,
            0,
            $"{suffix}.jpg",
            $"fingerprint-{Guid.NewGuid():N}",
            "Recovery is required.",
            originalObjectKey: Key($"{suffix}-original"),
            derivativeObjectKeys: [Key($"{suffix}-derivative")]);
        operation.AddFileOutcome(outcome);
        db.PhotographyUploadFileOutcomes.Add(outcome);
        operation.FinalizeBatch(1);
        await db.SaveChangesAsync();
        rows.OperationIds.Add(operation.PhotographyUploadOperationId);
        return (operation, outcome);
    }

    private static async Task<PhotographyUploadOperation> SeedTerminalRejectedOperationAsync(
        MuseumDbContext db,
        Guid artifactId,
        CreatedRows rows,
        string suffix,
        DateTimeOffset lastSeenAt)
    {
        var operation = PhotographyUploadOperation.Start("photographer-1", PhotographyUploadOperationKind.CreateSetUpload, $"idem-{Guid.NewGuid():N}", $"fp-{Guid.NewGuid():N}", artifactId);
        db.PhotographyUploadOperations.Add(operation);
        var outcome = PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            0,
            $"{suffix}.jpg",
            $"fingerprint-{Guid.NewGuid():N}",
            "Unsupported file type.");
        operation.AddFileOutcome(outcome);
        db.PhotographyUploadFileOutcomes.Add(outcome);
        operation.FinalizeBatch(1);
        ForceLastSeenAt(operation, lastSeenAt);
        await db.SaveChangesAsync();
        rows.OperationIds.Add(operation.PhotographyUploadOperationId);
        return operation;
    }

    private async Task CleanupAsync(CreatedRows rows)
    {
        await using var cleanup = fixture.CreateContext();
        var recoveryEntityIds = rows.RecoveryIds.Select(id => id.ToString()).ToArray();
        if (recoveryEntityIds.Length > 0)
        {
            var audits = await cleanup.AuditEntries.Where(audit => recoveryEntityIds.Contains(audit.EntityId)).ToListAsync();
            cleanup.AuditEntries.RemoveRange(audits);
        }

        if (rows.RecoveryIds.Count > 0)
        {
            var recoveries = await cleanup.StorageOperationRecoveries.Where(recovery => rows.RecoveryIds.Contains(recovery.StorageOperationRecoveryId)).ToListAsync();
            cleanup.StorageOperationRecoveries.RemoveRange(recoveries);
        }

        if (rows.OperationIds.Count > 0)
        {
            var outcomes = await cleanup.PhotographyUploadFileOutcomes.Where(outcome => rows.OperationIds.Contains(outcome.PhotographyUploadOperationId)).ToListAsync();
            cleanup.PhotographyUploadFileOutcomes.RemoveRange(outcomes);
            var operations = await cleanup.PhotographyUploadOperations.Where(operation => rows.OperationIds.Contains(operation.PhotographyUploadOperationId)).ToListAsync();
            cleanup.PhotographyUploadOperations.RemoveRange(operations);
        }

        await cleanup.SaveChangesAsync();

        if (rows.ArtifactIds.Count == 0)
        {
            return;
        }

        var artifacts = await cleanup.Artifacts.Where(artifact => rows.ArtifactIds.Contains(artifact.ArtifactId)).ToListAsync();
        var categoryIds = artifacts.Select(artifact => artifact.CategoryId).ToArray();
        var locationIds = artifacts
            .SelectMany(artifact => new[] { artifact.CurrentLocationId, artifact.LastKnownStorageLocationId })
            .OfType<Guid>()
            .Distinct()
            .ToArray();

        cleanup.Artifacts.RemoveRange(artifacts);
        await cleanup.SaveChangesAsync();

        if (categoryIds.Length > 0)
        {
            var categories = await cleanup.ArtifactCategories.Where(category => categoryIds.Contains(category.CategoryId)).ToListAsync();
            cleanup.ArtifactCategories.RemoveRange(categories);
        }

        if (locationIds.Length > 0)
        {
            var locations = await cleanup.Locations.Where(location => locationIds.Contains(location.LocationId)).ToListAsync();
            cleanup.Locations.RemoveRange(locations);
        }

        await cleanup.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<MuseumSystem.Domain.Modules.IdentityAccess.AuditEntry>> AuditsForRecoveryAsync(MuseumDbContext db, Guid recoveryId) =>
        await db.AuditEntries
            .AsNoTracking()
            .Where(audit => audit.EntityId == recoveryId.ToString())
            .OrderBy(audit => audit.OccurredAt)
            .ToListAsync();

    private static void AssertAuditMetadata(
        MuseumSystem.Domain.Modules.IdentityAccess.AuditEntry audit,
        Guid recoveryId,
        string actorUserId,
        string previousStatus,
        string newStatus,
        DateTimeOffset attemptedAt,
        DateTimeOffset? resolvedAt,
        string forbiddenText)
    {
        Assert.Equal(actorUserId, audit.ActorUserId);
        Assert.Equal("Photography", audit.ModuleName);
        Assert.Equal(nameof(StorageOperationRecovery), audit.EntityName);
        Assert.Equal(recoveryId.ToString(), audit.EntityId);
        Assert.NotEqual(default, audit.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(audit.Summary));
        Assert.Contains("OperationType=UploadCleanup", audit.ChangeSummary);
        Assert.Contains("ArtifactImageId=<null>", audit.ChangeSummary);
        Assert.Contains($"PreviousStatus={previousStatus}", audit.ChangeSummary);
        Assert.Contains($"NewStatus={newStatus}", audit.ChangeSummary);
        Assert.Contains($"AttemptedAtUtc={attemptedAt:O}", audit.ChangeSummary);
        if (resolvedAt is not null)
        {
            Assert.Contains($"ResolvedAtUtc={resolvedAt:O}", audit.ChangeSummary);
        }

        AssertSafeText(audit.Summary, forbiddenText);
        AssertSafeText(audit.ChangeSummary ?? string.Empty, forbiddenText);
    }
    private static void ApplyStatus(StorageOperationRecovery recovery, StorageOperationRecoveryStatus status, DateTimeOffset attemptedAt)
    {
        switch (status)
        {
            case StorageOperationRecoveryStatus.Pending:
                break;
            case StorageOperationRecoveryStatus.Retrying:
                recovery.MarkRetrying(attemptedAt);
                break;
            case StorageOperationRecoveryStatus.FailedNeedsAttention:
                recovery.MarkRetrying(attemptedAt);
                recovery.MarkFailedNeedsAttention(attemptedAt, "Cleanup still needs manual attention.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Only unresolved statuses are valid for this helper.");
        }
    }

    private static void ForceStatus(PhotographyUploadOperation operation, PhotographyUploadOperationStatus status)
    {
        var statusProperty = typeof(PhotographyUploadOperation).GetProperty(nameof(PhotographyUploadOperation.Status))!;
        statusProperty.SetValue(operation, status);
        var completedAtProperty = typeof(PhotographyUploadOperation).GetProperty(nameof(PhotographyUploadOperation.CompletedAt))!;
        completedAtProperty.SetValue(operation, null);
    }
    private static void ForceLastSeenAt(PhotographyUploadOperation operation, DateTimeOffset lastSeenAt)
    {
        var property = typeof(PhotographyUploadOperation).GetProperty(nameof(PhotographyUploadOperation.LastSeenAt))!;
        property.SetValue(operation, lastSeenAt);
    }

    private static DateTimeOffset ExpiredLastSeenAt() => RetentionNow - TimeSpan.FromDays(RetentionDays) - TimeSpan.FromMinutes(1);

    private static ImageStorageObjectKey Key(string suffix) =>
        ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/{suffix}.jpg");

    private static void AssertSafeText(string text, string forbiddenText)
    {
        Assert.DoesNotContain(forbiddenText, text, StringComparison.OrdinalIgnoreCase);
        string[] forbiddenFragments = ["ObjectKey", "artifact-images/", "bucket", "endpoint", "minio", "presigned", "credential", "Exception"];
        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(fragment, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class CreatedRows
    {
        public List<Guid> ArtifactIds { get; } = [];
        public List<Guid> OperationIds { get; } = [];
        public List<Guid> RecoveryIds { get; } = [];
    }

    private sealed class TestAuditActorContext(string actorUserId) : IAuditActorContext
    {
        public AuditActor CurrentActor => new(actorUserId, actorUserId, true);
    }
}

internal sealed class PostgresRecoveryFakeStorage : IArtifactImageStorage
{
    private readonly Dictionary<string, ArtifactImageStorageResultKind> deleteOverrides = new(StringComparer.Ordinal);

    public List<ImageStorageObjectKey> StatCalls { get; } = [];
    public List<ImageStorageObjectKey> DeleteObjectCalls { get; } = [];

    public void OverrideDelete(ImageStorageObjectKey key, ArtifactImageStorageResultKind kind) => deleteOverrides[key.Value] = kind;

    public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
    {
        DeleteObjectCalls.Add(objectKey);
        if (deleteOverrides.TryGetValue(objectKey.Value, out var kind) && kind != ArtifactImageStorageResultKind.Success)
        {
            return ValueTask.FromResult(ArtifactImageStorageDeleteResult.Failed(objectKey, kind, "Simulated", "Image storage is currently unavailable."));
        }

        return ValueTask.FromResult(ArtifactImageStorageDeleteResult.Success(objectKey));
    }

    public ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
    {
        StatCalls.Add(objectKey);
        return ValueTask.FromResult(ArtifactImageStorageStatResult.Failed(ArtifactImageStorageResultKind.NotFound, "NotFound", "Stored object was not found."));
    }

    public ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Contracts;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;
using MuseumSystem.Infrastructure.Photography.Imaging;
using MuseumSystem.Infrastructure.Photography.Storage;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class StorageConsistencyRecoveryTests(PostgresPhotographyTestFixture postgres, MinioArtifactImageStorageTestFixture minio)
    : IClassFixture<MinioArtifactImageStorageTestFixture>
{
    private static readonly DateTimeOffset RetryAt = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Metadata_failure_after_real_minio_writes_cleans_all_objects_without_recovery()
    {
        var rows = new CreatedRows();
        var storage = new RecordingArtifactImageStorage(minio.CreateStorage());

        try
        {
            await using var db = postgres.CreateContext();
            var artifact = await SeedArtifactAsync(db, rows, "XCS");
            var useCase = NewCreateUseCase(new FaultingMetadataDbContext(db), storage, "photographer-t114-cleanup");

            var result = await useCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, "cleanup-success"));

            Assert.True(result.Succeeded);
            Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
            var file = Assert.Single(result.Value.FileResults);
            Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, file.Status);
            Assert.Equal("Image metadata could not be saved after storage.", file.StaffFacingMessage);
            Assert.Equal(3, storage.StoredObjectKeys.Count);
            Assert.Empty(await db.StorageOperationRecoveries.Where(recovery => recovery.ArtifactId == artifact.ArtifactId).ToListAsync());
            Assert.False(await db.ArtifactImages.AnyAsync(image => image.ArtifactId == artifact.ArtifactId && image.Status == ArtifactImageStatus.Available));

            foreach (var key in storage.StoredObjectKeys)
            {
                Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.Inner.StatAsync(key)).Kind);
            }
        }
        finally
        {
            await CleanupObjectsAsync(minio.CreateStorage(), storage.StoredObjectKeys);
            await CleanupRowsAsync(rows);
        }
    }

    [Fact]
    public async Task Metadata_failure_with_owned_minio_down_creates_recovery_then_restart_resolves_and_replay_is_idempotent()
    {
        await using var harness = await RestartableMinioHarness.StartAsync();
        var rows = new CreatedRows();
        var baseStorage = harness.CreateStorage();
        var storage = new RecordingArtifactImageStorage(baseStorage);

        try
        {
            await using (var db = postgres.CreateContext())
            {
                var artifact = await SeedArtifactAsync(db, rows, "XCR");
                var faultingDb = new FaultingMetadataDbContext(db, beforeInjectedFailureAsync: harness.StopAsync);
                var useCase = NewCreateUseCase(faultingDb, storage, "photographer-t114-recovery");

                var result = await useCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, "cleanup-recovery"));

                Assert.True(result.Succeeded);
                Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
                var file = Assert.Single(result.Value.FileResults);
                Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, file.Status);
                AssertSafeText(file.StaffFacingMessage, harness.Options.Endpoint, harness.Options.BucketName, harness.Options.AccessKey, harness.Options.SecretKey);

                var operation = await db.PhotographyUploadOperations.AsNoTracking().SingleAsync(operation => operation.PhotographyUploadOperationId == result.Value.PhotographyUploadOperationId);
                rows.OperationIds.Add(operation.PhotographyUploadOperationId);
                var outcome = await db.PhotographyUploadFileOutcomes.AsNoTracking().SingleAsync(outcome => outcome.PhotographyUploadOperationId == operation.PhotographyUploadOperationId);
                var recovery = await db.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.ArtifactId == artifact.ArtifactId);
                rows.RecoveryIds.Add(recovery.StorageOperationRecoveryId);

                Assert.Equal(StorageOperationRecoveryType.UploadCleanup, recovery.OperationType);
                Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
                Assert.Equal(artifact.ArtifactId, recovery.ArtifactId);
                Assert.Equal(operation.PhotographyUploadOperationId, recovery.PhotographyUploadOperationId);
                Assert.Equal(outcome.PhotographyUploadFileOutcomeId, recovery.PhotographyUploadFileOutcomeId);
                Assert.Equal(storage.StoredObjectKeys, recovery.ObjectKeys);
                Assert.Null(recovery.ArtifactImageId);
                Assert.False(await db.ArtifactImages.AnyAsync(image => image.ArtifactId == artifact.ArtifactId && image.Status == ArtifactImageStatus.Available));
                AssertSafeText(recovery.FailureSummary, harness.Options.Endpoint, harness.Options.BucketName, harness.Options.AccessKey, harness.Options.SecretKey);
            }

            await harness.RestartAsync();
            foreach (var key in storage.StoredObjectKeys)
            {
                Assert.True((await baseStorage.StatAsync(key)).Exists);
            }

            Guid recoveryId;
            await using (var retryDb = postgres.CreateContext())
            {
                var recovery = await retryDb.StorageOperationRecoveries.AsNoTracking().SingleAsync(id => rows.RecoveryIds.Contains(id.StorageOperationRecoveryId));
                recoveryId = recovery.StorageOperationRecoveryId;

                var retry = await NewRecoveryUseCase(retryDb, baseStorage, "recovery-worker-t114")
                    .RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));

                Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, retry.Outcome);
                Assert.True(retry.Succeeded);
                AssertSafeText(retry.StaffFacingMessage, harness.Options.Endpoint, harness.Options.BucketName, harness.Options.AccessKey, harness.Options.SecretKey);
            }

            foreach (var key in storage.StoredObjectKeys)
            {
                Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await baseStorage.StatAsync(key)).Kind);
            }

            await using (var reload = postgres.CreateContext())
            {
                var recovery = await reload.StorageOperationRecoveries.AsNoTracking().SingleAsync(candidate => rows.RecoveryIds.Contains(candidate.StorageOperationRecoveryId));
                var operation = await reload.PhotographyUploadOperations.AsNoTracking().SingleAsync(operation => operation.PhotographyUploadOperationId == recovery.PhotographyUploadOperationId);
                var outcome = await reload.PhotographyUploadFileOutcomes.AsNoTracking().SingleAsync(outcome => outcome.PhotographyUploadFileOutcomeId == recovery.PhotographyUploadFileOutcomeId);

                Assert.Equal(StorageOperationRecoveryStatus.Resolved, recovery.Status);
                Assert.Equal(RetryAt, recovery.LastAttemptedAt);
                Assert.Equal(RetryAt, recovery.ResolvedAt);
                Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);
                Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, outcome.Status);
                Assert.Equal("Upload could not be completed. Storage cleanup was completed safely.", outcome.StaffFacingMessage);
                Assert.Empty(await reload.ArtifactImages.Where(image => image.ArtifactId == recovery.ArtifactId).ToListAsync());

                var audits = await AuditsForRecoveryAsync(reload, recovery.StorageOperationRecoveryId);
                Assert.Contains(audits, audit => audit.ActionName == PhotographyAuditActions.StorageRecoveryRetry);
                Assert.Contains(audits, audit => audit.ActionName == PhotographyAuditActions.StorageRecoveryResolved);
                foreach (var audit in audits)
                {
                    AssertSafeText(audit.Summary, harness.Options.Endpoint, harness.Options.BucketName, harness.Options.AccessKey, harness.Options.SecretKey);
                    AssertSafeText(audit.ChangeSummary, [.. storage.StoredObjectKeys.Select(key => key.Value), harness.Options.Endpoint, harness.Options.BucketName, harness.Options.AccessKey, harness.Options.SecretKey]);
                }
            }

            await using (var replayDb = postgres.CreateContext())
            {
                var replay = await NewRecoveryUseCase(replayDb, baseStorage, "recovery-worker-t114-replay")
                    .RetryAsync(new StorageOperationRecoveryRetryCommand(recoveryId));
                Assert.Equal(StorageOperationRecoveryRetryOutcome.AlreadyResolved, replay.Outcome);
                Assert.True(replay.Succeeded);
            }
        }
        finally
        {
            await CleanupRowsAsync(rows);
        }
    }

    [Fact]
    public async Task Same_storage_instance_reports_retryable_while_owned_minio_is_stopped_and_succeeds_after_restart()
    {
        await using var harness = await RestartableMinioHarness.StartAsync();
        var storage = harness.CreateStorage();
        var key = ImageStorageObjectKey.Create(harness.CreateObjectKey("restart/original.jpg"));
        var bytes = PhotographyIntegrationTestImages.Jpeg(320, 240);

        var write = await storage.StoreOriginalAsync(key, PhotographyIntegrationTestImages.Stream(bytes), "image/jpeg", bytes.LongLength, "sha256-restart-proof");
        Assert.True(write.Succeeded);
        Assert.True((await storage.StatAsync(key)).Exists);

        await harness.StopAsync();
        var stopped = await storage.StatAsync(key);

        Assert.Equal(ArtifactImageStorageResultKind.RetryableFailure, stopped.Kind);
        Assert.NotNull(stopped.Failure);
        AssertSafeFailure(stopped.Failure!, key, harness.Options.Endpoint, harness.Options.BucketName, harness.Options.AccessKey, harness.Options.SecretKey);
        Assert.NotEqual(ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured, stopped.Kind);

        await harness.RestartAsync();
        Assert.True((await storage.StatAsync(key)).Exists);
    }

    [Fact]
    public async Task Disappearing_real_object_before_verification_is_not_accepted_as_metadata()
    {
        var rows = new CreatedRows();
        var storage = new RecordingArtifactImageStorage(minio.CreateStorage()) { DeleteOriginalBeforeFirstVerificationStat = true };

        try
        {
            await using var db = postgres.CreateContext();
            var artifact = await SeedArtifactAsync(db, rows, "XMD");
            var useCase = NewCreateUseCase(db, storage, "photographer-t114-missing");

            var result = await useCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, "missing-before-verification"));

            Assert.True(result.Succeeded);
            Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
            var file = Assert.Single(result.Value.FileResults);
            Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, file.Status);
            Assert.Equal("Stored image object was not found.", file.StaffFacingMessage);
            Assert.True(storage.DeletedObjectBeforeVerification);
            Assert.False(await db.ArtifactImages.AnyAsync(image => image.ArtifactId == artifact.ArtifactId && image.Status == ArtifactImageStatus.Available));
            Assert.Empty(await db.StorageOperationRecoveries.Where(recovery => recovery.ArtifactId == artifact.ArtifactId).ToListAsync());

            foreach (var key in storage.StoredObjectKeys)
            {
                Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.Inner.StatAsync(key)).Kind);
            }
        }
        finally
        {
            await CleanupObjectsAsync(minio.CreateStorage(), storage.StoredObjectKeys);
            await CleanupRowsAsync(rows);
        }
    }

    [Fact]
    public async Task Real_storage_surface_maps_missing_object_and_missing_bucket_to_structured_results()
    {
        var storage = minio.CreateStorage();
        var missingKey = ImageStorageObjectKey.Create(minio.CreateObjectKey("missing/direct-stat.jpg"));

        var missing = await storage.StatAsync(missingKey);

        Assert.Equal(ArtifactImageStorageResultKind.NotFound, missing.Kind);
        Assert.NotNull(missing.Failure);
        AssertSafeFailure(missing.Failure!, missingKey, minio.Options.Endpoint, minio.Options.BucketName, minio.Options.AccessKey, minio.Options.SecretKey);

        var missingBucketOptions = new MinioArtifactImageStorageOptions
        {
            Provider = "Minio",
            Endpoint = minio.Options.Endpoint,
            BucketName = $"{minio.Options.BucketName}-missing-{Guid.NewGuid():N}",
            AccessKey = minio.Options.AccessKey,
            SecretKey = minio.Options.SecretKey,
            Region = minio.Options.Region,
            UseTls = minio.Options.UseTls,
            RequestTimeoutSeconds = minio.Options.RequestTimeoutSeconds
        };
        var missingBucketStorage = new MinioArtifactImageStorage(Options.Create(missingBucketOptions));
        var missingBucketKey = ImageStorageObjectKey.Create("artifact-images/integration/missing-bucket-config.jpg");
        var missingBucket = await missingBucketStorage.StatAsync(missingBucketKey);

        Assert.Equal(ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured, missingBucket.Kind);
        Assert.NotNull(missingBucket.Failure);
        AssertSafeFailure(
            missingBucket.Failure!,
            missingBucketKey,
            minio.Options.Endpoint,
            minio.Options.BucketName,
            missingBucketOptions.BucketName,
            minio.Options.AccessKey,
            minio.Options.SecretKey);
    }

    private static CreatePhotographySetWithImagesCommand CreateCommand(Guid artifactId, string suffix)
    {
        var bytes = PhotographyIntegrationTestImages.Jpeg(640, 480);
        return new CreatePhotographySetWithImagesCommand(
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 9, 30),
            "photographer-t114",
            $"t114-{suffix}-{Guid.NewGuid():N}",
            [PhotographyIntegrationTestImages.UploadFile(0, $"{suffix}.jpg", bytes)]);
    }

    private static CreatePhotographySetWithImagesUseCase NewCreateUseCase(IMuseumDbContext dbContext, IArtifactImageStorage storage, string actorUserId)
    {
        var persistence = new PhotographyUploadPersistenceService(dbContext);
        var imageProcessor = new ArtifactImageProcessor(Options.Create(new ArtifactImageProcessingOptions()));
        var fingerprint = new PhotographyUploadFingerprintService();
        var auditWriter = new AuditWriter(dbContext, new TestAuditActorContext(actorUserId));
        var auditService = new PhotographyUploadAuditService(auditWriter);
        var consistency = new PhotographyUploadConsistencyService(
            persistence,
            storage,
            new PhotographyObjectKeyFactory(),
            new ArtifactImageStorageHealthService(),
            auditService);

        return new CreatePhotographySetWithImagesUseCase(
            persistence,
            imageProcessor,
            fingerprint,
            consistency,
            new PhotographyResponseMapper(),
            new TestAuditActorContext(actorUserId));
    }

    private static StorageOperationRecoveryUseCase NewRecoveryUseCase(MuseumDbContext db, IArtifactImageStorage storage, string actorUserId)
    {
        var auditWriter = new AuditWriter(db, new TestAuditActorContext(actorUserId));
        var clock = new FixedTimeProvider(RetryAt);
        var finalizationService = new ArtifactImageDeletionFinalizationService(db, auditWriter, clock);
        return new StorageOperationRecoveryUseCase(db, storage, finalizationService, auditWriter, clock);
    }

    private async Task<Artifact> SeedArtifactAsync(MuseumDbContext db, CreatedRows rows, string prefix)
    {
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, prefix);
        rows.ArtifactIds.Add(artifact.ArtifactId);
        return artifact;
    }

    private async Task CleanupRowsAsync(CreatedRows rows)
    {
        if (rows.ArtifactIds.Count == 0)
        {
            return;
        }

        await using var db = postgres.CreateContext();
        var operationIds = await db.PhotographyUploadOperations
            .Where(operation => rows.ArtifactIds.Contains(operation.ArtifactId) || rows.OperationIds.Contains(operation.PhotographyUploadOperationId))
            .Select(operation => operation.PhotographyUploadOperationId)
            .ToListAsync();
        var outcomeIds = operationIds.Count == 0
            ? []
            : await db.PhotographyUploadFileOutcomes
                .Where(outcome => operationIds.Contains(outcome.PhotographyUploadOperationId))
                .Select(outcome => outcome.PhotographyUploadFileOutcomeId)
                .ToListAsync();
        var recoveryIds = await db.StorageOperationRecoveries
            .Where(recovery => rows.ArtifactIds.Contains(recovery.ArtifactId) || rows.RecoveryIds.Contains(recovery.StorageOperationRecoveryId))
            .Select(recovery => recovery.StorageOperationRecoveryId)
            .ToListAsync();
        var imageIds = await db.ArtifactImages
            .Where(image => rows.ArtifactIds.Contains(image.ArtifactId))
            .Select(image => image.ArtifactImageId)
            .ToListAsync();
        var setIds = await db.PhotographySets
            .Where(set => rows.ArtifactIds.Contains(set.ArtifactId))
            .Select(set => set.PhotographySetId)
            .ToListAsync();

        var auditEntityIds = rows.ArtifactIds
            .Concat(operationIds)
            .Concat(outcomeIds)
            .Concat(recoveryIds)
            .Concat(imageIds)
            .Concat(setIds)
            .Select(id => id.ToString())
            .ToArray();
        if (auditEntityIds.Length > 0)
        {
            db.AuditEntries.RemoveRange(await db.AuditEntries.Where(audit => auditEntityIds.Contains(audit.EntityId)).ToListAsync());
        }

        if (recoveryIds.Count > 0)
        {
            db.StorageOperationRecoveries.RemoveRange(await db.StorageOperationRecoveries.Where(recovery => recoveryIds.Contains(recovery.StorageOperationRecoveryId)).ToListAsync());
        }

        if (operationIds.Count > 0)
        {
            db.PhotographyUploadFileOutcomes.RemoveRange(await db.PhotographyUploadFileOutcomes.Where(outcome => operationIds.Contains(outcome.PhotographyUploadOperationId)).ToListAsync());
            db.PhotographyUploadOperations.RemoveRange(await db.PhotographyUploadOperations.Where(operation => operationIds.Contains(operation.PhotographyUploadOperationId)).ToListAsync());
        }

        if (imageIds.Count > 0)
        {
            db.ArtifactImageDerivatives.RemoveRange(await db.ArtifactImageDerivatives.Where(derivative => imageIds.Contains(derivative.ArtifactImageId)).ToListAsync());
            db.ArtifactImages.RemoveRange(await db.ArtifactImages.Where(image => imageIds.Contains(image.ArtifactImageId)).ToListAsync());
        }

        if (setIds.Count > 0)
        {
            db.PhotographySets.RemoveRange(await db.PhotographySets.Where(set => setIds.Contains(set.PhotographySetId)).ToListAsync());
        }

        await db.SaveChangesAsync();

        var artifacts = await db.Artifacts.Where(artifact => rows.ArtifactIds.Contains(artifact.ArtifactId)).ToListAsync();
        var categoryIds = artifacts.Select(artifact => artifact.CategoryId).Distinct().ToArray();
        var locationIds = artifacts
            .SelectMany(artifact => new[] { artifact.CurrentLocationId, artifact.LastKnownStorageLocationId })
            .OfType<Guid>()
            .Distinct()
            .ToArray();

        db.Artifacts.RemoveRange(artifacts);
        await db.SaveChangesAsync();

        if (categoryIds.Length > 0)
        {
            db.ArtifactCategories.RemoveRange(await db.ArtifactCategories.Where(category => categoryIds.Contains(category.CategoryId)).ToListAsync());
        }

        if (locationIds.Length > 0)
        {
            db.Locations.RemoveRange(await db.Locations.Where(location => locationIds.Contains(location.LocationId)).ToListAsync());
        }

        await db.SaveChangesAsync();
    }

    private static async Task CleanupObjectsAsync(IArtifactImageStorage storage, IReadOnlyCollection<ImageStorageObjectKey> keys)
    {
        foreach (var key in keys.Distinct())
        {
            await storage.DeleteObjectAsync(key);
        }
    }

    private static async Task<IReadOnlyList<AuditEntry>> AuditsForRecoveryAsync(MuseumDbContext db, Guid recoveryId) =>
        await db.AuditEntries
            .AsNoTracking()
            .Where(audit => audit.EntityId == recoveryId.ToString())
            .OrderBy(audit => audit.OccurredAt)
            .ToListAsync();

    private static void AssertSafeFailure(ArtifactImageStorageFailure failure, ImageStorageObjectKey key, params string?[] forbiddenValues)
    {
        AssertSafeText(failure.StaffFacingMessage, [key.Value, .. forbiddenValues]);
        AssertSafeText(failure.OperationalSummary ?? string.Empty, [key.Value, .. forbiddenValues]);
    }

    private static void AssertSafeText(string? value, params string?[] forbiddenValues)
    {
        var text = value ?? string.Empty;
        string[] forbiddenFragments = ["MinIO", "minio", "artifact-images/", "AccessKey", "SecretKey", "credential", "C:\\", "/data/", "Exception", "Connection refused"];
        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(fragment, text, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var forbiddenValue in forbiddenValues.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            Assert.DoesNotContain(forbiddenValue!, text, StringComparison.OrdinalIgnoreCase);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FaultingMetadataDbContext(MuseumDbContext inner, Func<Task>? beforeInjectedFailureAsync = null) : IMuseumDbContext
    {
        private bool failureInjected;

        public DbSet<ArtifactCategory> ArtifactCategories => inner.ArtifactCategories;
        public DbSet<Artifact> Artifacts => inner.Artifacts;
        public DbSet<Location> Locations => inner.Locations;
        public DbSet<MovementRecord> MovementRecords => inner.MovementRecords;
        public DbSet<ImportBatch> ImportBatches => inner.ImportBatches;
        public DbSet<ImportRow> ImportRows => inner.ImportRows;
        public DbSet<ReconciliationSession> ReconciliationSessions => inner.ReconciliationSessions;
        public DbSet<ReconciliationResult> ReconciliationResults => inner.ReconciliationResults;
        public DbSet<DocumentedCorrection> DocumentedCorrections => inner.DocumentedCorrections;
        public DbSet<AuditEntry> AuditEntries => inner.AuditEntries;
        public DbSet<DocumentationTemplate> DocumentationTemplates => inner.DocumentationTemplates;
        public DbSet<DocumentationTemplateVersion> DocumentationTemplateVersions => inner.DocumentationTemplateVersions;
        public DbSet<DocumentationTemplateField> DocumentationTemplateFields => inner.DocumentationTemplateFields;
        public DbSet<DocumentationTemplateFieldOption> DocumentationTemplateFieldOptions => inner.DocumentationTemplateFieldOptions;
        public DbSet<DocumentationRecord> DocumentationRecords => inner.DocumentationRecords;
        public DbSet<DocumentationRevision> DocumentationRevisions => inner.DocumentationRevisions;
        public DbSet<PhotographyRequest> PhotographyRequests => inner.PhotographyRequests;
        public DbSet<PhotographySet> PhotographySets => inner.PhotographySets;
        public DbSet<ArtifactImage> ArtifactImages => inner.ArtifactImages;
        public DbSet<ArtifactImageDerivative> ArtifactImageDerivatives => inner.ArtifactImageDerivatives;
        public DbSet<ArtifactPhotographyState> ArtifactPhotographyStates => inner.ArtifactPhotographyStates;
        public DbSet<PhotographyUploadOperation> PhotographyUploadOperations => inner.PhotographyUploadOperations;
        public DbSet<PhotographyUploadFileOutcome> PhotographyUploadFileOutcomes => inner.PhotographyUploadFileOutcomes;
        public DbSet<StorageOperationRecovery> StorageOperationRecoveries => inner.StorageOperationRecoveries;

        public Task<IMuseumDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            inner.BeginTransactionAsync(cancellationToken);

        public void ClearTrackedChanges() => inner.ClearTrackedChanges();

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!failureInjected && IsSuccessfulImageMetadataBoundary())
            {
                failureInjected = true;
                if (beforeInjectedFailureAsync is not null)
                {
                    await beforeInjectedFailureAsync();
                }

                throw new DbUpdateException("Injected metadata persistence failure after storage verification.");
            }

            return await inner.SaveChangesAsync(cancellationToken);
        }

        private bool IsSuccessfulImageMetadataBoundary() =>
            inner.ChangeTracker.Entries<ArtifactImage>().Any(entry => entry.State == EntityState.Added)
            && inner.ChangeTracker.Entries<ArtifactImageDerivative>().Any(entry => entry.State == EntityState.Added)
            && inner.ChangeTracker.Entries<PhotographyUploadFileOutcome>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
    }

    private sealed class RecordingArtifactImageStorage(IArtifactImageStorage inner) : IArtifactImageStorage
    {
        private bool verificationDeletionAttempted;

        public IArtifactImageStorage Inner => inner;
        public List<ImageStorageObjectKey> OriginalKeys { get; } = [];
        public List<ImageStorageObjectKey> DerivativeKeys { get; } = [];
        public IReadOnlyList<ImageStorageObjectKey> StoredObjectKeys => [.. OriginalKeys, .. DerivativeKeys];
        public bool DeleteOriginalBeforeFirstVerificationStat { get; init; }
        public bool DeletedObjectBeforeVerification { get; private set; }

        public async ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default)
        {
            var result = await inner.StoreOriginalAsync(objectKey, content, contentType, lengthBytes, checksum, cancellationToken);
            if (result.Succeeded)
            {
                OriginalKeys.Add(objectKey);
            }

            return result;
        }

        public async ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default)
        {
            var result = await inner.StoreDerivativeAsync(objectKey, content, contentType, lengthBytes, derivativeKind, checksum, cancellationToken);
            if (result.Succeeded)
            {
                DerivativeKeys.Add(objectKey);
            }

            return result;
        }

        public async ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
        {
            if (DeleteOriginalBeforeFirstVerificationStat
                && !verificationDeletionAttempted
                && OriginalKeys.Contains(objectKey)
                && DerivativeKeys.Count >= 2)
            {
                verificationDeletionAttempted = true;
                var delete = await inner.DeleteObjectAsync(objectKey, cancellationToken);
                DeletedObjectBeforeVerification = delete.Kind is ArtifactImageStorageResultKind.Success or ArtifactImageStorageResultKind.NotFound;
            }

            return await inner.StatAsync(objectKey, cancellationToken);
        }

        public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
            inner.OpenReadAsync(objectKey, cancellationToken);

        public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
            inner.CreateShortLivedReadAccessAsync(objectKey, requestedLifetime, cancellationToken);

        public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
            inner.DeleteObjectAsync(objectKey, cancellationToken);

        public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, CancellationToken cancellationToken = default) =>
            inner.DeleteImageObjectsAsync(originalObjectKey, derivativeObjectKeys, cancellationToken);
    }

    private sealed class RestartableMinioHarness : IAsyncDisposable
    {
        private const int MinioPort = 9000;
        private const string AccessKey = "minioadmin";
        private const string SecretKey = "minioadmin";
        private readonly List<string> objectKeys = [];
        private readonly IContainer container;
        private bool isRunning;
        private IMinioClient adminClient = null!;

        private RestartableMinioHarness(IContainer container, MinioArtifactImageStorageOptions options)
        {
            this.container = container;
            Options = options;
        }

        public MinioArtifactImageStorageOptions Options { get; }

        public static async Task<RestartableMinioHarness> StartAsync()
        {
            var hostPort = GetFreeTcpPort();
            var container = new ContainerBuilder("minio/minio:latest")
                .WithEnvironment("MINIO_ROOT_USER", AccessKey)
                .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
                .WithPortBinding(hostPort, MinioPort)
                .WithCommand("server", "/data", "--address", ":9000")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(MinioPort))
                .Build();

            await container.StartAsync();
            var options = new MinioArtifactImageStorageOptions
            {
                Provider = "Minio",
                Endpoint = $"http://localhost:{hostPort}",
                BucketName = $"museum-feature003-t114-{Guid.NewGuid():N}",
                AccessKey = AccessKey,
                SecretKey = SecretKey,
                Region = "us-east-1",
                UseTls = false,
                RequestTimeoutSeconds = 1
            };

            var harness = new RestartableMinioHarness(container, options) { isRunning = true };
            await harness.EnsureBucketAsync();
            return harness;
        }

        public MinioArtifactImageStorage CreateStorage() => new(Microsoft.Extensions.Options.Options.Create(Options));

        public string CreateObjectKey(string suffix)
        {
            var key = $"artifact-images/integration/{Guid.NewGuid():N}/{suffix}";
            objectKeys.Add(key);
            return key;
        }

        public async Task StopAsync()
        {
            if (!isRunning)
            {
                return;
            }

            await container.StopAsync();
            isRunning = false;
        }

        public async Task RestartAsync()
        {
            if (!isRunning)
            {
                await container.StartAsync();
                isRunning = true;
            }

            await EnsureBucketAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!isRunning)
                {
                    await RestartAsync();
                }
            }
            finally
            {
                await container.DisposeAsync();
            }
        }

        private async Task EnsureBucketAsync()
        {
            adminClient = new MinioClient()
                .WithEndpoint(NormalizeEndpoint(Options.Endpoint))
                .WithCredentials(Options.AccessKey, Options.SecretKey)
                .WithSSL(Options.UseTls)
                .WithTimeout(1000)
                .Build();

            Exception? lastException = null;
            for (var attempt = 1; attempt <= 20; attempt++)
            {
                try
                {
                    var exists = await adminClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(Options.BucketName));
                    if (!exists)
                    {
                        await adminClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(Options.BucketName));
                    }

                    return;
                }
                catch (Exception ex) when (attempt < 20)
                {
                    lastException = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(250));
                }
            }

            throw new InvalidOperationException("Owned MinIO test container did not become ready.", lastException);
        }

        private static int GetFreeTcpPort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            }

            return endpoint;
        }
    }
}

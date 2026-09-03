using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class PhotographyUploadRecoveryUseCaseTests
{
    [Fact]
    public async Task Metadata_commit_failure_after_object_writes_cleans_up_and_records_failed_outcome()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var faultingContext = new SequencedSuccessfulFileMetadataFaultingDbContext(db, failOnSuccessfulMetadataSave: 1);
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "recovery-cleanup-success-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, Assert.Single(result.Value.FileResults).Status);
        var expectedKeys = StoredKeys(storage);
        Assert.Equal(expectedKeys, Assert.Single(storage.DeleteImageObjectCalls));
        Assert.All(expectedKeys, key => Assert.Contains(key, storage.StatCalls));
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        Assert.Equal(0, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
        Assert.Equal(0, await db.StorageOperationRecoveries.CountAsync());
        Assert.DoesNotContain(await db.PhotographyUploadFileOutcomes.ToListAsync(), outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
    }

    [Fact]
    public async Task Metadata_commit_failure_with_cleanup_failure_creates_durable_recovery()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var faultingContext = new SequencedSuccessfulFileMetadataFaultingDbContext(db, failOnSuccessfulMetadataSave: 1);
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "recovery-cleanup-failure-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
        var outcome = await db.PhotographyUploadFileOutcomes.SingleAsync();
        var expectedKeys = StoredKeys(storage);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, outcome.Status);
        Assert.Equal(expectedKeys[0], outcome.OriginalObjectKey);
        Assert.Equal(expectedKeys.Skip(1), outcome.DerivativeObjectKeys);
        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryType.UploadCleanup, recovery.OperationType);
        Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
        Assert.Equal(artifact.ArtifactId, recovery.ArtifactId);
        Assert.Equal(expectedKeys, recovery.ObjectKeys);
        Assert.Equal(expectedKeys, Assert.Single(storage.DeleteImageObjectCalls));
        Assert.False(await db.ArtifactImages.AnyAsync(image => image.Status == ArtifactImageStatus.Available));
    }

    [Fact]
    public async Task Partial_cleanup_failure_after_metadata_failure_records_recovery_for_all_stored_keys()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var faultingContext = new SequencedSuccessfulFileMetadataFaultingDbContext(db, failOnSuccessfulMetadataSave: 1);
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        storage.CleanupFailureIndexes.Add(2);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "partial-cleanup-failure-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
        var cleanupAttemptedKeys = StoredKeys(storage);
        var unresolvedKey = cleanupAttemptedKeys[2];
        Assert.Equal(cleanupAttemptedKeys, Assert.Single(storage.DeleteImageObjectCalls));
        Assert.Equal([unresolvedKey], (await db.StorageOperationRecoveries.SingleAsync()).ObjectKeys);
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        var outcome = await db.PhotographyUploadFileOutcomes.SingleAsync();
        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, outcome.Status);
        Assert.Null(outcome.OriginalObjectKey);
        Assert.Equal([unresolvedKey], outcome.DerivativeObjectKeys);
    }

    [Fact]
    public async Task Earlier_file_success_survives_later_metadata_failure_without_custody_mutation()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        var beforeStatus = artifact.CurrentStatus;
        var beforeLocationId = artifact.CurrentLocationId;
        var beforeHolderType = artifact.CurrentHolderType;
        var beforeHolderName = artifact.CurrentHolderName;
        await db.SaveChangesAsync();
        var movementCount = await db.MovementRecords.CountAsync();
        var locationCount = await db.Locations.CountAsync();
        var faultingContext = new SequencedSuccessfulFileMetadataFaultingDbContext(db, failOnSuccessfulMetadataSave: 2);
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "side.jpg", [4, 5, 6])
            ],
            idempotencyKey: "later-metadata-failure-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Collection(result.Value.FileResults.OrderBy(file => file.ClientFileOrdinal),
            file => Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, file.Status),
            file => Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, file.Status));
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Equal(2, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(2, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(movementCount, await db.MovementRecords.CountAsync());
        Assert.Equal(locationCount, await db.Locations.CountAsync());
        Assert.Equal(beforeStatus, artifact.CurrentStatus);
        Assert.Equal(beforeLocationId, artifact.CurrentLocationId);
        Assert.Equal(beforeHolderType, artifact.CurrentHolderType);
        Assert.Equal(beforeHolderName, artifact.CurrentHolderName);
    }

    [Fact]
    public async Task Terminal_replay_after_failure_does_not_duplicate_successful_metadata_or_storage_identity()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var faultingContext = new SequencedSuccessfulFileMetadataFaultingDbContext(db, failOnSuccessfulMetadataSave: 2);
        var firstStorage = new FakeArtifactImageStorage();
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: firstStorage, persistenceContext: faultingContext);
        var command = CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "side.jpg", [4, 5, 6])
            ],
            idempotencyKey: "replay-after-metadata-failure-key");

        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(command);
        var image = await db.ArtifactImages.SingleAsync();
        var successfulOutcome = await db.PhotographyUploadFileOutcomes.SingleAsync(outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: replayStorage);

        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "side.jpg", [4, 5, 6])
            ],
            idempotencyKey: "replay-after-metadata-failure-key"));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Value!.PhotographyUploadOperationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Equal(2, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(image.ArtifactImageId, successfulOutcome.ArtifactImageId);
        Assert.Equal(image.OriginalObjectKey, successfulOutcome.OriginalObjectKey);
    }

    private static ImageStorageObjectKey[] StoredKeys(FakeArtifactImageStorage storage) =>
    [
        storage.StoreOriginalCalls.Single().ObjectKey,
        storage.StoreDerivativeCalls[0].ObjectKey,
        storage.StoreDerivativeCalls[1].ObjectKey
    ];
}

internal sealed class SequencedSuccessfulFileMetadataFaultingDbContext(MuseumDbContext inner, int failOnSuccessfulMetadataSave) : IMuseumDbContext
{
    private int successfulMetadataSaveAttempts;

    public int SuccessfulFileMetadataFailuresThrown { get; private set; }
    public int ClearTrackedChangesCalls { get; private set; }

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

    public void ClearTrackedChanges()
    {
        ClearTrackedChangesCalls++;
        inner.ClearTrackedChanges();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var hasAddedImage = inner.ChangeTracker.Entries<ArtifactImage>()
            .Any(entry => entry.State == EntityState.Added);
        var hasAddedSucceededOutcome = inner.ChangeTracker.Entries<PhotographyUploadFileOutcome>()
            .Any(entry => entry.State == EntityState.Added && entry.Entity.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        if (hasAddedImage && hasAddedSucceededOutcome)
        {
            successfulMetadataSaveAttempts++;
            if (successfulMetadataSaveAttempts == failOnSuccessfulMetadataSave)
            {
                SuccessfulFileMetadataFailuresThrown++;
                throw new DbUpdateException("Simulated successful file metadata persistence failure.");
            }
        }

        return inner.SaveChangesAsync(cancellationToken);
    }
}

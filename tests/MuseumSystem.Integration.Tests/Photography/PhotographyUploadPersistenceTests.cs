using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
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

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyUploadPersistenceTests(PostgresPhotographyTestFixture postgres, MinioArtifactImageStorageTestFixture minio)
    : IClassFixture<MinioArtifactImageStorageTestFixture>, IAsyncLifetime
{
    private readonly List<Guid> createdArtifactIds = [];
    [Fact]
    public async Task Successful_file_metadata_survives_later_rejection_and_round_trips_with_derivatives()
    {
        await using var db = postgres.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, "PE");
        createdArtifactIds.Add(artifact.ArtifactId);
        var host = CreateHost(db, minio.CreateStorage());

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            PhotographyIntegrationTestImages.UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480)),
            PhotographyIntegrationTestImages.UploadFile(1, "notes.jpg", "not image content"u8.ToArray())
        ], idempotencyKey: "partial-rejection"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Rejected], result.Value.FileResults.Select(file => file.Status).ToArray());
        Assert.DoesNotContain(result.Value.FileResults, file => file.StaffFacingMessage.Contains("artifact-images", StringComparison.OrdinalIgnoreCase));

        await using var verification = postgres.CreateContext();
        var set = await verification.PhotographySets.SingleAsync(set => set.ArtifactId == artifact.ArtifactId);
        var image = await verification.ArtifactImages.Include(artifactImage => artifactImage.Derivatives).SingleAsync(artifactImage => artifactImage.ArtifactId == artifact.ArtifactId);
        var operation = await verification.PhotographyUploadOperations.Include(upload => upload.FileOutcomes).SingleAsync(upload => upload.PhotographyUploadOperationId == result.Value.PhotographyUploadOperationId);

        Assert.Equal(set.PhotographySetId, operation.PhotographySetId);
        Assert.Equal(artifact.ArtifactId, image.ArtifactId);
        Assert.Equal(set.PhotographySetId, image.PhotographySetId);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(640, image.PixelWidth);
        Assert.Equal(480, image.PixelHeight);
        Assert.Equal(2, image.Derivatives.Count);
        Assert.Equal([ImageDerivativeKind.Thumbnail, ImageDerivativeKind.Preview], image.Derivatives.Select(derivative => derivative.Kind).Order().ToArray());
        Assert.Equal(2, operation.FileOutcomes.Count);
        Assert.Single(operation.FileOutcomes, outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        Assert.Single(operation.FileOutcomes, outcome => outcome.Status == PhotographyUploadFileOutcomeStatus.Rejected && outcome.ArtifactImageId is null);
    }

    [Fact]
    public async Task Later_storage_failure_does_not_roll_back_previously_committed_success()
    {
        await using var db = postgres.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, "SF");
        createdArtifactIds.Add(artifact.ArtifactId);
        var storage = new FailingOriginalWriteStorage(minio.CreateStorage(), failOriginalCallOrdinal: 1);
        var host = CreateHost(db, storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            PhotographyIntegrationTestImages.UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480)),
            PhotographyIntegrationTestImages.UploadFile(1, "side.jpg", PhotographyIntegrationTestImages.Jpeg(320, 240))
        ], idempotencyKey: "later-storage-failure"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Failed], result.Value.FileResults.Select(file => file.Status).ToArray());

        await using var verification = postgres.CreateContext();
        Assert.Equal(1, await verification.PhotographySets.CountAsync(set => set.ArtifactId == artifact.ArtifactId));
        Assert.Equal(1, await verification.ArtifactImages.CountAsync(image => image.ArtifactId == artifact.ArtifactId));
        var outcomes = await verification.PhotographyUploadFileOutcomes.Where(outcome => outcome.PhotographyUploadOperationId == result.Value.PhotographyUploadOperationId).OrderBy(outcome => outcome.ClientFileOrdinal).ToListAsync();
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Failed], outcomes.Select(outcome => outcome.Status).ToArray());
        Assert.NotNull(outcomes[0].ArtifactImageId);
        Assert.Null(outcomes[1].ArtifactImageId);
    }

    [Fact]
    public async Task Later_database_failure_after_storage_cleanup_does_not_roll_back_previously_committed_success()
    {
        await using var db = postgres.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, "DF");
        createdArtifactIds.Add(artifact.ArtifactId);
        var faultingContext = new FaultingMuseumDbContext(db, "db-fail.jpg");
        var host = CreateHost(db, minio.CreateStorage(), faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            PhotographyIntegrationTestImages.UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480)),
            PhotographyIntegrationTestImages.UploadFile(1, "db-fail.jpg", PhotographyIntegrationTestImages.Jpeg(320, 240))
        ], idempotencyKey: "later-database-failure"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Equal(1, faultingContext.MetadataFailuresThrown);

        await using var verification = postgres.CreateContext();
        Assert.Equal(1, await verification.PhotographySets.CountAsync(set => set.ArtifactId == artifact.ArtifactId));
        Assert.Equal(1, await verification.ArtifactImages.CountAsync(image => image.ArtifactId == artifact.ArtifactId));
        Assert.Equal(2, await verification.PhotographyUploadFileOutcomes.CountAsync(outcome => outcome.PhotographyUploadOperationId == result.Value.PhotographyUploadOperationId));
        Assert.Equal(0, await verification.StorageOperationRecoveries.CountAsync());
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Failed],
            await verification.PhotographyUploadFileOutcomes.Where(outcome => outcome.PhotographyUploadOperationId == result.Value.PhotographyUploadOperationId).OrderBy(outcome => outcome.ClientFileOrdinal).Select(outcome => outcome.Status).ToArrayAsync());
    }

    [Fact]
    public async Task All_invalid_upload_persists_authoritative_outcomes_without_usable_set_or_image_rows()
    {
        await using var db = postgres.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, "AI");
        createdArtifactIds.Add(artifact.ArtifactId);
        var host = CreateHost(db, minio.CreateStorage());

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            PhotographyIntegrationTestImages.UploadFile(0, "bad.jpg", "not an image"u8.ToArray()),
            PhotographyIntegrationTestImages.UploadFile(1, "bad.png", [0, 1, 2, 3])
        ], idempotencyKey: "all-invalid"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
        Assert.Null(result.Value.PhotographySetId);
        Assert.Null(result.Value.PhotographySet);
        Assert.All(result.Value.FileResults, file => Assert.Equal(PhotographyUploadFileOutcomeStatus.Rejected, file.Status));

        await using var verification = postgres.CreateContext();
        Assert.Equal(0, await verification.PhotographySets.CountAsync(set => set.ArtifactId == artifact.ArtifactId));
        Assert.Equal(0, await verification.ArtifactImages.CountAsync(image => image.ArtifactId == artifact.ArtifactId));
        Assert.Equal(2, await verification.PhotographyUploadFileOutcomes.CountAsync(outcome => outcome.PhotographyUploadOperationId == result.Value.PhotographyUploadOperationId));
    }

    [Fact]
    public async Task Idempotent_replay_uses_persisted_authoritative_per_file_outcomes()
    {
        await using var db = postgres.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(db, "IR");
        createdArtifactIds.Add(artifact.ArtifactId);
        var host = CreateHost(db, minio.CreateStorage());
        var files = new[]
        {
            PhotographyIntegrationTestImages.UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480)),
            PhotographyIntegrationTestImages.UploadFile(1, "bad.png", "not an image"u8.ToArray())
        };

        var first = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, files, idempotencyKey: "replay-key"));
        var replay = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            PhotographyIntegrationTestImages.UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480)),
            PhotographyIntegrationTestImages.UploadFile(1, "bad.png", "not an image"u8.ToArray())
        ], idempotencyKey: "replay-key"));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Value!.PhotographyUploadOperationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Equal(first.Value.FileResults.Select(file => file.Status), replay.Value.FileResults.Select(file => file.Status));
        Assert.Equal(first.Value.FileResults.Single(file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded).ArtifactImageId,
            replay.Value.FileResults.Single(file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded).ArtifactImageId);

        await using var verification = postgres.CreateContext();
        Assert.Equal(1, await verification.PhotographySets.CountAsync(set => set.ArtifactId == artifact.ArtifactId));
        Assert.Equal(1, await verification.ArtifactImages.CountAsync(image => image.ArtifactId == artifact.ArtifactId));
        Assert.Equal(2, await verification.PhotographyUploadFileOutcomes.CountAsync(outcome => outcome.PhotographyUploadOperationId == first.Value.PhotographyUploadOperationId));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (createdArtifactIds.Count == 0)
        {
            return;
        }

        await using var cleanup = postgres.CreateContext();
        var operations = await cleanup.PhotographyUploadOperations
            .Include(operation => operation.FileOutcomes)
            .Where(operation => createdArtifactIds.Contains(operation.ArtifactId))
            .ToListAsync();
        cleanup.PhotographyUploadFileOutcomes.RemoveRange(operations.SelectMany(operation => operation.FileOutcomes));
        cleanup.PhotographyUploadOperations.RemoveRange(operations);

        var images = await cleanup.ArtifactImages
            .Include(image => image.Derivatives)
            .Where(image => createdArtifactIds.Contains(image.ArtifactId))
            .ToListAsync();
        var recoveries = await cleanup.StorageOperationRecoveries
            .Where(recovery => createdArtifactIds.Contains(recovery.ArtifactId))
            .ToListAsync();

        var storage = minio.CreateStorage();
        foreach (var image in images)
        {
            await storage.DeleteImageObjectsAsync(image.OriginalObjectKey, image.Derivatives.Select(derivative => derivative.ObjectKey).ToArray());
        }

        foreach (var recovery in recoveries.Where(recovery => recovery.ObjectKeys.Count > 0))
        {
            await storage.DeleteImageObjectsAsync(recovery.ObjectKeys.First(), recovery.ObjectKeys.Skip(1).ToArray());
        }

        cleanup.ArtifactImageDerivatives.RemoveRange(images.SelectMany(image => image.Derivatives));
        cleanup.ArtifactImages.RemoveRange(images);
        cleanup.StorageOperationRecoveries.RemoveRange(recoveries);
        cleanup.PhotographySets.RemoveRange(await cleanup.PhotographySets
            .Where(set => createdArtifactIds.Contains(set.ArtifactId))
            .ToListAsync());
        await cleanup.SaveChangesAsync();
    }
    private static PhotographyUploadPersistenceHost CreateHost(
        MuseumDbContext db,
        IArtifactImageStorage storage,
        IMuseumDbContext? persistenceContext = null)
    {
        var persistence = new PhotographyUploadPersistenceService(persistenceContext ?? db);
        var processor = new ArtifactImageProcessor(Options.Create(new ArtifactImageProcessingOptions
        {
            MaximumOriginalBytes = 20 * 1024 * 1024,
            Thumbnail = new DerivativeOptions(320, 320, 82),
            Preview = new DerivativeOptions(1600, 1600, 86)
        }));
        var fingerprint = new PhotographyUploadFingerprintService();
        var objectKeys = new PhotographyObjectKeyFactory();
        var audit = new PhotographyUploadAuditService(new RecordingAuditWriter());
        var consistency = new PhotographyUploadConsistencyService(persistence, storage, objectKeys, new ArtifactImageStorageHealthService(), audit);
        var mapper = new PhotographyResponseMapper();
        var actor = new TestAuditActorContext("photographer-1");

        return new PhotographyUploadPersistenceHost(
            new CreatePhotographySetWithImagesUseCase(persistence, processor, fingerprint, consistency, mapper, actor));
    }

    private static CreatePhotographySetWithImagesCommand CreateCommand(
        Guid artifactId,
        IReadOnlyList<PhotographyUploadFileInput> files,
        string idempotencyKey) =>
        new(
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 26),
            "photographer-1",
            idempotencyKey,
            files);
}

internal sealed record PhotographyUploadPersistenceHost(CreatePhotographySetWithImagesUseCase CreateUseCase);

internal sealed class FailingOriginalWriteStorage(IArtifactImageStorage inner, int failOriginalCallOrdinal) : IArtifactImageStorage
{
    private int originalWriteCalls;

    public async ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default)
    {
        var ordinal = originalWriteCalls++;
        if (ordinal == failOriginalCallOrdinal)
        {
            return ArtifactImageStorageWriteResult.Failed(ArtifactImageStorageResultKind.RetryableFailure, "Storage.InjectedFailure", "Image storage is currently unavailable.");
        }

        return await inner.StoreOriginalAsync(objectKey, content, contentType, lengthBytes, checksum, cancellationToken);
    }

    public ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default) =>
        inner.StoreDerivativeAsync(objectKey, content, contentType, lengthBytes, derivativeKind, checksum, cancellationToken);

    public ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        inner.StatAsync(objectKey, cancellationToken);

    public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        inner.OpenReadAsync(objectKey, cancellationToken);

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
        inner.CreateShortLivedReadAccessAsync(objectKey, requestedLifetime, cancellationToken);

    public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        inner.DeleteObjectAsync(objectKey, cancellationToken);

    public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, CancellationToken cancellationToken = default) =>
        inner.DeleteImageObjectsAsync(originalObjectKey, derivativeObjectKeys, cancellationToken);
}

internal sealed class FaultingMuseumDbContext(MuseumDbContext inner, string filenameToFail) : IMuseumDbContext
{
    public int MetadataFailuresThrown { get; private set; }
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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (inner.ChangeTracker.Entries<ArtifactImage>().Any(IsFailingImage))
        {
            MetadataFailuresThrown++;
            throw new DbUpdateException("Injected metadata persistence failure for integration coverage.");
        }

        return inner.SaveChangesAsync(cancellationToken);
    }

    private bool IsFailingImage(EntityEntry<ArtifactImage> entry) =>
        entry.State == EntityState.Added
        && string.Equals(entry.Entity.OriginalFilename, filenameToFail, StringComparison.OrdinalIgnoreCase);
}

internal sealed class RecordingAuditWriter : IAuditWriter
{
    public List<AuditWriteRequest> Requests { get; } = [];

    public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }
}

internal sealed class TestAuditActorContext(string userId) : IAuditActorContext
{
    public AuditActor CurrentActor => new(userId, "Photography integration user", true);
}

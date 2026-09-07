using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyCustodyBoundaryRegressionTests(PostgresPhotographyTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UploadedAt = new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
    private readonly List<Guid> createdArtifactIds = [];
    private readonly List<Guid> createdCategoryIds = [];
    private readonly List<Guid> createdLocationIds = [];

    [Fact]
    public async Task Creating_photography_set_preserves_artifact_identity_and_does_not_duplicate_artifact()
    {
        var artifactId = await SeedInStorageArtifactAsync("CI");

        Feature001Snapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, artifactId);
        }

        var (setId, _) = await PersistPhotographySetWithImageAsync(artifactId, "identity-preservation");

        Feature001Snapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadFeature001SnapshotAsync(afterContext, artifactId);
            var persistedSet = await afterContext.PhotographySets.AsNoTracking().SingleAsync(set => set.PhotographySetId == setId);

            Assert.Equal(artifactId, persistedSet.ArtifactId);
        }

        AssertFeature001StateUnchanged(before, after);
        Assert.Equal(1, after.ArtifactRowsByArtifactId);
        Assert.Equal(1, after.ArtifactRowsByMuseumNumberDisplay);
    }

    [Fact]
    public async Task Photography_metadata_preserves_current_custody_location_and_holder_fields()
    {
        var artifactId = await SeedOutOfStorageArtifactWithDeliveryAsync("CH");

        Feature001Snapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, artifactId);
        }

        await PersistPhotographySetWithImageAsync(artifactId, "custody-preservation");

        Feature001Snapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadFeature001SnapshotAsync(afterContext, artifactId);
        }

        AssertFeature001StateUnchanged(before, after);
        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, after.CurrentStatus);
        Assert.Null(after.CurrentLocationId);
        Assert.Equal(MovementRecipientType.LaboratoryDivision.ToString(), after.CurrentHolderType);
        Assert.Equal("Laboratory conservation bench", after.CurrentHolderName);
    }

    [Fact]
    public async Task Photography_metadata_preserves_movement_history_without_generating_photography_movement()
    {
        var artifactId = await SeedOutOfStorageArtifactWithDeliveryAsync("MH");

        Feature001Snapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, artifactId);
        }

        await PersistPhotographySetWithImageAsync(artifactId, "movement-preservation");

        Feature001Snapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadFeature001SnapshotAsync(afterContext, artifactId);
        }

        AssertFeature001StateUnchanged(before, after);
        Assert.Single(after.Movements);
        Assert.DoesNotContain(after.Movements, movement => movement.MovementType == MovementType.Return);
        Assert.DoesNotContain(after.Movements, movement => movement.RecipientType == MovementRecipientType.Photographer);
    }

    [Fact]
    public async Task Photography_metadata_does_not_reopen_or_rewrite_returned_storehouse_state()
    {
        var seeded = await SeedReturnedArtifactWithDeliveryAndReturnAsync("RS");

        Feature001Snapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, seeded.ArtifactId);
        }

        await PersistPhotographySetWithImageAsync(seeded.ArtifactId, "returned-state-preservation");

        Feature001Snapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadFeature001SnapshotAsync(afterContext, seeded.ArtifactId);
        }

        AssertFeature001StateUnchanged(before, after);
        Assert.Equal(ArtifactCurrentStatus.InStorage, after.CurrentStatus);
        Assert.Equal(seeded.ReturnStorageLocationId, after.CurrentLocationId);
        Assert.Null(after.CurrentHolderType);
        Assert.Null(after.CurrentHolderName);
        Assert.Equal(2, after.Movements.Count);
        Assert.Single(after.Movements, movement => movement.MovementType == MovementType.Return);
    }

    [Fact]
    public async Task Pending_photography_request_references_existing_artifact_without_changing_feature001_state()
    {
        var artifactId = await SeedOutOfStorageArtifactWithDeliveryAsync("RQ");

        Feature001Snapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, artifactId);
        }

        Guid requestId;
        await using (var actionContext = fixture.CreateContext())
        {
            var useCase = new CreatePhotographyRequestUseCase(
                actionContext,
                new TestAuditActorContext("requester-1"),
                new StaticPermissionChecker([PermissionNames.PhotographyRequest]),
                new RecordingAuditWriter(),
                new FixedTimeProvider(RequestedAt));

            var result = await useCase.CreatePhotographyRequest(new CreatePhotographyRequestCommand(
                artifactId,
                PhotographyPurpose.PreMaintenance));

            Assert.True(result.Succeeded);
            requestId = result.Value!.PhotographyRequestId;
        }

        Feature001Snapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadFeature001SnapshotAsync(afterContext, artifactId);
            var request = await afterContext.PhotographyRequests.AsNoTracking().SingleAsync(row => row.PhotographyRequestId == requestId);

            Assert.Equal(artifactId, request.ArtifactId);
            Assert.Equal(PhotographyRequestStatus.Pending, request.Status);
            Assert.Equal(PhotographyPurpose.PreMaintenance, request.Purpose);
            Assert.Equal("requester-1", request.RequestedByUserId);
            Assert.Equal(RequestedAt, request.RequestedAt);
        }

        AssertFeature001StateUnchanged(before, after);
    }

    [Fact]
    public async Task Setting_primary_image_references_existing_image_without_changing_feature001_state_or_movement_history()
    {
        var artifactId = await SeedOutOfStorageArtifactWithDeliveryAsync("PI");
        var (_, imageId) = await PersistPhotographySetWithImageAsync(artifactId, "primary-boundary");

        Feature001Snapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, artifactId);
        }

        await using (var actionContext = fixture.CreateContext())
        {
            var actor = new TestAuditActorContext("manager-1");
            var stateService = new ArtifactPhotographyStateService(actionContext);
            var useCase = new SetPrimaryArtifactImageUseCase(
                actionContext,
                actor,
                new StaticPermissionChecker([PermissionNames.PhotographyManage]),
                new RecordingAuditWriter(),
                stateService,
                new FixedTimeProvider(UploadedAt));

            var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
                artifactId,
                imageId,
                ExpectedConcurrencyToken: 0));

            Assert.True(result.Succeeded);
        }

        Feature001Snapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadFeature001SnapshotAsync(afterContext, artifactId);
            var state = await afterContext.ArtifactPhotographyStates.AsNoTracking().SingleAsync(row => row.ArtifactId == artifactId);

            Assert.Equal(imageId, state.PrimaryImageId);
        }

        AssertFeature001StateUnchanged(before, after);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (createdArtifactIds.Count == 0)
        {
            return;
        }

        await using var cleanup = fixture.CreateContext();
        var images = await cleanup.ArtifactImages
            .Include(image => image.Derivatives)
            .Where(image => createdArtifactIds.Contains(image.ArtifactId))
            .ToListAsync();

        cleanup.PhotographyRequests.RemoveRange(await cleanup.PhotographyRequests
            .Where(request => createdArtifactIds.Contains(request.ArtifactId))
            .ToListAsync());
        cleanup.ArtifactPhotographyStates.RemoveRange(await cleanup.ArtifactPhotographyStates
            .Where(state => createdArtifactIds.Contains(state.ArtifactId))
            .ToListAsync());
        cleanup.ArtifactImageDerivatives.RemoveRange(images.SelectMany(image => image.Derivatives));
        cleanup.ArtifactImages.RemoveRange(images);
        cleanup.PhotographySets.RemoveRange(await cleanup.PhotographySets
            .Where(set => createdArtifactIds.Contains(set.ArtifactId))
            .ToListAsync());
        cleanup.MovementRecords.RemoveRange(await cleanup.MovementRecords
            .Where(record => createdArtifactIds.Contains(record.ArtifactId))
            .ToListAsync());
        cleanup.Artifacts.RemoveRange(await cleanup.Artifacts
            .Where(artifact => createdArtifactIds.Contains(artifact.ArtifactId))
            .ToListAsync());
        cleanup.ArtifactCategories.RemoveRange(await cleanup.ArtifactCategories
            .Where(category => createdCategoryIds.Contains(category.CategoryId))
            .ToListAsync());
        cleanup.Locations.RemoveRange(await cleanup.Locations
            .Where(location => createdLocationIds.Contains(location.LocationId))
            .ToListAsync());

        await cleanup.SaveChangesAsync();
    }

    private async Task<Guid> SeedInStorageArtifactAsync(string prefix)
    {
        await using var context = fixture.CreateContext();
        var (category, storage) = CreateCategoryAndStorage(prefix);
        var artifact = Artifact.Create(category, 1, $"{prefix} photography boundary artifact", storage);

        context.ArtifactCategories.Add(category);
        context.Locations.Add(storage);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();
        Track(category, storage, artifact);

        return artifact.ArtifactId;
    }

    private async Task<Guid> SeedOutOfStorageArtifactWithDeliveryAsync(string prefix)
    {
        await using var context = fixture.CreateContext();
        var (category, storage) = CreateCategoryAndStorage(prefix);
        var artifact = Artifact.Create(category, 1, $"{prefix} delivered artifact", storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Laboratory conservation bench");
        var movement = MovementRecord.CreateDelivery(
            Guid.NewGuid(),
            artifact,
            MovementRecipientType.LaboratoryDivision,
            "Laboratory conservation bench",
            "Condition review before display",
            "Boundary regression delivery",
            "registrar-1");

        context.ArtifactCategories.Add(category);
        context.Locations.Add(storage);
        context.Artifacts.Add(artifact);
        context.MovementRecords.Add(movement);
        await context.SaveChangesAsync();
        Track(category, storage, artifact);

        return artifact.ArtifactId;
    }

    private async Task<ReturnedArtifactSeed> SeedReturnedArtifactWithDeliveryAndReturnAsync(string prefix)
    {
        await using var context = fixture.CreateContext();
        var category = ArtifactCategory.Create($"{prefix}{Guid.NewGuid():N}"[..8], $"{prefix} category");
        var originalStorage = Location.Create($"{prefix} original storage {Guid.NewGuid():N}", LocationType.Storage);
        var returnStorage = Location.Create($"{prefix} return storage {Guid.NewGuid():N}", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, $"{prefix} returned artifact", originalStorage);
        var movementGroupId = Guid.NewGuid();

        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Laboratory conservation bench");
        var delivery = MovementRecord.CreateDelivery(
            movementGroupId,
            artifact,
            MovementRecipientType.LaboratoryDivision,
            "Laboratory conservation bench",
            "Condition review before return",
            "Boundary regression delivery",
            "registrar-1");
        artifact.ReturnToStorage(returnStorage);
        var returned = MovementRecord.CreateReturn(
            movementGroupId,
            artifact,
            returnStorage,
            "Boundary regression return",
            "registrar-2");

        context.ArtifactCategories.Add(category);
        context.Locations.AddRange(originalStorage, returnStorage);
        context.Artifacts.Add(artifact);
        context.MovementRecords.AddRange(delivery, returned);
        await context.SaveChangesAsync();
        Track(category, originalStorage, artifact);
        createdLocationIds.Add(returnStorage.LocationId);

        return new ReturnedArtifactSeed(artifact.ArtifactId, returnStorage.LocationId);
    }

    private async Task<(Guid SetId, Guid ImageId)> PersistPhotographySetWithImageAsync(Guid artifactId, string objectKeyPrefix)
    {
        await using var context = fixture.CreateContext();
        var set = PhotographySet.Create(
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 27),
            "photographer-1",
            "photographer-1");
        var image = ArtifactImage.Create(
            artifactId,
            set.PhotographySetId,
            ImageStorageObjectKey.Create($"artifact-images/{objectKeyPrefix}-{Guid.NewGuid():N}/original.jpg"),
            "front.jpg",
            "image/jpeg",
            2048,
            1200,
            900,
            "photographer-1",
            UploadedAt);

        context.PhotographySets.Add(set);
        context.ArtifactImages.Add(image);
        await context.SaveChangesAsync();

        return (set.PhotographySetId, image.ArtifactImageId);
    }

    private static async Task<Feature001Snapshot> LoadFeature001SnapshotAsync(MuseumDbContext context, Guid artifactId)
    {
        var artifact = await context.Artifacts.AsNoTracking().SingleAsync(row => row.ArtifactId == artifactId);
        var movements = await context.MovementRecords
            .AsNoTracking()
            .Where(record => record.ArtifactId == artifactId)
            .OrderBy(record => record.MovementId)
            .Select(record => new MovementSnapshot(
                record.MovementId,
                record.MovementType,
                record.MovementGroupId,
                record.ArtifactId,
                record.RecipientType,
                record.RecipientName,
                record.Purpose,
                record.ReturnLocationId,
                record.Note,
                record.OccurredAt,
                record.RecordedBy))
            .ToListAsync();

        return new Feature001Snapshot(
            artifact.ArtifactId,
            artifact.MuseumNumberDisplay,
            artifact.CategoryId,
            artifact.CurrentStatus,
            artifact.CurrentLocationId,
            artifact.CurrentHolderType,
            artifact.CurrentHolderName,
            artifact.LastKnownStorageLocationId,
            movements,
            await context.Artifacts.CountAsync(row => row.ArtifactId == artifact.ArtifactId),
            await context.Artifacts.CountAsync(row => row.MuseumNumberDisplay == artifact.MuseumNumberDisplay));
    }

    private static void AssertFeature001StateUnchanged(Feature001Snapshot before, Feature001Snapshot after)
    {
        Assert.Equal(before.ArtifactId, after.ArtifactId);
        Assert.Equal(before.MuseumNumberDisplay, after.MuseumNumberDisplay);
        Assert.Equal(before.CategoryId, after.CategoryId);
        Assert.Equal(before.CurrentStatus, after.CurrentStatus);
        Assert.Equal(before.CurrentLocationId, after.CurrentLocationId);
        Assert.Equal(before.CurrentHolderType, after.CurrentHolderType);
        Assert.Equal(before.CurrentHolderName, after.CurrentHolderName);
        Assert.Equal(before.LastKnownStorageLocationId, after.LastKnownStorageLocationId);
        Assert.Equal(before.Movements, after.Movements);
        Assert.Equal(before.ArtifactRowsByArtifactId, after.ArtifactRowsByArtifactId);
        Assert.Equal(before.ArtifactRowsByMuseumNumberDisplay, after.ArtifactRowsByMuseumNumberDisplay);
    }

    private (ArtifactCategory Category, Location Storage) CreateCategoryAndStorage(string prefix)
    {
        var category = ArtifactCategory.Create($"{prefix}{Guid.NewGuid():N}"[..8], $"{prefix} category");
        var storage = Location.Create($"{prefix} storage {Guid.NewGuid():N}", LocationType.Storage);

        return (category, storage);
    }

    private void Track(ArtifactCategory category, Location location, Artifact artifact)
    {
        createdCategoryIds.Add(category.CategoryId);
        createdLocationIds.Add(location.LocationId);
        createdArtifactIds.Add(artifact.ArtifactId);
    }

    private sealed class StaticPermissionChecker(IReadOnlyCollection<string> permissions) : ICurrentActorPermissionChecker
    {
        private readonly HashSet<string> permissions = new(permissions, StringComparer.Ordinal);

        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record ReturnedArtifactSeed(Guid ArtifactId, Guid ReturnStorageLocationId);

    private sealed record Feature001Snapshot(
        Guid ArtifactId,
        string MuseumNumberDisplay,
        Guid CategoryId,
        ArtifactCurrentStatus CurrentStatus,
        Guid? CurrentLocationId,
        string? CurrentHolderType,
        string? CurrentHolderName,
        Guid? LastKnownStorageLocationId,
        IReadOnlyList<MovementSnapshot> Movements,
        int ArtifactRowsByArtifactId,
        int ArtifactRowsByMuseumNumberDisplay);

    private sealed record MovementSnapshot(
        Guid MovementId,
        MovementType MovementType,
        Guid MovementGroupId,
        Guid ArtifactId,
        MovementRecipientType? RecipientType,
        string? RecipientName,
        string? Purpose,
        Guid? ReturnLocationId,
        string? Note,
        DateTimeOffset OccurredAt,
        string? RecordedBy);
}

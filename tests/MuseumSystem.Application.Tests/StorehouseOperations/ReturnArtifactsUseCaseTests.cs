using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.StorehouseOperations;

public sealed class ReturnArtifactsUseCaseTests
{
    [Fact]
    public async Task Return_updates_current_location_and_last_known_storage_location()
    {
        await using var db = CreateDbContext();
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var returnStorage = Location.Create("Shelf B", LocationType.Storage);
        var category = ArtifactCategory.Create("ARC", "Archive");
        var artifact = Artifact.Create(category, 1, "Artifact", storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.Photographer, "Studio");
        db.ArtifactCategories.Add(category);
        db.Locations.AddRange(storage, returnStorage);
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();

        var result = await new ReturnArtifactsUseCase(db).ReturnArtifacts(new ReturnArtifactsRequest([artifact.ArtifactId], returnStorage.LocationId));

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactCurrentStatus.InStorage, artifact.CurrentStatus);
        Assert.Equal(returnStorage.LocationId, artifact.CurrentLocationId);
        Assert.Equal(returnStorage.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Null(artifact.CurrentHolderType);
        Assert.Null(artifact.CurrentHolderName);
        Assert.Single(db.MovementRecords);
    }

    [Fact]
    public async Task Return_rejects_non_storage_location_without_changing_artifacts()
    {
        await using var db = CreateDbContext();
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var displayHall = Location.Create("Hall 1", LocationType.DisplayHall);
        var category = ArtifactCategory.Create("ARC", "Archive");
        var artifact = Artifact.Create(category, 1, "Artifact", storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab A");
        db.ArtifactCategories.Add(category);
        db.Locations.AddRange(storage, displayHall);
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();

        var result = await new ReturnArtifactsUseCase(db).ReturnArtifacts(new ReturnArtifactsRequest([artifact.ArtifactId], displayHall.LocationId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Return.LocationInvalid");
        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Null(artifact.CurrentLocationId);
        Assert.Equal(storage.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Empty(db.MovementRecords);
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

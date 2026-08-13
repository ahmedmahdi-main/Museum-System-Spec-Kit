using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.StorehouseOperations;

public sealed class DeliverArtifactsUseCaseTests
{
    [Fact]
    public async Task Bulk_delivery_rejects_entire_operation_when_any_artifact_is_ineligible()
    {
        await using var db = CreateDbContext();
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var category = ArtifactCategory.Create("ARC", "Archive");
        var eligible = Artifact.Create(category, 1, "Eligible artifact", storage);
        var ineligible = Artifact.Create(category, 2, "Already delivered", storage);
        ineligible.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Documentation");
        db.ArtifactCategories.Add(category);
        db.Locations.Add(storage);
        db.Artifacts.AddRange(eligible, ineligible);
        await db.SaveChangesAsync();

        var result = await new DeliverArtifactsUseCase(db).DeliverArtifacts(new DeliverArtifactsRequest(
            [eligible.ArtifactId, ineligible.ArtifactId],
            MovementRecipientType.LaboratoryDivision,
            "Lab A",
            null,
            "Study"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Delivery.Ineligible");
        Assert.Equal(ArtifactCurrentStatus.InStorage, eligible.CurrentStatus);
        Assert.Equal(storage.LocationId, eligible.CurrentLocationId);
        Assert.Null(eligible.CurrentHolderName);
        Assert.Empty(db.MovementRecords);
    }

    [Fact]
    public async Task Display_hall_delivery_sets_location_and_holder_to_display_hall()
    {
        await using var db = CreateDbContext();
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var displayHall = Location.Create("Hall 1", LocationType.DisplayHall);
        var category = ArtifactCategory.Create("ARC", "Archive");
        var artifact = Artifact.Create(category, 1, "Artifact", storage);
        db.ArtifactCategories.Add(category);
        db.Locations.AddRange(storage, displayHall);
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync();

        var result = await new DeliverArtifactsUseCase(db).DeliverArtifacts(new DeliverArtifactsRequest(
            [artifact.ArtifactId],
            MovementRecipientType.DisplayHall,
            null,
            displayHall.LocationId,
            "Display"));

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Equal(displayHall.LocationId, artifact.CurrentLocationId);
        Assert.Equal(MovementRecipientType.DisplayHall.ToString(), artifact.CurrentHolderType);
        Assert.Equal(displayHall.NameArabic, artifact.CurrentHolderName);
        Assert.Equal(storage.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Single(db.MovementRecords);
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

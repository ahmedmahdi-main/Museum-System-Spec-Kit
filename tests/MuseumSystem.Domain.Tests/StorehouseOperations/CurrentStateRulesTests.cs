using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Tests.StorehouseOperations;

public sealed class CurrentStateRulesTests
{
    [Fact]
    public void In_storage_artifact_keeps_storage_location_without_holder()
    {
        var artifact = CreateArtifact(out var storage);

        Assert.Equal(ArtifactCurrentStatus.InStorage, artifact.CurrentStatus);
        Assert.Equal(storage.LocationId, artifact.CurrentLocationId);
        Assert.Null(artifact.CurrentHolderType);
        Assert.Null(artifact.CurrentHolderName);
        Assert.Equal(storage.LocationId, artifact.LastKnownStorageLocationId);
    }

    [Theory]
    [InlineData(MovementRecipientType.DocumentationDivision)]
    [InlineData(MovementRecipientType.LaboratoryDivision)]
    [InlineData(MovementRecipientType.Photographer)]
    public void Internal_holder_delivery_clears_current_location_and_preserves_last_storage(MovementRecipientType recipientType)
    {
        var artifact = CreateArtifact(out var storage);

        artifact.DeliverToInternalHolder(recipientType, "Recipient");

        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Null(artifact.CurrentLocationId);
        Assert.Equal(recipientType.ToString(), artifact.CurrentHolderType);
        Assert.Equal("Recipient", artifact.CurrentHolderName);
        Assert.Equal(storage.LocationId, artifact.LastKnownStorageLocationId);
    }

    [Fact]
    public void Display_hall_delivery_sets_current_location_and_display_holder()
    {
        var artifact = CreateArtifact(out var storage);
        var displayHall = Location.Create("Display Hall 1", LocationType.DisplayHall);

        artifact.DeliverToDisplayHall(displayHall);

        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Equal(displayHall.LocationId, artifact.CurrentLocationId);
        Assert.Equal(MovementRecipientType.DisplayHall.ToString(), artifact.CurrentHolderType);
        Assert.Equal(displayHall.NameArabic, artifact.CurrentHolderName);
        Assert.Equal(storage.LocationId, artifact.LastKnownStorageLocationId);
    }

    [Fact]
    public void Return_requires_valid_storage_location()
    {
        var artifact = CreateArtifact(out _);
        artifact.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Documentation");
        var displayHall = Location.Create("Display Hall 1", LocationType.DisplayHall);

        Assert.Throws<InvalidOperationException>(() => artifact.ReturnToStorage(displayHall));
    }

    private static Artifact CreateArtifact(out Location storage)
    {
        var category = ArtifactCategory.Create("ARC", "Archive");
        storage = Location.Create("Shelf A", LocationType.Storage);
        return Artifact.Create(category, 10, "Basic description", storage);
    }
}

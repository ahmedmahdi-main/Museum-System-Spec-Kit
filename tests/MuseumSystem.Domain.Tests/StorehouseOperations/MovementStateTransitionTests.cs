using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Tests.StorehouseOperations;

public sealed class MovementStateTransitionTests
{
    [Fact]
    public void Deliver_to_internal_holder_moves_artifact_out_of_storage()
    {
        var artifact = CreateArtifact(out var storage);

        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab A");

        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Null(artifact.CurrentLocationId);
        Assert.Equal(MovementRecipientType.LaboratoryDivision.ToString(), artifact.CurrentHolderType);
        Assert.Equal("Lab A", artifact.CurrentHolderName);
        Assert.Equal(storage.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Equal(1, artifact.ConcurrencyToken);
    }

    [Fact]
    public void Return_to_storage_moves_artifact_back_to_storage()
    {
        var artifact = CreateArtifact(out _);
        var returnLocation = Location.Create("Shelf B", LocationType.Storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.Photographer, "Studio");

        artifact.ReturnToStorage(returnLocation);

        Assert.Equal(ArtifactCurrentStatus.InStorage, artifact.CurrentStatus);
        Assert.Equal(returnLocation.LocationId, artifact.CurrentLocationId);
        Assert.Null(artifact.CurrentHolderType);
        Assert.Null(artifact.CurrentHolderName);
        Assert.Equal(returnLocation.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Equal(2, artifact.ConcurrencyToken);
    }

    [Fact]
    public void Delivery_rejects_artifact_that_is_already_out_of_storage()
    {
        var artifact = CreateArtifact(out _);
        artifact.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Documentation");

        Assert.Throws<InvalidOperationException>(() => artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab"));
    }

    [Fact]
    public void Return_rejects_artifact_that_is_already_in_storage()
    {
        var artifact = CreateArtifact(out _);
        var storage = Location.Create("Shelf B", LocationType.Storage);

        Assert.Throws<InvalidOperationException>(() => artifact.ReturnToStorage(storage));
    }

    private static Artifact CreateArtifact(out Location storage)
    {
        var category = ArtifactCategory.Create("ARC", "Archive");
        storage = Location.Create("Shelf A", LocationType.Storage);
        return Artifact.Create(category, 10, "Basic description", storage);
    }
}

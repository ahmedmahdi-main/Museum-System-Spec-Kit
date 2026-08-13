using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Tests.ArtifactRegistry;

public sealed class ArtifactTests
{
    [Fact]
    public void Artifact_gets_stable_internal_id_and_initial_storage_state()
    {
        var category = ArtifactCategory.Create("BRZ", "برونز");
        var location = Location.Create("الخزانة 1", LocationType.Storage);

        var artifact = Artifact.Create(category, 7, "تمثال صغير", location);
        var artifactId = artifact.ArtifactId;

        artifact.UpdateBasicDescription("تمثال برونزي صغير");

        Assert.Equal(artifactId, artifact.ArtifactId);
        Assert.Equal(category.CategoryId, artifact.CategoryId);
        Assert.Equal("BRZ/7", artifact.MuseumNumberDisplay);
        Assert.Equal(ArtifactCurrentStatus.InStorage, artifact.CurrentStatus);
        Assert.Equal(location.LocationId, artifact.CurrentLocationId);
        Assert.Equal(location.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Null(artifact.CurrentHolderType);
        Assert.Null(artifact.CurrentHolderName);
    }

    [Fact]
    public void Artifact_requires_active_storage_location()
    {
        var category = ArtifactCategory.Create("BRZ", "برونز");
        var display = Location.Create("قاعة العرض", LocationType.DisplayHall);

        Assert.Throws<InvalidOperationException>(() => Artifact.Create(category, 7, "تمثال صغير", display));
    }
}

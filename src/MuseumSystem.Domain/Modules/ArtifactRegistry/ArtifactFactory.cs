using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Modules.ArtifactRegistry;

public static class ArtifactFactory
{
    public static Artifact Create(ArtifactCategory category, int itemNumber, string basicDescription, Location initialLocation) =>
        Artifact.Create(category, itemNumber, basicDescription, initialLocation);
}

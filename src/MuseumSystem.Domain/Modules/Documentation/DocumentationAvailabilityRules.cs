using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Modules.Documentation;

public static class DocumentationAvailabilityRules
{
    public static bool IsAvailableForDocumentation(Artifact artifact) =>
        CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision);
}

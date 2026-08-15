using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class DocumentationAvailabilityService
{
    public bool IsAvailableToDocumentation(Artifact artifact) =>
        CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision);

    public string? GetUnavailableReason(Artifact artifact) =>
        IsAvailableToDocumentation(artifact) ? null : "The artifact is not currently held by Documentation.";
}

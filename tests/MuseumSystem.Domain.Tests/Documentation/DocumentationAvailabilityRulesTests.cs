using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class DocumentationAvailabilityRulesTests
{
    [Fact]
    public void Documentation_availability_uses_current_state_holder_type_not_holder_name()
    {
        var artifact = CreateArtifact();
        artifact.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Any display name");

        Assert.True(CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision));
        Assert.True(DocumentationAvailabilityRules.IsAvailableForDocumentation(artifact));
    }

    [Fact]
    public void Other_out_of_storage_holders_are_not_available_to_documentation()
    {
        var artifact = CreateArtifact();
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "DocumentationDivision");

        Assert.False(CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision));
        Assert.False(DocumentationAvailabilityRules.IsAvailableForDocumentation(artifact));
    }

    private static Artifact CreateArtifact()
    {
        var category = ArtifactCategory.Create("DOC", "Documentation category");
        var storage = Location.Create("Storage", LocationType.Storage);
        return Artifact.Create(category, 1, "Artifact", storage);
    }
}

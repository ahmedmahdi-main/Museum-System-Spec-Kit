using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class Feature001RegressionTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Documentation_schema_does_not_break_artifact_search_movement_or_return_state()
    {
        await using var context = fixture.CreateContext();
        var category = ArtifactCategory.Create($"F{Guid.NewGuid():N}"[..8], "Feature one category");
        var storage = Location.Create($"Feature one storage {Guid.NewGuid():N}", LocationType.Storage);
        var artifact = Artifact.Create(category, Random.Shared.Next(1, 1_000_000), "Feature one artifact", storage);
        context.ArtifactCategories.Add(category);
        context.Locations.Add(storage);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();

        var found = await context.Artifacts.SingleAsync(a => a.MuseumNumberDisplay == artifact.MuseumNumberDisplay);
        found.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Documentation");
        context.MovementRecords.Add(MovementRecord.CreateDelivery(Guid.NewGuid(), found, MovementRecipientType.DocumentationDivision, "Documentation", "Documentation", null));
        await context.SaveChangesAsync();

        Assert.True(CurrentStateRules.IsHeldBy(found, MovementRecipientType.DocumentationDivision));

        found.ReturnToStorage(storage);
        context.MovementRecords.Add(MovementRecord.CreateReturn(Guid.NewGuid(), found, storage, null));
        await context.SaveChangesAsync();

        Assert.Equal(ArtifactCurrentStatus.InStorage, found.CurrentStatus);
        Assert.Equal(storage.LocationId, found.CurrentLocationId);
        Assert.Equal(2, await context.MovementRecords.CountAsync(m => m.ArtifactId == found.ArtifactId));
    }
}

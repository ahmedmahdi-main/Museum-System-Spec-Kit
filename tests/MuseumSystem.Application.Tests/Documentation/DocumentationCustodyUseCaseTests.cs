using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationCustodyUseCaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_documentation_succeeds_regardless_of_custody_holder(bool heldByDocumentation)
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        if (heldByDocumentation)
        {
            DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        }
        else
        {
            artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        }

        DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        await db.SaveChangesAsync();

        var holderBefore = artifact.CurrentHolderType;
        var locationBefore = artifact.CurrentLocationId;
        var movementCountBefore = db.MovementRecords.Count();

        var result = await new CreateDocumentationRecordUseCase(db, new DocumentationTemplateResolver(db), new DocumentationAvailabilityService(), new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext())
            .CreateDocumentationRecord(new CreateDocumentationRecordRequest(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        Assert.Equal(holderBefore, artifact.CurrentHolderType);
        Assert.Equal(locationBefore, artifact.CurrentLocationId);
        Assert.Equal(movementCountBefore, db.MovementRecords.Count());
    }
}

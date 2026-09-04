using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DraftCustodyUseCaseTests
{
    [Fact]
    public async Task Draft_save_succeeds_when_artifact_is_no_longer_held_by_documentation_without_changing_custody()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        artifact.ReturnToStorage(storage);
        await db.SaveChangesAsync();

        var statusBefore = artifact.CurrentStatus;
        var holderBefore = artifact.CurrentHolderType;
        var locationBefore = artifact.CurrentLocationId;
        var movementCountBefore = db.MovementRecords.Count();

        var result = await new SaveDocumentationDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext())
            .SaveDocumentationDraft(new SaveDocumentationDraftRequest(record.DocumentationRecordId, record.ConcurrencyToken, DocumentationApplicationTestHost.RequiredTextValue()));

        Assert.True(result.Succeeded);
        Assert.Equal(DocumentationRecordStatus.Draft, record.Status);
        Assert.Equal(statusBefore, artifact.CurrentStatus);
        Assert.Equal(holderBefore, artifact.CurrentHolderType);
        Assert.Equal(locationBefore, artifact.CurrentLocationId);
        Assert.Equal(movementCountBefore, db.MovementRecords.Count());
    }
}

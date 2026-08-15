using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DraftCustodyUseCaseTests
{
    [Fact]
    public async Task Draft_save_is_blocked_when_artifact_is_no_longer_held_by_documentation()
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

        var result = await new SaveDocumentationDraftUseCase(db, new DocumentationAvailabilityService(), new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext())
            .SaveDocumentationDraft(new SaveDocumentationDraftRequest(record.DocumentationRecordId, record.ConcurrencyToken, DocumentationApplicationTestHost.RequiredTextValue()));

        Assert.False(result.Succeeded);
        Assert.Equal("DocumentationRecord.CustodyRequired", result.ValidationIssues[0].Code);
        Assert.Equal(DocumentationRecordStatus.Draft, record.Status);
    }
}

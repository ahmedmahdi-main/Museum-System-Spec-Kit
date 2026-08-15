using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class GetDocumentationRecordForEditUseCaseTests
{
    [Fact]
    public async Task Returns_bound_template_fields_values_status_and_concurrency_token()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "tester");
        record.SaveDraft(new Dictionary<string, DocumentationFieldValue>(StringComparer.Ordinal) { ["title"] = DocumentationFieldValue.Text("Saved title") }, version, "tester");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var result = await new GetDocumentationRecordForEditUseCase(db, new DocumentationAvailabilityService())
            .GetDocumentationRecordForEdit(new GetDocumentationRecordForEditRequest(record.DocumentationRecordId, new DocumentationActionPermissionSet(CanEdit: true, CanComplete: true)));

        Assert.True(result.Succeeded);
        Assert.Equal(DocumentationRecordStatus.Draft, result.Value!.Record.Status);
        Assert.Equal(record.ConcurrencyToken, result.Value.Record.ConcurrencyToken);
        Assert.Equal(version.DocumentationTemplateVersionId, result.Value.TemplateVersion.DocumentationTemplateVersionId);
        Assert.Equal("Saved title", result.Value.Values.Single(value => value.FieldKey == "title").TextValue);
        Assert.True(result.Value.Actions.CanSaveDraft);
        Assert.True(result.Value.Actions.CanComplete);
    }
}

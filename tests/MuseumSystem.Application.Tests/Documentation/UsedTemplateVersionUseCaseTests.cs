using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class UsedTemplateVersionUseCaseTests
{
    [Fact]
    public async Task Used_version_rejects_definition_edit_but_allows_retirement()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var version = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        template.ActivateVersion(version, "tester");
        db.DocumentationRecords.Add(DocumentationRecord.Create(Guid.NewGuid(), version, "tester"));
        await db.SaveChangesAsync();

        var edit = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(version.DocumentationTemplateVersionId, version.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("changed", "Changed", DocumentationFieldType.Text, true, 1, "Main", null, [])
            ]));
        var retire = await new RetireTemplateVersionUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).RetireTemplateVersion(
            new RetireTemplateVersionRequest(version.DocumentationTemplateVersionId, version.ConcurrencyToken));

        Assert.False(edit.Succeeded);
        Assert.True(retire.Succeeded);
        Assert.Equal(DocumentationTemplateVersionStatus.Retired, version.Status);
    }
}

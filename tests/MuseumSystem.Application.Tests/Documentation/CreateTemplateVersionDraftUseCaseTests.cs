using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class CreateTemplateVersionDraftUseCaseTests
{
    [Fact]
    public async Task Creates_empty_draft_version_with_next_version_number()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        await db.SaveChangesAsync();

        var result = await new CreateTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).CreateTemplateVersionDraft(
            new CreateTemplateVersionDraftRequest(template.DocumentationTemplateId));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.VersionNumber);
        Assert.Empty(result.Value.Fields);
    }

    [Fact]
    public async Task Copies_existing_version_into_new_draft_with_new_field_ids()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var source = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        await db.SaveChangesAsync();

        var result = await new CreateTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).CreateTemplateVersionDraft(
            new CreateTemplateVersionDraftRequest(template.DocumentationTemplateId, source.DocumentationTemplateVersionId));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.VersionNumber);
        Assert.Equal(source.Fields[0].FieldKey, result.Value.Fields[0].FieldKey);
        Assert.NotEqual(source.Fields[0].DocumentationTemplateFieldId, result.Value.Fields[0].DocumentationTemplateFieldId);
    }
}

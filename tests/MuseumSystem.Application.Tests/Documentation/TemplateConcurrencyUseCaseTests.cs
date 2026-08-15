using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class TemplateConcurrencyUseCaseTests
{
    [Fact]
    public async Task Stale_draft_save_returns_conflict_without_updating_fields()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField("original")]);
        await db.SaveChangesAsync();

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken - 1, DocumentationApplicationTestHost.SevenFieldInputs()));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Single(draft.Fields);
        Assert.Equal("original", draft.Fields[0].FieldKey);
    }

    [Fact]
    public async Task Stale_lifecycle_write_returns_conflict()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        await db.SaveChangesAsync();

        var result = await new ActivateTemplateVersionUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).ActivateTemplateVersion(
            new ActivateTemplateVersionRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken + 1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
    }
}

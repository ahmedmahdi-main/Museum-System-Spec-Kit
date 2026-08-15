using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class SaveTemplateVersionDraftUseCaseTests
{
    [Fact]
    public async Task Saves_draft_fields_for_all_seven_supported_field_types()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template);
        await db.SaveChangesAsync();

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken, DocumentationApplicationTestHost.SevenFieldInputs()));

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.Value!.Fields.Count);
        Assert.Equal(Enum.GetValues<DocumentationFieldType>().Order(), result.Value.Fields.Select(field => field.FieldType).Order());
    }

    [Fact]
    public async Task Allows_draft_select_fields_without_options_before_activation()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template);
        await db.SaveChangesAsync();

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("single", "Single", DocumentationFieldType.SingleSelect, false, 1, "Options", null, [])
            ]));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Fields[0].Options);
    }
}

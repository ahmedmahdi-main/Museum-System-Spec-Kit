using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class TemplateFieldValidationUseCaseTests
{
    [Fact]
    public async Task Rejects_duplicate_field_keys()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var draft = await CreateDraft(db);

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("same", "One", DocumentationFieldType.Text, true, 1, "Main", "Help", []),
                new DocumentationTemplateFieldInputDto("same", "Two", DocumentationFieldType.Number, false, 2, "Main", null, [])
            ]));

        Assert.False(result.Succeeded);
        Assert.Contains("Duplicate field key", result.ValidationIssues[0].Message);
    }

    [Fact]
    public async Task Rejects_duplicate_option_keys_and_options_on_non_select_fields()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var duplicateOptionDraft = await CreateDraft(db);
        var useCase = new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext());

        var duplicateOption = await useCase.SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(duplicateOptionDraft.DocumentationTemplateVersionId, duplicateOptionDraft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("single", "Single", DocumentationFieldType.SingleSelect, true, 1, "Options", null,
                [
                    new DocumentationTemplateFieldOptionInputDto("a", "A", 1),
                    new DocumentationTemplateFieldOptionInputDto("a", "Also A", 2)
                ])
            ]));

        var nonSelectDraft = db.DocumentationTemplates.Single().CreateDraftVersion(actor: "tester");
        db.DocumentationTemplateVersions.Add(nonSelectDraft);
        await db.SaveChangesAsync();
        var nonSelectOptions = await useCase.SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(nonSelectDraft.DocumentationTemplateVersionId, nonSelectDraft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("text", "Text", DocumentationFieldType.Text, false, 1, "Main", null,
                [
                    new DocumentationTemplateFieldOptionInputDto("a", "A", 1)
                ])
            ]));

        Assert.False(duplicateOption.Succeeded);
        Assert.False(nonSelectOptions.Succeeded);
        Assert.Contains("Duplicate option key", duplicateOption.ValidationIssues[0].Message);
        Assert.Contains("Only select fields", nonSelectOptions.ValidationIssues[0].Message);
    }

    [Fact]
    public async Task Preserves_required_section_help_text_and_display_order()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var draft = await CreateDraft(db);

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("condition", "Condition", DocumentationFieldType.Text, true, 4, "Condition", "Describe visible condition.", [])
            ]));

        Assert.True(result.Succeeded);
        var field = Assert.Single(result.Value!.Fields);
        Assert.True(field.IsRequired);
        Assert.Equal("Condition", field.Section);
        Assert.Equal("Describe visible condition.", field.HelpText);
        Assert.Equal(4, field.DisplayOrder);
    }

    [Fact]
    public async Task Activation_rejects_select_fields_without_options()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var draft = await CreateDraft(db);
        var save = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("single", "Single", DocumentationFieldType.SingleSelect, false, 1, "Options", null, [])
            ]));

        var result = await new ActivateTemplateVersionUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).ActivateTemplateVersion(
            new ActivateTemplateVersionRequest(draft.DocumentationTemplateVersionId, save.Value!.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains("requires at least one option", result.ValidationIssues[0].Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Save_draft_rejects_non_positive_field_display_order(int displayOrder)
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var draft = await CreateDraft(db);

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("condition", "Condition", DocumentationFieldType.Text, true, displayOrder, "Main", null, [])
            ]));

        Assert.False(result.Succeeded);
        Assert.Contains("Field display order", result.ValidationIssues[0].Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Save_draft_rejects_non_positive_option_display_order(int displayOrder)
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var draft = await CreateDraft(db);

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("condition", "Condition", DocumentationFieldType.SingleSelect, true, 1, "Main", null,
                [
                    new DocumentationTemplateFieldOptionInputDto("good", "Good", displayOrder)
                ])
            ]));

        Assert.False(result.Succeeded);
        Assert.Contains("Option display order", result.ValidationIssues[0].Message);
    }

    [Fact]
    public async Task Save_draft_preserves_positive_option_display_order()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var draft = await CreateDraft(db);

        var result = await new SaveTemplateVersionDraftUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken,
            [
                new DocumentationTemplateFieldInputDto("condition", "Condition", DocumentationFieldType.SingleSelect, true, 3, "Main", null,
                [
                    new DocumentationTemplateFieldOptionInputDto("good", "Good", 2)
                ])
            ]));

        Assert.True(result.Succeeded);
        var field = Assert.Single(result.Value!.Fields);
        Assert.Equal(3, field.DisplayOrder);
        Assert.Equal(2, field.Options.Single().DisplayOrder);
    }
    private static async Task<DocumentationTemplateVersion> CreateDraft(MuseumSystem.Infrastructure.Persistence.MuseumDbContext db)
    {
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template);
        await db.SaveChangesAsync();
        return draft;
    }
}

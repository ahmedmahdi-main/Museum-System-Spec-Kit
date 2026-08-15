using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class CreateDocumentationTemplateUseCaseTests
{
    [Fact]
    public async Task Creates_template_family_for_existing_category_using_authenticated_actor_user_id()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditWriter();

        var result = await new CreateDocumentationTemplateUseCase(db, audit, DocumentationApplicationTestHost.ActorContext("user-42", "Manager")).CreateDocumentationTemplate(new CreateDocumentationTemplateRequest(
            category.CategoryId,
            "Ceramics documentation",
            "Fields for ceramics"));

        Assert.True(result.Succeeded);
        Assert.Equal(category.CategoryId, result.Value!.ArtifactCategoryId);
        var template = Assert.Single(db.DocumentationTemplates);
        Assert.Equal("user-42", template.CreatedBy);
    }

    [Fact]
    public async Task Uses_actor_display_name_when_authenticated_actor_has_no_user_id()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        await db.SaveChangesAsync();

        var result = await new CreateDocumentationTemplateUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext(null, "Display Manager")).CreateDocumentationTemplate(
            new CreateDocumentationTemplateRequest(category.CategoryId, "Display actor template", null));

        Assert.True(result.Succeeded);
        Assert.Equal("Display Manager", db.DocumentationTemplates.Single().CreatedBy);
    }

    [Fact]
    public async Task Rejects_missing_category_and_second_family_for_same_category()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        DocumentationApplicationTestHost.AddTemplate(db, category);
        await db.SaveChangesAsync();
        var useCase = new CreateDocumentationTemplateUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext());

        var missing = await useCase.CreateDocumentationTemplate(new CreateDocumentationTemplateRequest(Guid.NewGuid(), "Missing", null));
        var duplicate = await useCase.CreateDocumentationTemplate(new CreateDocumentationTemplateRequest(category.CategoryId, "Duplicate", null));

        Assert.False(missing.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal("ArtifactCategory.NotFound", missing.ValidationIssues[0].Code);
        Assert.Equal("DocumentationTemplate.CategoryAlreadyHasTemplate", duplicate.ValidationIssues[0].Code);
    }
}
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class TemplateQueryUseCaseTests
{
    [Fact]
    public async Task Lists_template_families_with_category_and_version_counts()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        template.ActivateVersion(draft, "tester");
        await db.SaveChangesAsync();

        var items = await new TemplateQueryUseCases(db).ListDocumentationTemplates();

        var item = Assert.Single(items);
        Assert.Equal(category.CategoryCode, item.ArtifactCategoryCode);
        Assert.Equal(1, item.VersionCount);
        Assert.Equal(1, item.ActiveVersionCount);
        Assert.Equal(DocumentationTemplateVersionStatus.Active, item.ActiveVersion!.Status);
    }

    [Fact]
    public async Task Views_template_version_with_used_read_only_indicator()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        template.ActivateVersion(draft, "tester");
        var record = DocumentationRecord.Create(Guid.NewGuid(), draft, "tester");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var result = await new TemplateQueryUseCases(db).ViewTemplateVersion(draft.DocumentationTemplateVersionId);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.IsUsed);
        Assert.True(result.Value.IsReadOnly);
        Assert.Single(result.Value.Fields);
    }
}

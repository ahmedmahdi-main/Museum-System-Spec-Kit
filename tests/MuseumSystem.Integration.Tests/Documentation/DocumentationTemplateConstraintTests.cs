using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationTemplateConstraintTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Database_allows_zero_active_versions_after_retirement()
    {
        await using var context = fixture.CreateContext();
        var category = ArtifactCategory.Create($"Z{Guid.NewGuid():N}"[..8], "Zero active category");
        var template = DocumentationTemplate.Create(category.CategoryId, "Zero active template");
        var version = template.CreateDraftVersion([DocumentationTemplateField.Create("name", "Name", DocumentationFieldType.Text, true, 1, "Main")]);
        template.ActivateVersion(version);
        template.RetireVersion(version);
        context.ArtifactCategories.Add(category);
        context.DocumentationTemplates.Add(template);

        await context.SaveChangesAsync();

        Assert.Empty(await context.DocumentationTemplateVersions.Where(v => v.DocumentationTemplateId == template.DocumentationTemplateId && v.Status == DocumentationTemplateVersionStatus.Active).ToListAsync());
    }

    [Fact]
    public async Task Database_rejects_two_active_versions_for_one_template()
    {
        await using var context = fixture.CreateContext();
        var category = ArtifactCategory.Create($"A{Guid.NewGuid():N}"[..8], "Active race category");
        var template = DocumentationTemplate.Create(category.CategoryId, "Active race template");
        var first = template.CreateDraftVersion([DocumentationTemplateField.Create("first", "First", DocumentationFieldType.Text, true, 1, "Main")]);
        var second = template.CreateDraftVersion([DocumentationTemplateField.Create("second", "Second", DocumentationFieldType.Text, true, 1, "Main")]);
        first.Activate();
        second.Activate();
        context.ArtifactCategories.Add(category);
        context.DocumentationTemplates.Add(template);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_rejects_stale_concurrent_activation_that_would_create_two_active_versions()
    {
        Guid templateId;
        await using (var seed = fixture.CreateContext())
        {
            var category = ArtifactCategory.Create($"R{Guid.NewGuid():N}"[..8], "Activation race category");
            var template = DocumentationTemplate.Create(category.CategoryId, "Activation race template");
            template.CreateDraftVersion([DocumentationTemplateField.Create("first", "First", DocumentationFieldType.Text, true, 1, "Main")]);
            template.CreateDraftVersion([DocumentationTemplateField.Create("second", "Second", DocumentationFieldType.Text, true, 1, "Main")]);
            templateId = template.DocumentationTemplateId;
            seed.ArtifactCategories.Add(category);
            seed.DocumentationTemplates.Add(template);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var firstTemplate = await LoadTemplateAsync(firstContext, templateId);
        var secondTemplate = await LoadTemplateAsync(secondContext, templateId);

        firstTemplate.ActivateVersion(firstTemplate.Versions.Single(v => v.VersionNumber == 1), "first-manager");
        await firstContext.SaveChangesAsync();

        secondTemplate.ActivateVersion(secondTemplate.Versions.Single(v => v.VersionNumber == 2), "second-manager");
        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    private static Task<DocumentationTemplate> LoadTemplateAsync(DbContext context, Guid templateId) =>
        context.Set<DocumentationTemplate>()
            .Include(template => template.Versions)
            .ThenInclude(version => version.Fields)
            .SingleAsync(template => template.DocumentationTemplateId == templateId);
}

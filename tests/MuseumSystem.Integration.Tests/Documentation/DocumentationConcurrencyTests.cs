using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationConcurrencyTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Documentation_record_uses_optimistic_concurrency_token()
    {
        Guid recordId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(seed, $"C{Guid.NewGuid():N}"[..8]);
            var record = DocumentationRecord.Create(artifact.ArtifactId, version);
            recordId = record.DocumentationRecordId;
            seed.DocumentationRecords.Add(record);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.DocumentationRecords.Include(r => r.DocumentationTemplateVersion).ThenInclude(v => v!.Fields).SingleAsync(r => r.DocumentationRecordId == recordId);
        var second = await secondContext.DocumentationRecords.Include(r => r.DocumentationTemplateVersion).ThenInclude(v => v!.Fields).SingleAsync(r => r.DocumentationRecordId == recordId);

        first.SaveDraft(new Dictionary<string, DocumentationFieldValue> { ["title"] = DocumentationFieldValue.Text("First") }, first.DocumentationTemplateVersion!);
        await firstContext.SaveChangesAsync();

        second.SaveDraft(new Dictionary<string, DocumentationFieldValue> { ["title"] = DocumentationFieldValue.Text("Second") }, second.DocumentationTemplateVersion!);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Documentation_template_version_uses_optimistic_concurrency_token_for_stale_draft_changes()
    {
        Guid templateId;
        await using (var seed = fixture.CreateContext())
        {
            var category = ArtifactCategory.Create($"TC{Guid.NewGuid():N}"[..8], "Template concurrency category");
            var template = DocumentationTemplate.Create(category.CategoryId, "Template concurrency");
            template.CreateDraftVersion([DocumentationTemplateField.Create("initial", "Initial", DocumentationFieldType.Text, true, 1, "Main")]);
            templateId = template.DocumentationTemplateId;
            seed.ArtifactCategories.Add(category);
            seed.DocumentationTemplates.Add(template);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await LoadTemplateAsync(firstContext, templateId);
        var second = await LoadTemplateAsync(secondContext, templateId);
        var firstVersion = first.Versions.Single();
        var secondVersion = second.Versions.Single();

        first.ActivateVersion(firstVersion, "first-manager");
        await firstContext.SaveChangesAsync();

        second.ActivateVersion(secondVersion, "second-manager");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private static Task<DocumentationTemplate> LoadTemplateAsync(DbContext context, Guid templateId) =>
        context.Set<DocumentationTemplate>()
            .Include(template => template.Versions)
            .ThenInclude(version => version.Fields)
            .SingleAsync(template => template.DocumentationTemplateId == templateId);
}

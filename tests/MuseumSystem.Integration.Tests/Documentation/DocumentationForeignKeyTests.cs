using Npgsql;

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationForeignKeyTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Documentation_records_restrict_missing_artifact_and_template_version_references()
    {
        await using var context = fixture.CreateContext();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."DocumentationRecords" (
                "DocumentationRecordId", "ArtifactId", "DocumentationTemplateVersionId", "Status", "Values", "CreatedAt", "ConcurrencyToken")
            values ({0}, {1}, {2}, 'Draft', '{{}}', now(), 0)
            """, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Deleting_artifact_referenced_by_documentation_record_is_restricted()
    {
        await using var context = fixture.CreateContext();
        var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(context, $"AF{Guid.NewGuid():N}"[..8]);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version);
        context.DocumentationRecords.Add(record);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            delete from museum."Artifacts" where "ArtifactId" = {0}
            """, artifact.ArtifactId));
    }

    [Fact]
    public async Task Deleting_artifact_category_referenced_by_documentation_template_is_restricted()
    {
        await using var context = fixture.CreateContext();
        var category = ArtifactCategory.Create($"CF{Guid.NewGuid():N}"[..8], "Category FK");
        var template = DocumentationTemplate.Create(category.CategoryId, "Template FK");
        context.ArtifactCategories.Add(category);
        context.DocumentationTemplates.Add(template);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            delete from museum."ArtifactCategories" where "CategoryId" = {0}
            """, category.CategoryId));
    }
}

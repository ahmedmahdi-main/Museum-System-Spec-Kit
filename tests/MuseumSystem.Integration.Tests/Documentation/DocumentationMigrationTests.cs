using Microsoft.EntityFrameworkCore;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationMigrationTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Documentation_migration_is_applied_to_postgresql_schema()
    {
        await using var context = fixture.CreateContext();

        var migration = await context.Database.SqlQueryRaw<string>("""
            select "MigrationId" as "Value" from "__EFMigrationsHistory"
            where "MigrationId" like '%AddDocumentationSchema'
            """).SingleAsync();
        var tableCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value" from information_schema.tables
            where table_schema = 'museum' and table_name in (
                'DocumentationTemplates',
                'DocumentationTemplateVersions',
                'DocumentationTemplateFields',
                'DocumentationTemplateFieldOptions',
                'DocumentationRecords',
                'DocumentationRevisions')
            """).SingleAsync();

        Assert.EndsWith("AddDocumentationSchema", migration, StringComparison.Ordinal);
        Assert.Equal(6, tableCount);
    }
}

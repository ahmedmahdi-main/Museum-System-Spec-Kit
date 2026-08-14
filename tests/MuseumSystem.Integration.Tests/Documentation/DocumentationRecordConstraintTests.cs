using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationRecordConstraintTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Database_enforces_one_documentation_record_per_artifact()
    {
        await using var context = fixture.CreateContext();
        var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(context, $"R{Guid.NewGuid():N}"[..8]);
        context.DocumentationRecords.Add(DocumentationRecord.Create(artifact.ArtifactId, version));
        context.DocumentationRecords.Add(DocumentationRecord.Create(artifact.ArtifactId, version));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

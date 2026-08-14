using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationJsonbPersistenceTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Documentation_values_and_revision_snapshots_round_trip_as_jsonb()
    {
        await using var context = fixture.CreateContext();
        var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(context, $"J{Guid.NewGuid():N}"[..8]);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        record.Complete(DocumentationTestData.CompletedValues("Baseline"), version, "completer");
        record.CorrectCompleted(DocumentationTestData.CompletedValues("Corrected"), version, "{\"title\":\"Baseline -> Corrected\"}", "Better reading", "editor");
        context.DocumentationRecords.Add(record);
        await context.SaveChangesAsync();

        await using var reload = fixture.CreateContext();
        var saved = await reload.DocumentationRecords.Include(r => r.Revisions).SingleAsync(r => r.DocumentationRecordId == record.DocumentationRecordId);

        Assert.Contains("Baseline", saved.CompletedBaselineValuesJson, StringComparison.Ordinal);
        Assert.Contains("Corrected", saved.ValuesJson, StringComparison.Ordinal);
        Assert.Contains("Corrected", saved.Revisions.Single().NewValuesJson, StringComparison.Ordinal);
        Assert.Contains("Baseline -> Corrected", saved.Revisions.Single().ChangeSummaryJson, StringComparison.Ordinal);
    }
}

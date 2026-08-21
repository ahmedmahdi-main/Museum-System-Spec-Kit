using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Documentation;
using System.Text.Json;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationRevisionPersistenceTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Revision_two_and_three_round_trip_as_immutable_jsonb_snapshots()
    {
        await using var context = fixture.CreateContext();
        var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(context, $"R{Guid.NewGuid():N}"[..8]);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        record.Complete(DocumentationTestData.CompletedValues("Baseline"), version, "completer");
        var two = record.CorrectCompleted(DocumentationTestData.CompletedValues("Second"), version, "[{\"fieldKey\":\"title\"}]", "Second reason", "editor-2");
        context.DocumentationRecords.Add(record);
        await context.SaveChangesAsync();
        var twoSnapshot = (two.PreviousValuesJson, two.NewValuesJson, two.ChangeSummaryJson, two.Reason, two.CreatedBy, two.CreatedAt);

        var three = record.CorrectCompleted(DocumentationTestData.CompletedValues("Third"), version, "[{\"fieldKey\":\"title\"}]", "Third reason", "editor-3");
        context.DocumentationRevisions.Add(three);
        await context.SaveChangesAsync();

        await using var reload = fixture.CreateContext();
        var saved = await reload.DocumentationRecords.AsNoTracking().Include(item => item.Revisions).SingleAsync(item => item.DocumentationRecordId == record.DocumentationRecordId);
        Assert.Equal(version.DocumentationTemplateVersionId, saved.DocumentationTemplateVersionId);
        Assert.Equal([2, 3], saved.Revisions.OrderBy(item => item.RevisionNumber).Select(item => item.RevisionNumber));
        var savedTwo = saved.Revisions.Single(item => item.RevisionNumber == 2);
        AssertJsonEqual(twoSnapshot.PreviousValuesJson, savedTwo.PreviousValuesJson);
        AssertJsonEqual(twoSnapshot.NewValuesJson, savedTwo.NewValuesJson);
        AssertJsonEqual(twoSnapshot.ChangeSummaryJson, savedTwo.ChangeSummaryJson);
        Assert.Equal(twoSnapshot.Reason, savedTwo.Reason);
        Assert.Equal(twoSnapshot.CreatedBy, savedTwo.CreatedBy);
        Assert.InRange(savedTwo.CreatedAt, twoSnapshot.CreatedAt.AddMilliseconds(-1), twoSnapshot.CreatedAt.AddMilliseconds(1));
        Assert.Contains("Baseline", savedTwo.PreviousValuesJson);
        Assert.Contains("Second", savedTwo.NewValuesJson);
        Assert.Equal("Third reason", saved.Revisions.Single(item => item.RevisionNumber == 3).Reason);
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement));
    }
}

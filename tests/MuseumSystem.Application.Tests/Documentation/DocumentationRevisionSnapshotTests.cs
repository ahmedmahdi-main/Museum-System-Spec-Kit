using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationRevisionSnapshotTests
{
    [Fact]
    public void Change_summary_handles_all_types_and_ignores_multiselect_order()
    {
        var fields = new[]
        {
            DocumentationTemplateField.Create("text", "Text label", DocumentationFieldType.Text, false, 1, "Main"),
            DocumentationTemplateField.Create("multiline", "Notes label", DocumentationFieldType.MultilineText, false, 2, "Main"),
            DocumentationTemplateField.Create("number", "Number label", DocumentationFieldType.Number, false, 3, "Main"),
            DocumentationTemplateField.Create("date", "Date label", DocumentationFieldType.Date, false, 4, "Main"),
            DocumentationTemplateField.Create("boolean", "Boolean label", DocumentationFieldType.Boolean, false, 5, "Main"),
            DocumentationTemplateField.Create("single", "Single label", DocumentationFieldType.SingleSelect, false, 6, "Main", options: [DocumentationTemplateFieldOption.Create("a", "Alpha", 1), DocumentationTemplateFieldOption.Create("b", "Beta", 2)]),
            DocumentationTemplateField.Create("many", "Many label", DocumentationFieldType.MultiSelect, false, 7, "Main", options: [DocumentationTemplateFieldOption.Create("x", "X label", 1), DocumentationTemplateFieldOption.Create("y", "Y label", 2), DocumentationTemplateFieldOption.Create("z", "Z label", 3)])
        };
        const string previous = "{\"text\":\"old\",\"multiline\":\"old notes\",\"number\":1.5,\"date\":\"2026-01-01\",\"boolean\":false,\"single\":\"a\",\"many\":[\"x\",\"y\"]}";
        const string current = "{\"text\":\"new\",\"multiline\":\"new notes\",\"number\":2.5,\"date\":\"2026-01-02\",\"boolean\":true,\"single\":\"b\",\"many\":[\"y\",\"x\"]}";

        var changes = new DocumentationChangeSummaryService().Create(previous, current, fields);

        Assert.Equal(6, changes.Count);
        Assert.DoesNotContain(changes, change => change.FieldKey == "many");
        Assert.Contains(changes, change => change.FieldKey == "single" && change.Summary.Contains("Alpha") && change.Summary.Contains("Beta"));
        Assert.Contains(changes, change => change.FieldKey == "number" && change.Summary.Contains("1.5") && change.Summary.Contains("2.5"));
        Assert.Contains(changes, change => change.FieldKey == "date" && change.Summary.Contains("2026-01-02"));
        Assert.Contains(changes, change => change.FieldKey == "boolean" && change.Summary.Contains("لا") && change.Summary.Contains("نعم"));
    }

    [Fact]
    public async Task Revision_preserves_previous_new_summary_reason_author_timestamp_and_template()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, version, audit) = await PhaseDTestData.CompletedRecordAsync(db);

        await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Assessment changed"));
        var revision = Assert.Single(record.Revisions);

        Assert.Contains("good", revision.PreviousValuesJson);
        Assert.Contains("fair", revision.NewValuesJson);
        Assert.Contains("Condition label", revision.ChangeSummaryJson);
        Assert.Contains("Good label", revision.ChangeSummaryJson);
        Assert.Contains("Fair label", revision.ChangeSummaryJson);
        Assert.Equal("Assessment changed", revision.Reason);
        Assert.Equal("editor", revision.CreatedBy);
        Assert.True(revision.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal(version.DocumentationTemplateVersionId, revision.TemplateVersionId);
    }
}

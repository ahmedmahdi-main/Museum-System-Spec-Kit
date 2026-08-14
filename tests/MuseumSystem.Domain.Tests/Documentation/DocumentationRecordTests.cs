using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class DocumentationRecordTests
{
    [Fact]
    public void Record_creation_requires_active_template_version()
    {
        var draft = DocumentationTemplateVersion.CreateDraft(1, [DocumentationTemplateField.Create("name", "Name", DocumentationFieldType.Text, true, 1, "Main")]);

        Assert.Throws<InvalidOperationException>(() => DocumentationRecord.Create(Guid.NewGuid(), draft));

        draft.Activate();
        draft.Retire();

        Assert.Throws<InvalidOperationException>(() => DocumentationRecord.Create(Guid.NewGuid(), draft));
    }

    [Fact]
    public void Draft_can_be_completed_with_revision_one_baseline_without_revision_row()
    {
        var version = TestVersion();
        var record = DocumentationRecord.Create(Guid.NewGuid(), version, "creator");

        record.SaveDraft(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Vase") }, version, "editor");
        record.Complete(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Vase") }, version, "completer");

        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.NotNull(record.CompletedBaselineValuesJson);
        Assert.Empty(record.Revisions);
    }

    [Fact]
    public void Existing_record_remains_bound_to_original_template_version_after_retirement()
    {
        var version = TestVersion();
        var record = DocumentationRecord.Create(Guid.NewGuid(), version);

        version.Retire("manager");
        record.SaveDraft(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Vase") }, version);
        record.Complete(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Vase") }, version);

        Assert.Equal(version.DocumentationTemplateVersionId, record.DocumentationTemplateVersionId);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
    }

    [Fact]
    public void Corrections_begin_at_revision_two_and_require_reason_and_change_summary()
    {
        var version = TestVersion();
        var record = DocumentationRecord.Create(Guid.NewGuid(), version);
        record.Complete(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Vase") }, version);

        Assert.Throws<ArgumentException>(() => record.CorrectCompleted(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Jar") }, version, "{}", " "));
        Assert.Throws<ArgumentException>(() => record.CorrectCompleted(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Jar") }, version, " ", "New reading"));
        var revision = record.CorrectCompleted(new Dictionary<string, DocumentationFieldValue> { ["name"] = DocumentationFieldValue.Text("Jar") }, version, "{\"name\":\"Vase -> Jar\"}", "New reading", "editor");

        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal("New reading", revision.Reason);
        Assert.Equal("{\"name\":\"Vase -> Jar\"}", revision.ChangeSummaryJson);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
    }

    private static DocumentationTemplateVersion TestVersion()
    {
        var version = DocumentationTemplateVersion.CreateDraft(1, [DocumentationTemplateField.Create("name", "Name", DocumentationFieldType.Text, true, 1, "Main")]);
        version.Activate();
        return version;
    }
}

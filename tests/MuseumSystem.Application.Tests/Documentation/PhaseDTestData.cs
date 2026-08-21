using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Documentation;

internal static class PhaseDTestData
{
    public static async Task<(DocumentationRecord Record, DocumentationTemplateVersion Version, RecordingAuditWriter Audit)> CompletedRecordAsync(MuseumDbContext db)
    {
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        var field = DocumentationTemplateField.Create("condition", "Condition label", DocumentationFieldType.SingleSelect, true, 1, "Main", options:
        [DocumentationTemplateFieldOption.Create("good", "Good label", 1), DocumentationTemplateFieldOption.Create("fair", "Fair label", 2)]);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category, [field]);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        record.Complete(new Dictionary<string, DocumentationFieldValue> { ["condition"] = DocumentationFieldValue.SingleSelect("good") }, version, "completer");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();
        return (record, version, new RecordingAuditWriter());
    }

    public static CorrectCompletedDocumentationUseCase CorrectUseCase(MuseumDbContext db, RecordingAuditWriter audit) =>
        new(db, new DocumentationChangeSummaryService(), audit, DocumentationApplicationTestHost.ActorContext("editor"));

    public static IReadOnlyList<DocumentationFieldValueInputDto> Value(string optionKey) =>
        [new DocumentationFieldValueInputDto { FieldKey = "condition", OptionKey = optionKey }];
}

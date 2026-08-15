using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class SaveDocumentationDraftUseCaseTests
{
    [Fact]
    public async Task Saves_valid_draft_values_updates_metadata_and_creates_no_formal_revision()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditWriter();

        var result = await NewUseCase(db, audit).SaveDocumentationDraft(new SaveDocumentationDraftRequest(
            record.DocumentationRecordId,
            record.ConcurrencyToken,
            DocumentationApplicationTestHost.RequiredTextValue("title", "Draft title")));

        Assert.True(result.Succeeded);
        Assert.Equal("user-1", record.LastModifiedBy);
        Assert.Empty(db.DocumentationRevisions);
        Assert.Contains("Draft title", record.ValuesJson);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.RecordSaveDraft);
    }

    [Fact]
    public async Task Round_trips_typed_values_for_all_supported_field_types_through_save_and_reload()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var fields = AllFieldDefinitions();
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category, fields);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var values = new List<DocumentationFieldValueInputDto>
        {
            new() { FieldKey = "text", TextValue = "Short text" },
            new() { FieldKey = "multiline", TextValue = "Line one\nLine two" },
            new() { FieldKey = "number", NumberValue = 12.5m },
            new() { FieldKey = "date", DateValue = new DateOnly(2026, 8, 15) },
            new() { FieldKey = "boolean", BooleanValue = true },
            new() { FieldKey = "single", OptionKey = "a" },
            new() { FieldKey = "many", OptionKeys = ["x", "y"] }
        };

        var save = await NewUseCase(db).SaveDocumentationDraft(new SaveDocumentationDraftRequest(record.DocumentationRecordId, record.ConcurrencyToken, values));
        var reload = await new GetDocumentationRecordForEditUseCase(db, new DocumentationAvailabilityService())
            .GetDocumentationRecordForEdit(new GetDocumentationRecordForEditRequest(record.DocumentationRecordId, new DocumentationActionPermissionSet(CanEdit: true)));

        Assert.True(save.Succeeded);
        Assert.True(reload.Succeeded);
        var returned = reload.Value!.Values.ToDictionary(value => value.FieldKey, StringComparer.Ordinal);
        Assert.Equal("Short text", returned["text"].TextValue);
        Assert.Equal("Line one\nLine two", returned["multiline"].TextValue);
        Assert.Equal(12.5m, returned["number"].NumberValue);
        Assert.Equal(new DateOnly(2026, 8, 15), returned["date"].DateValue);
        Assert.True(returned["boolean"].BooleanValue);
        Assert.Equal("a", returned["single"].OptionKey);
        Assert.Equal(["x", "y"], returned["many"].OptionKeys);
    }

    [Fact]
    public async Task Rejects_invalid_field_values()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var selectField = DocumentationTemplateField.Create("condition", "Condition", DocumentationFieldType.SingleSelect, false, 1, "Main", null,
        [
            DocumentationTemplateFieldOption.Create("good", "Good", 1)
        ]);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category, [selectField]);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var result = await NewUseCase(db).SaveDocumentationDraft(new SaveDocumentationDraftRequest(
            record.DocumentationRecordId,
            record.ConcurrencyToken,
            [new DocumentationFieldValueInputDto { FieldKey = "condition", OptionKey = "bad" }]));

        Assert.False(result.Succeeded);
        Assert.Equal("DocumentationRecord.ValuesInvalid", result.ValidationIssues[0].Code);
        Assert.Empty(db.DocumentationRevisions);
    }

    private static IReadOnlyList<DocumentationTemplateField> AllFieldDefinitions() =>
    [
        DocumentationTemplateField.Create("text", "Text", DocumentationFieldType.Text, true, 1, "Identity", "Short text"),
        DocumentationTemplateField.Create("multiline", "Multiline", DocumentationFieldType.MultilineText, false, 2, "Identity", "Long text"),
        DocumentationTemplateField.Create("number", "Number", DocumentationFieldType.Number, false, 3, "Measurements", null),
        DocumentationTemplateField.Create("date", "Date", DocumentationFieldType.Date, false, 4, "Measurements", null),
        DocumentationTemplateField.Create("boolean", "Boolean", DocumentationFieldType.Boolean, false, 5, "Flags", null),
        DocumentationTemplateField.Create("single", "Single", DocumentationFieldType.SingleSelect, true, 6, "Options", null,
        [
            DocumentationTemplateFieldOption.Create("a", "A", 1),
            DocumentationTemplateFieldOption.Create("b", "B", 2)
        ]),
        DocumentationTemplateField.Create("many", "Many", DocumentationFieldType.MultiSelect, false, 7, "Options", null,
        [
            DocumentationTemplateFieldOption.Create("x", "X", 1),
            DocumentationTemplateFieldOption.Create("y", "Y", 2)
        ])
    ];

    private static SaveDocumentationDraftUseCase NewUseCase(Infrastructure.Persistence.MuseumDbContext db, RecordingAuditWriter? audit = null) =>
        new(db, new DocumentationAvailabilityService(), audit ?? new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext());
}

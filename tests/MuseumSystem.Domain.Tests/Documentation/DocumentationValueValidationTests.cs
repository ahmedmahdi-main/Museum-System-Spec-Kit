using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class DocumentationValueValidationTests
{
    [Fact]
    public void Values_are_validated_against_field_type_required_state_and_options()
    {
        var fields = new[]
        {
            DocumentationTemplateField.Create("text", "Text", DocumentationFieldType.Text, true, 1, "Main"),
            DocumentationTemplateField.Create("multi", "Multiline", DocumentationFieldType.MultilineText, false, 2, "Main"),
            DocumentationTemplateField.Create("number", "Number", DocumentationFieldType.Number, true, 3, "Main"),
            DocumentationTemplateField.Create("date", "Date", DocumentationFieldType.Date, true, 4, "Main"),
            DocumentationTemplateField.Create("boolean", "Boolean", DocumentationFieldType.Boolean, true, 5, "Main"),
            DocumentationTemplateField.Create("single", "Single", DocumentationFieldType.SingleSelect, true, 6, "Main", options: [DocumentationTemplateFieldOption.Create("a", "A", 1)]),
            DocumentationTemplateField.Create("many", "Many", DocumentationFieldType.MultiSelect, true, 7, "Main", options: [DocumentationTemplateFieldOption.Create("a", "A", 1), DocumentationTemplateFieldOption.Create("b", "B", 2)])
        };

        var values = new Dictionary<string, DocumentationFieldValue>
        {
            ["text"] = DocumentationFieldValue.Text("value"),
            ["multi"] = DocumentationFieldValue.MultilineText("line"),
            ["number"] = DocumentationFieldValue.Number(12.5m),
            ["date"] = DocumentationFieldValue.Date(new DateOnly(2026, 8, 14)),
            ["boolean"] = DocumentationFieldValue.Boolean(false),
            ["single"] = DocumentationFieldValue.SingleSelect("a"),
            ["many"] = DocumentationFieldValue.MultiSelect(["a", "b"])
        };

        DocumentationValueRules.ValidateValues(fields, values, requireRequiredFields: true);
    }

    [Fact]
    public void Invalid_select_options_are_rejected()
    {
        var field = DocumentationTemplateField.Create("single", "Single", DocumentationFieldType.SingleSelect, true, 1, "Main", options: [DocumentationTemplateFieldOption.Create("a", "A", 1)]);

        Assert.Throws<InvalidOperationException>(() => DocumentationValueRules.ValidateValues([field], new Dictionary<string, DocumentationFieldValue> { ["single"] = DocumentationFieldValue.SingleSelect("b") }, requireRequiredFields: true));
    }
}

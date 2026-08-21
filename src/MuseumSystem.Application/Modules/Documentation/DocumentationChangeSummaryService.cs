using System.Globalization;
using System.Text.Json;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class DocumentationChangeSummaryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<DocumentationFieldChangeDto> Create(
        string previousValuesJson,
        string newValuesJson,
        IReadOnlyList<DocumentationTemplateField> fields)
    {
        var previous = DocumentationRecordMapper.ToValueDtos(previousValuesJson, fields).ToDictionary(value => value.FieldKey, StringComparer.Ordinal);
        var current = DocumentationRecordMapper.ToValueDtos(newValuesJson, fields).ToDictionary(value => value.FieldKey, StringComparer.Ordinal);

        return fields.OrderBy(field => field.DisplayOrder).ThenBy(field => field.FieldKey)
            .Where(field => !SemanticallyEqual(field, previous[field.FieldKey], current[field.FieldKey]))
            .Select(field => new DocumentationFieldChangeDto(
                field.FieldKey,
                field.Label,
                previous[field.FieldKey],
                current[field.FieldKey],
                $"{field.Label}: {DisplayValue(field, previous[field.FieldKey])} → {DisplayValue(field, current[field.FieldKey])}"))
            .ToList();
    }

    public string Serialize(IReadOnlyList<DocumentationFieldChangeDto> changes) =>
        JsonSerializer.Serialize(changes, JsonOptions);

    public IReadOnlyList<DocumentationFieldChangeDto> Deserialize(string changeSummaryJson) =>
        JsonSerializer.Deserialize<List<DocumentationFieldChangeDto>>(changeSummaryJson, JsonOptions) ?? [];

    private static bool SemanticallyEqual(DocumentationTemplateField field, DocumentationFieldValueDto left, DocumentationFieldValueDto right) =>
        field.FieldType switch
        {
            DocumentationFieldType.Text or DocumentationFieldType.MultilineText => left.TextValue == right.TextValue,
            DocumentationFieldType.Number => left.NumberValue == right.NumberValue,
            DocumentationFieldType.Date => left.DateValue == right.DateValue,
            DocumentationFieldType.Boolean => left.BooleanValue == right.BooleanValue,
            DocumentationFieldType.SingleSelect => left.OptionKey == right.OptionKey,
            DocumentationFieldType.MultiSelect => left.OptionKeys.ToHashSet(StringComparer.Ordinal).SetEquals(right.OptionKeys),
            _ => false
        };

    private static string DisplayValue(DocumentationTemplateField field, DocumentationFieldValueDto value) => field.FieldType switch
    {
        DocumentationFieldType.Text or DocumentationFieldType.MultilineText => value.TextValue ?? "—",
        DocumentationFieldType.Number => value.NumberValue?.ToString(CultureInfo.InvariantCulture) ?? "—",
        DocumentationFieldType.Date => value.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—",
        DocumentationFieldType.Boolean => value.BooleanValue is null ? "—" : value.BooleanValue.Value ? "نعم" : "لا",
        DocumentationFieldType.SingleSelect => OptionLabel(field, value.OptionKey),
        DocumentationFieldType.MultiSelect => value.OptionKeys.Count == 0
            ? "—"
            : string.Join(", ", value.OptionKeys.OrderBy(key => key, StringComparer.Ordinal).Select(key => OptionLabel(field, key))),
        _ => "—"
    };

    private static string OptionLabel(DocumentationTemplateField field, string? optionKey) =>
        optionKey is null ? "—" : field.Options.FirstOrDefault(option => option.OptionKey == optionKey)?.Label ?? optionKey;
}

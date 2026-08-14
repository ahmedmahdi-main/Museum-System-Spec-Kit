using System.Globalization;

namespace MuseumSystem.Domain.Modules.Documentation;

public sealed record DocumentationFieldValue
{
    private DocumentationFieldValue(DocumentationFieldType fieldType, string? textValue, decimal? numberValue, DateOnly? dateValue, bool? booleanValue, IReadOnlyList<string> optionKeys)
    {
        FieldType = fieldType;
        TextValue = textValue;
        NumberValue = numberValue;
        DateValue = dateValue;
        BooleanValue = booleanValue;
        OptionKeys = optionKeys;
    }

    public DocumentationFieldType FieldType { get; }
    public string? TextValue { get; }
    public decimal? NumberValue { get; }
    public DateOnly? DateValue { get; }
    public bool? BooleanValue { get; }
    public IReadOnlyList<string> OptionKeys { get; }

    public static DocumentationFieldValue Text(string? value) => new(DocumentationFieldType.Text, NormalizeOptional(value), null, null, null, []);

    public static DocumentationFieldValue MultilineText(string? value) => new(DocumentationFieldType.MultilineText, NormalizeOptional(value), null, null, null, []);

    public static DocumentationFieldValue Number(decimal? value) => new(DocumentationFieldType.Number, null, value, null, null, []);

    public static DocumentationFieldValue Date(DateOnly? value) => new(DocumentationFieldType.Date, null, null, value, null, []);

    public static DocumentationFieldValue Boolean(bool? value) => new(DocumentationFieldType.Boolean, null, null, null, value, []);

    public static DocumentationFieldValue SingleSelect(string? optionKey) =>
        new(DocumentationFieldType.SingleSelect, null, null, null, null, string.IsNullOrWhiteSpace(optionKey) ? [] : [NormalizeKey(optionKey)]);

    public static DocumentationFieldValue MultiSelect(IEnumerable<string>? optionKeys) =>
        new(DocumentationFieldType.MultiSelect, null, null, null, null, NormalizeKeys(optionKeys));

    public bool IsEmpty() => FieldType switch
    {
        DocumentationFieldType.Text or DocumentationFieldType.MultilineText => string.IsNullOrWhiteSpace(TextValue),
        DocumentationFieldType.Number => NumberValue is null,
        DocumentationFieldType.Date => DateValue is null,
        DocumentationFieldType.Boolean => BooleanValue is null,
        DocumentationFieldType.SingleSelect or DocumentationFieldType.MultiSelect => OptionKeys.Count == 0,
        _ => true
    };

    public object? ToJsonValue() => FieldType switch
    {
        DocumentationFieldType.Text or DocumentationFieldType.MultilineText => TextValue,
        DocumentationFieldType.Number => NumberValue,
        DocumentationFieldType.Date => DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DocumentationFieldType.Boolean => BooleanValue,
        DocumentationFieldType.SingleSelect => OptionKeys.Count == 0 ? null : OptionKeys[0],
        DocumentationFieldType.MultiSelect => OptionKeys,
        _ => null
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeKeys(IEnumerable<string>? optionKeys) =>
        optionKeys?.Where(key => !string.IsNullOrWhiteSpace(key)).Select(NormalizeKey).Distinct(StringComparer.Ordinal).ToArray() ?? [];

    internal static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A key is required.", nameof(value));
        }

        return value.Trim();
    }
}

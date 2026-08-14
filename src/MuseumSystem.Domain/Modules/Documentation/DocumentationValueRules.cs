using System.Text.Json;

namespace MuseumSystem.Domain.Modules.Documentation;

public static class DocumentationValueRules
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void ValidateValues(IEnumerable<DocumentationTemplateField> fields, IReadOnlyDictionary<string, DocumentationFieldValue> values, bool requireRequiredFields)
    {
        var fieldList = fields.ToList();
        var fieldKeys = fieldList.Select(field => field.FieldKey).ToHashSet(StringComparer.Ordinal);
        var unknownKey = values.Keys.FirstOrDefault(key => !fieldKeys.Contains(key));
        if (unknownKey is not null)
        {
            throw new InvalidOperationException($"Unknown documentation field '{unknownKey}'.");
        }

        foreach (var field in fieldList)
        {
            values.TryGetValue(field.FieldKey, out var value);
            if (value is null)
            {
                if (requireRequiredFields && field.IsRequired)
                {
                    throw new InvalidOperationException($"Required field '{field.FieldKey}' is missing.");
                }

                continue;
            }

            if (value.FieldType != field.FieldType)
            {
                throw new InvalidOperationException($"Field '{field.FieldKey}' has the wrong value type.");
            }

            if (requireRequiredFields && field.IsRequired && value.IsEmpty())
            {
                throw new InvalidOperationException($"Required field '{field.FieldKey}' is missing.");
            }

            ValidateOptions(field, value);
        }
    }

    public static string SerializeValues(IReadOnlyDictionary<string, DocumentationFieldValue> values)
    {
        var payload = values.ToDictionary(pair => pair.Key, pair => pair.Value.ToJsonValue(), StringComparer.Ordinal);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static void ValidateOptions(DocumentationTemplateField field, DocumentationFieldValue value)
    {
        if (!field.IsSelectField)
        {
            return;
        }

        var validOptions = field.Options.Select(option => option.OptionKey).ToHashSet(StringComparer.Ordinal);
        foreach (var optionKey in value.OptionKeys)
        {
            if (!validOptions.Contains(optionKey))
            {
                throw new InvalidOperationException($"Option '{optionKey}' is not valid for field '{field.FieldKey}'.");
            }
        }

        if (field.FieldType == DocumentationFieldType.SingleSelect && value.OptionKeys.Count > 1)
        {
            throw new InvalidOperationException($"Field '{field.FieldKey}' accepts only one option.");
        }
    }
}

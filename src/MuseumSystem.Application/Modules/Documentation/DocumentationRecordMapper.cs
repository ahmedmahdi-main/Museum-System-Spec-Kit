using System.Globalization;
using System.Text.Json;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.Documentation;

internal static class DocumentationRecordMapper
{
    public static DocumentationArtifactSummaryDto ToArtifactSummary(Artifact artifact, bool isAvailable, string? unavailableReason) => new(
        artifact.ArtifactId,
        artifact.CategoryId,
        artifact.Category?.CategoryCode ?? string.Empty,
        artifact.Category?.NameArabic ?? "Unknown category",
        artifact.MuseumNumberDisplay,
        artifact.BasicDescription,
        artifact.CurrentStatus,
        artifact.CurrentLocationId,
        artifact.CurrentLocation?.NameArabic,
        artifact.CurrentHolderType,
        artifact.CurrentHolderName,
        artifact.LastKnownStorageLocationId,
        isAvailable,
        isAvailable ? null : unavailableReason);

    public static DocumentationRecordSummaryDto ToRecordSummary(DocumentationRecord record, DocumentationTemplateVersion templateVersion) => new(
        record.DocumentationRecordId,
        record.ArtifactId,
        record.DocumentationTemplateVersionId,
        templateVersion.VersionNumber,
        record.Status,
        record.ConcurrencyToken,
        record.CreatedAt,
        record.CreatedBy,
        record.LastModifiedAt,
        record.LastModifiedBy,
        record.CompletedAt,
        record.CompletedBy,
        !string.IsNullOrWhiteSpace(record.CompletedBaselineValuesJson));

    public static DocumentationActionAvailabilityDto ToActions(
        DocumentationRecord? record,
        DocumentationTemplateVersion? activeTemplateVersion,
        bool isAvailableToDocumentation,
        DocumentationActionPermissionSet permissions)
    {
        if (record is null)
        {
            var blockedReason = activeTemplateVersion is null
                ? "No Active documentation template is available for this Artifact Category."
                : !permissions.CanCreate
                    ? "You are not authorized to create documentation records."
                    : null;

            return new DocumentationActionAvailabilityDto(
                blockedReason is null,
                blockedReason,
                false,
                false,
                null,
                false,
                null,
                false);
        }

        if (record.Status == DocumentationRecordStatus.Completed)
        {
            return new DocumentationActionAvailabilityDto(false, "The artifact already has a Completed documentation record.", false, false, null, false, null, true);
        }

        var draftBlockedReason = !permissions.CanEdit
            ? "You are not authorized to edit documentation records."
            : null;

        var completeBlockedReason = draftBlockedReason ?? (!permissions.CanComplete
            ? "You are not authorized to complete documentation records."
            : null);

        return new DocumentationActionAvailabilityDto(
            false,
            "The artifact already has a Draft documentation record.",
            draftBlockedReason is null,
            draftBlockedReason is null,
            draftBlockedReason,
            completeBlockedReason is null,
            completeBlockedReason,
            false);
    }

    public static IReadOnlyDictionary<string, DocumentationFieldValue> ToDomainValues(
        IEnumerable<DocumentationTemplateField> fields,
        IReadOnlyList<DocumentationFieldValueInputDto> inputs)
    {
        var inputByKey = inputs.ToDictionary(input => input.FieldKey, StringComparer.Ordinal);
        var values = new Dictionary<string, DocumentationFieldValue>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (!inputByKey.TryGetValue(field.FieldKey, out var input))
            {
                continue;
            }

            values[field.FieldKey] = field.FieldType switch
            {
                DocumentationFieldType.Text => DocumentationFieldValue.Text(input.TextValue),
                DocumentationFieldType.MultilineText => DocumentationFieldValue.MultilineText(input.TextValue),
                DocumentationFieldType.Number => DocumentationFieldValue.Number(input.NumberValue),
                DocumentationFieldType.Date => DocumentationFieldValue.Date(input.DateValue),
                DocumentationFieldType.Boolean => DocumentationFieldValue.Boolean(input.BooleanValue),
                DocumentationFieldType.SingleSelect => DocumentationFieldValue.SingleSelect(input.OptionKey),
                DocumentationFieldType.MultiSelect => DocumentationFieldValue.MultiSelect(input.OptionKeys),
                _ => throw new InvalidOperationException($"Unsupported field type '{field.FieldType}'.")
            };
        }

        foreach (var input in inputs.Where(input => !fields.Any(field => field.FieldKey == input.FieldKey)))
        {
            values[input.FieldKey] = DocumentationFieldValue.Text(input.TextValue);
        }

        return values;
    }

    public static IReadOnlyList<DocumentationFieldValueDto> ToValueDtos(string valuesJson, IReadOnlyList<DocumentationTemplateField> fields)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(valuesJson) ? "{}" : valuesJson);
        return fields
            .OrderBy(field => field.DisplayOrder)
            .ThenBy(field => field.FieldKey)
            .Select(field => ToValueDto(field, document.RootElement))
            .ToList();
    }

    public static List<DocumentationFieldValueInputDto> ToInputDtos(IReadOnlyList<DocumentationFieldValueDto> values) =>
        values.Select(value => new DocumentationFieldValueInputDto
        {
            FieldKey = value.FieldKey,
            TextValue = value.TextValue,
            NumberValue = value.NumberValue,
            DateValue = value.DateValue,
            BooleanValue = value.BooleanValue,
            OptionKey = value.OptionKey,
            OptionKeys = value.OptionKeys.ToList()
        }).ToList();

    private static DocumentationFieldValueDto ToValueDto(DocumentationTemplateField field, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(field.FieldKey, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return new DocumentationFieldValueDto(field.FieldKey, field.FieldType, null, null, null, null, null, []);
        }

        return field.FieldType switch
        {
            DocumentationFieldType.Text or DocumentationFieldType.MultilineText => new(field.FieldKey, field.FieldType, value.GetString(), null, null, null, null, []),
            DocumentationFieldType.Number => new(field.FieldKey, field.FieldType, null, TryGetDecimal(value), null, null, null, []),
            DocumentationFieldType.Date => new(field.FieldKey, field.FieldType, null, null, TryGetDate(value), null, null, []),
            DocumentationFieldType.Boolean => new(field.FieldKey, field.FieldType, null, null, null, value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null, null, []),
            DocumentationFieldType.SingleSelect => new(field.FieldKey, field.FieldType, null, null, null, null, value.GetString(), []),
            DocumentationFieldType.MultiSelect => new(field.FieldKey, field.FieldType, null, null, null, null, null, ReadStringArray(value)),
            _ => new DocumentationFieldValueDto(field.FieldKey, field.FieldType, null, null, null, null, null, [])
        };
    }

    private static decimal? TryGetDecimal(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

    private static DateOnly? TryGetDate(JsonElement value) =>
        value.ValueKind == JsonValueKind.String &&
        DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToList()
            : [];
}

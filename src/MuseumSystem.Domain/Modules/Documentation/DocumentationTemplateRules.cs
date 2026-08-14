namespace MuseumSystem.Domain.Modules.Documentation;

public static class DocumentationTemplateRules
{
    public static void ValidateField(DocumentationTemplateField field)
    {
        ValidateStructuralField(field);
    }

    public static void ValidateStructuralField(DocumentationTemplateField field)
    {
        if (!Enum.IsDefined(field.FieldType))
        {
            throw new InvalidOperationException("Unsupported documentation field type.");
        }

        var duplicateOptionKey = field.Options
            .GroupBy(option => option.OptionKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateOptionKey is not null)
        {
            throw new InvalidOperationException($"Duplicate option key '{duplicateOptionKey}'.");
        }

        if (!field.IsSelectField && field.Options.Count > 0)
        {
            throw new InvalidOperationException("Only select fields can define options.");
        }
    }

    public static void ValidateVersionFields(IEnumerable<DocumentationTemplateField> fields)
    {
        var fieldList = fields.ToList();
        ValidateUniqueFieldKeys(fieldList);

        foreach (var field in fieldList)
        {
            ValidateStructuralField(field);
        }
    }

    public static void ValidateVersionFieldsForActivation(IEnumerable<DocumentationTemplateField> fields)
    {
        var fieldList = fields.ToList();
        ValidateVersionFields(fieldList);

        foreach (var field in fieldList.Where(field => field.IsSelectField && field.Options.Count == 0))
        {
            throw new InvalidOperationException($"Select field '{field.FieldKey}' requires at least one option before activation.");
        }
    }

    private static void ValidateUniqueFieldKeys(IEnumerable<DocumentationTemplateField> fields)
    {
        var duplicateFieldKey = fields
            .GroupBy(field => field.FieldKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateFieldKey is not null)
        {
            throw new InvalidOperationException($"Duplicate field key '{duplicateFieldKey}'.");
        }
    }
}

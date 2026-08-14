namespace MuseumSystem.Domain.Modules.Documentation;

public sealed class DocumentationTemplateField
{
    private readonly List<DocumentationTemplateFieldOption> _options = [];

    private DocumentationTemplateField()
    {
    }

    private DocumentationTemplateField(string fieldKey, string label, DocumentationFieldType fieldType, bool isRequired, int displayOrder, string section, string? helpText, IEnumerable<DocumentationTemplateFieldOption>? options)
    {
        DocumentationTemplateFieldId = Guid.NewGuid();
        FieldKey = DocumentationFieldValue.NormalizeKey(fieldKey);
        Label = RequireText(label, nameof(label));
        FieldType = fieldType;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
        Section = RequireText(section, nameof(section));
        HelpText = NormalizeOptional(helpText);

        if (options is not null)
        {
            _options.AddRange(options);
        }

        DocumentationTemplateRules.ValidateField(this);
    }

    public Guid DocumentationTemplateFieldId { get; private set; }
    public Guid DocumentationTemplateVersionId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public DocumentationFieldType FieldType { get; private set; }
    public bool IsRequired { get; private set; }
    public int DisplayOrder { get; private set; }
    public string Section { get; private set; } = string.Empty;
    public string? HelpText { get; private set; }
    public IReadOnlyList<DocumentationTemplateFieldOption> Options => _options;

    public static DocumentationTemplateField Create(
        string fieldKey,
        string label,
        DocumentationFieldType fieldType,
        bool isRequired,
        int displayOrder,
        string section,
        string? helpText = null,
        IEnumerable<DocumentationTemplateFieldOption>? options = null) =>
        new(fieldKey, label, fieldType, isRequired, displayOrder, section, helpText, options);

    internal bool IsSelectField => FieldType is DocumentationFieldType.SingleSelect or DocumentationFieldType.MultiSelect;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

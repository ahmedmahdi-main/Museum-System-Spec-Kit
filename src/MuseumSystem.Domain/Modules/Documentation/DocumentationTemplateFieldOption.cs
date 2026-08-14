namespace MuseumSystem.Domain.Modules.Documentation;

public sealed class DocumentationTemplateFieldOption
{
    private DocumentationTemplateFieldOption()
    {
    }

    private DocumentationTemplateFieldOption(string optionKey, string label, int displayOrder)
    {
        DocumentationTemplateFieldOptionId = Guid.NewGuid();
        OptionKey = DocumentationFieldValue.NormalizeKey(optionKey);
        Label = RequireText(label, nameof(label));
        DisplayOrder = displayOrder;
    }

    public Guid DocumentationTemplateFieldOptionId { get; private set; }
    public Guid DocumentationTemplateFieldId { get; private set; }
    public string OptionKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }

    public static DocumentationTemplateFieldOption Create(string optionKey, string label, int displayOrder) => new(optionKey, label, displayOrder);

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

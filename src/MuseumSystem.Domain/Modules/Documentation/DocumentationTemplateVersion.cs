namespace MuseumSystem.Domain.Modules.Documentation;

public sealed class DocumentationTemplateVersion
{
    private readonly List<DocumentationTemplateField> _fields = [];

    private DocumentationTemplateVersion()
    {
    }

    private DocumentationTemplateVersion(int versionNumber, IEnumerable<DocumentationTemplateField>? fields, string? actor)
    {
        DocumentationTemplateVersionId = Guid.NewGuid();
        VersionNumber = versionNumber <= 0 ? throw new ArgumentOutOfRangeException(nameof(versionNumber)) : versionNumber;
        Status = DocumentationTemplateVersionStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = NormalizeOptional(actor);
        if (fields is not null)
        {
            ReplaceFields(fields, actor);
        }
    }

    public Guid DocumentationTemplateVersionId { get; private set; }
    public Guid DocumentationTemplateId { get; private set; }
    public int VersionNumber { get; private set; }
    public DocumentationTemplateVersionStatus Status { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public string? ActivatedBy { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public string? RetiredBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }
    public bool IsUsed { get; private set; }
    public int ConcurrencyToken { get; private set; }
    public IReadOnlyList<DocumentationTemplateField> Fields => _fields;

    public static DocumentationTemplateVersion CreateDraft(int versionNumber, IEnumerable<DocumentationTemplateField>? fields = null, string? actor = null) => new(versionNumber, fields, actor);

    public void ReplaceFields(IEnumerable<DocumentationTemplateField> fields, string? actor = null)
    {
        EnsureEditableDefinitions();
        var fieldList = fields.ToList();
        DocumentationTemplateRules.ValidateVersionFields(fieldList);
        _fields.Clear();
        _fields.AddRange(fieldList);
        Touch(actor);
    }

    public void Activate(string? actor = null)
    {
        if (Status != DocumentationTemplateVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft template versions can be activated.");
        }

        DocumentationTemplateRules.ValidateVersionFieldsForActivation(_fields);
        Status = DocumentationTemplateVersionStatus.Active;
        ActivatedAt = DateTimeOffset.UtcNow;
        ActivatedBy = NormalizeOptional(actor);
        Touch(actor);
    }

    public void Retire(string? actor = null)
    {
        if (Status != DocumentationTemplateVersionStatus.Active)
        {
            throw new InvalidOperationException("Only Active template versions can be retired.");
        }

        Status = DocumentationTemplateVersionStatus.Retired;
        RetiredAt = DateTimeOffset.UtcNow;
        RetiredBy = NormalizeOptional(actor);
        Touch(actor);
    }

    public void MarkUsed(string? actor = null)
    {
        IsUsed = true;
        Touch(actor);
    }

    private void EnsureEditableDefinitions()
    {
        if (Status != DocumentationTemplateVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft template versions can edit field definitions.");
        }

        if (IsUsed)
        {
            throw new InvalidOperationException("Used template versions are immutable except retirement status.");
        }
    }

    private void Touch(string? actor)
    {
        ConcurrencyToken++;
        LastModifiedAt = DateTimeOffset.UtcNow;
        LastModifiedBy = NormalizeOptional(actor);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

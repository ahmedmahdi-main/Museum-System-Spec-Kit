namespace MuseumSystem.Domain.Modules.Documentation;

public sealed class DocumentationTemplate
{
    private readonly List<DocumentationTemplateVersion> _versions = [];

    private DocumentationTemplate()
    {
    }

    private DocumentationTemplate(Guid artifactCategoryId, string name, string? description, string? actor)
    {
        DocumentationTemplateId = Guid.NewGuid();
        ArtifactCategoryId = artifactCategoryId == Guid.Empty ? throw new ArgumentException("Artifact category is required.", nameof(artifactCategoryId)) : artifactCategoryId;
        Name = RequireText(name, nameof(name));
        Description = NormalizeOptional(description);
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = NormalizeOptional(actor);
    }

    public Guid DocumentationTemplateId { get; private set; }
    public Guid ArtifactCategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }
    public IReadOnlyList<DocumentationTemplateVersion> Versions => _versions;

    public static DocumentationTemplate Create(Guid artifactCategoryId, string name, string? description = null, string? actor = null) => new(artifactCategoryId, name, description, actor);

    public DocumentationTemplateVersion CreateDraftVersion(IEnumerable<DocumentationTemplateField>? fields = null, string? actor = null)
    {
        var version = DocumentationTemplateVersion.CreateDraft(_versions.Count == 0 ? 1 : _versions.Max(v => v.VersionNumber) + 1, fields, actor);
        _versions.Add(version);
        Touch(actor);
        return version;
    }

    public void ActivateVersion(DocumentationTemplateVersion version, string? actor = null)
    {
        if (!_versions.Contains(version))
        {
            throw new InvalidOperationException("Version does not belong to this template.");
        }

        foreach (var activeVersion in _versions.Where(candidate => candidate.Status == DocumentationTemplateVersionStatus.Active && candidate != version))
        {
            activeVersion.Retire(actor);
        }

        version.Activate(actor);
        Touch(actor);
    }

    public void RetireVersion(DocumentationTemplateVersion version, string? actor = null)
    {
        if (!_versions.Contains(version))
        {
            throw new InvalidOperationException("Version does not belong to this template.");
        }

        version.Retire(actor);
        Touch(actor);
    }

    private void Touch(string? actor)
    {
        LastModifiedAt = DateTimeOffset.UtcNow;
        LastModifiedBy = NormalizeOptional(actor);
    }

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

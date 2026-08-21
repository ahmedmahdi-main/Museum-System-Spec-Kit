namespace MuseumSystem.Domain.Modules.Documentation;

public sealed class DocumentationRecord
{
    private readonly List<DocumentationRevision> _revisions = [];

    private DocumentationRecord()
    {
    }

    private DocumentationRecord(Guid artifactId, DocumentationTemplateVersion templateVersion, string? actor)
    {
        if (templateVersion.Status != DocumentationTemplateVersionStatus.Active)
        {
            throw new InvalidOperationException("Documentation records can only be created from an Active template version.");
        }

        DocumentationRecordId = Guid.NewGuid();
        ArtifactId = artifactId == Guid.Empty ? throw new ArgumentException("Artifact is required.", nameof(artifactId)) : artifactId;
        DocumentationTemplateVersionId = templateVersion.DocumentationTemplateVersionId;
        DocumentationTemplateVersion = templateVersion;
        Status = DocumentationRecordStatus.Draft;
        ValuesJson = "{}";
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = NormalizeOptional(actor);
        templateVersion.MarkUsed(actor);
    }

    public Guid DocumentationRecordId { get; private set; }
    public Guid ArtifactId { get; private set; }
    public Guid DocumentationTemplateVersionId { get; private set; }
    public DocumentationTemplateVersion? DocumentationTemplateVersion { get; private set; }
    public DocumentationRecordStatus Status { get; private set; }
    public string ValuesJson { get; private set; } = "{}";
    public string? CompletedBaselineValuesJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public int ConcurrencyToken { get; private set; }
    public IReadOnlyList<DocumentationRevision> Revisions => _revisions;

    public static DocumentationRecord Create(Guid artifactId, DocumentationTemplateVersion templateVersion, string? actor = null) => new(artifactId, templateVersion, actor);

    public void SaveDraft(IReadOnlyDictionary<string, DocumentationFieldValue> values, DocumentationTemplateVersion templateVersion, string? actor = null)
    {
        EnsureDraft();
        EnsureTemplateVersion(templateVersion);
        DocumentationValueRules.ValidateValues(templateVersion.Fields, values, requireRequiredFields: false);
        ValuesJson = DocumentationValueRules.SerializeValues(values);
        Touch(actor);
    }

    public void Complete(IReadOnlyDictionary<string, DocumentationFieldValue> values, DocumentationTemplateVersion templateVersion, string? actor = null)
    {
        EnsureDraft();
        EnsureTemplateVersion(templateVersion);
        DocumentationValueRules.ValidateValues(templateVersion.Fields, values, requireRequiredFields: true);
        ValuesJson = DocumentationValueRules.SerializeValues(values);
        CompletedBaselineValuesJson = ValuesJson;
        Status = DocumentationRecordStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        CompletedBy = NormalizeOptional(actor);
        Touch(actor);
    }

    public DocumentationRevision CorrectCompleted(IReadOnlyDictionary<string, DocumentationFieldValue> values, DocumentationTemplateVersion templateVersion, string changeSummaryJson, string reason, string? actor = null)
    {
        var revision = PrepareCompletedCorrection(values, templateVersion, changeSummaryJson, reason, actor);
        AddPreparedCorrectionRevision(revision);
        return revision;
    }

    internal DocumentationRevision PrepareCompletedCorrection(IReadOnlyDictionary<string, DocumentationFieldValue> values, DocumentationTemplateVersion templateVersion, string changeSummaryJson, string reason, string? actor = null)
    {
        if (Status != DocumentationRecordStatus.Completed)
        {
            throw new InvalidOperationException("Only completed records can be corrected.");
        }

        EnsureTemplateVersion(templateVersion);
        DocumentationValueRules.ValidateValues(templateVersion.Fields, values, requireRequiredFields: true);
        var previousValuesJson = ValuesJson;
        var newValuesJson = DocumentationValueRules.SerializeValues(values);
        var revision = DocumentationRevision.Create(
            DocumentationRecordId,
            DocumentationTemplateVersionId,
            NextCorrectionRevisionNumber(),
            previousValuesJson,
            newValuesJson,
            changeSummaryJson,
            reason,
            actor);

        ValuesJson = newValuesJson;
        Touch(actor);
        return revision;
    }

    internal void AddPreparedCorrectionRevision(DocumentationRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.DocumentationRecordId != DocumentationRecordId || revision.TemplateVersionId != DocumentationTemplateVersionId)
        {
            throw new InvalidOperationException("The correction revision does not belong to this documentation record.");
        }

        if (revision.RevisionNumber != NextCorrectionRevisionNumber())
        {
            throw new InvalidOperationException("The correction revision number is not the next expected revision.");
        }

        _revisions.Add(revision);
    }

    private int NextCorrectionRevisionNumber() => _revisions.Count == 0 ? 2 : _revisions.Max(revision => revision.RevisionNumber) + 1;

    private void EnsureDraft()
    {
        if (Status != DocumentationRecordStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft records can be changed through this operation.");
        }
    }

    private void EnsureTemplateVersion(DocumentationTemplateVersion templateVersion)
    {
        if (DocumentationTemplateVersionId != templateVersion.DocumentationTemplateVersionId)
        {
            throw new InvalidOperationException("Documentation values must use the record's original template version.");
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

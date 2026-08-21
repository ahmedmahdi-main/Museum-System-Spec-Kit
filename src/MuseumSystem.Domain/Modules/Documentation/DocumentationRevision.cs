namespace MuseumSystem.Domain.Modules.Documentation;

public sealed class DocumentationRevision
{
    private DocumentationRevision()
    {
    }

    private DocumentationRevision(Guid documentationRecordId, Guid templateVersionId, int revisionNumber, string previousValuesJson, string newValuesJson, string changeSummaryJson, string reason, string? actor)
    {
        if (revisionNumber < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Correction revisions start at Revision 2.");
        }

        DocumentationRevisionId = Guid.NewGuid();
        DocumentationRecordId = documentationRecordId;
        TemplateVersionId = templateVersionId;
        RevisionNumber = revisionNumber;
        PreviousValuesJson = RequireJson(previousValuesJson, nameof(previousValuesJson));
        NewValuesJson = RequireJson(newValuesJson, nameof(newValuesJson));
        ChangeSummaryJson = RequireJson(changeSummaryJson, nameof(changeSummaryJson));
        Reason = RequireText(reason, nameof(reason));
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = NormalizeOptional(actor);
    }

    public Guid DocumentationRevisionId { get; private set; }
    public Guid DocumentationRecordId { get; private set; }
    public Guid TemplateVersionId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string PreviousValuesJson { get; private set; } = "{}";
    public string NewValuesJson { get; private set; } = "{}";
    public string ChangeSummaryJson { get; private set; } = "{}";
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }

    internal static DocumentationRevision Create(Guid documentationRecordId, Guid templateVersionId, int revisionNumber, string previousValuesJson, string newValuesJson, string changeSummaryJson, string reason, string? actor = null) =>
        new(documentationRecordId, templateVersionId, revisionNumber, previousValuesJson, newValuesJson, changeSummaryJson, reason, actor);

    private static string RequireJson(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A JSON value is required.", paramName);
        }

        return value;
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

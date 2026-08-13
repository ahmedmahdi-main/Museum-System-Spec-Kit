using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public enum DocumentedCorrectionSourceType
{
    Reconciliation = 1,
    AdministrativeCorrection = 2
}

public enum DocumentedCorrectionType
{
    LocationCorrection = 1,
    HolderCorrection = 2,
    StatusCorrection = 3
}

public sealed class DocumentedCorrection
{
    private DocumentedCorrection()
    {
    }

    private DocumentedCorrection(Guid artifactId, DocumentedCorrectionSourceType sourceType, Guid? sourceId, DocumentedCorrectionType correctionType, string previousValueSummary, string newValueSummary, string reason)
    {
        CorrectionId = Guid.NewGuid();
        ArtifactId = artifactId;
        SourceType = sourceType;
        SourceId = sourceId;
        CorrectionType = correctionType;
        PreviousValueSummary = RequireText(previousValueSummary, nameof(previousValueSummary));
        NewValueSummary = RequireText(newValueSummary, nameof(newValueSummary));
        Reason = RequireText(reason, nameof(reason));
        CorrectedAt = DateTimeOffset.UtcNow;
    }

    public Guid CorrectionId { get; private set; }
    public Guid ArtifactId { get; private set; }
    public Artifact? Artifact { get; private set; }
    public DocumentedCorrectionSourceType SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public DocumentedCorrectionType CorrectionType { get; private set; }
    public string PreviousValueSummary { get; private set; } = string.Empty;
    public string NewValueSummary { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CorrectedAt { get; private set; }
    public string? CorrectedBy { get; private set; }

    public static DocumentedCorrection Create(Guid artifactId, DocumentedCorrectionSourceType sourceType, Guid? sourceId, DocumentedCorrectionType correctionType, string previousValueSummary, string newValueSummary, string reason) =>
        new(artifactId, sourceType, sourceId, correctionType, previousValueSummary, newValueSummary, reason);

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

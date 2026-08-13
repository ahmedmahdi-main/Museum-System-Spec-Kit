using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public enum ReconciliationResultType
{
    Matched = 1,
    Missing = 2,
    Extra = 3,
    Conflict = 4,
    NeedsReview = 5
}

public sealed class ReconciliationResult
{
    private ReconciliationResult()
    {
    }

    private ReconciliationResult(Guid reconciliationSessionId, Guid? artifactId, string? observedMuseumNumber, Guid? expectedLocationId, Guid? observedLocationId, ReconciliationResultType resultType, string issueDescription)
    {
        ReconciliationResultId = Guid.NewGuid();
        ReconciliationSessionId = reconciliationSessionId;
        ArtifactId = artifactId;
        ObservedMuseumNumber = NormalizeOptional(observedMuseumNumber);
        ExpectedLocationId = expectedLocationId;
        ObservedLocationId = observedLocationId;
        ResultType = resultType;
        IssueDescription = RequireText(issueDescription, nameof(issueDescription));
    }

    public Guid ReconciliationResultId { get; private set; }
    public Guid ReconciliationSessionId { get; private set; }
    public ReconciliationSession? ReconciliationSession { get; private set; }
    public Guid? ArtifactId { get; private set; }
    public Artifact? Artifact { get; private set; }
    public string? ObservedMuseumNumber { get; private set; }
    public Guid? ExpectedLocationId { get; private set; }
    public Location? ExpectedLocation { get; private set; }
    public Guid? ObservedLocationId { get; private set; }
    public Location? ObservedLocation { get; private set; }
    public ReconciliationResultType ResultType { get; private set; }
    public string IssueDescription { get; private set; } = string.Empty;
    public bool IsConfirmed { get; private set; }

    public static ReconciliationResult Create(Guid reconciliationSessionId, Guid? artifactId, string? observedMuseumNumber, Guid? expectedLocationId, Guid? observedLocationId, ReconciliationResultType resultType, string issueDescription) =>
        new(reconciliationSessionId, artifactId, observedMuseumNumber, expectedLocationId, observedLocationId, resultType, issueDescription);

    public void ConfirmConflict()
    {
        if (ResultType != ReconciliationResultType.Conflict)
        {
            throw new InvalidOperationException("Only conflict results can be confirmed for documented correction.");
        }

        IsConfirmed = true;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public enum ReconciliationSessionStatus
{
    Draft = 1,
    Completed = 2,
    Reviewed = 3
}

public sealed class ReconciliationSession
{
    private readonly List<ReconciliationResult> _results = [];

    private ReconciliationSession()
    {
    }

    private ReconciliationSession(Location location, string? note)
    {
        if (!location.IsActive || location.LocationType != LocationType.Storage)
        {
            throw new InvalidOperationException("Reconciliation requires an active storage location.");
        }

        ReconciliationSessionId = Guid.NewGuid();
        LocationId = location.LocationId;
        Location = location;
        StartedAt = DateTimeOffset.UtcNow;
        Status = ReconciliationSessionStatus.Draft;
        Note = NormalizeOptional(note);
    }

    public Guid ReconciliationSessionId { get; private set; }
    public Guid LocationId { get; private set; }
    public Location? Location { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public string? StartedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public ReconciliationSessionStatus Status { get; private set; }
    public string? Note { get; private set; }
    public IReadOnlyCollection<ReconciliationResult> Results => _results.AsReadOnly();

    public static ReconciliationSession Start(Location location, string? note = null) => new(location, note);

    public void ReplaceResults(IEnumerable<ReconciliationResult> results)
    {
        if (Status == ReconciliationSessionStatus.Reviewed)
        {
            throw new InvalidOperationException("Reviewed reconciliation sessions cannot be changed.");
        }

        _results.Clear();
        _results.AddRange(results);
        Status = ReconciliationSessionStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkReviewed()
    {
        if (Status != ReconciliationSessionStatus.Completed)
        {
            throw new InvalidOperationException("Only completed reconciliation sessions can be reviewed.");
        }

        Status = ReconciliationSessionStatus.Reviewed;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

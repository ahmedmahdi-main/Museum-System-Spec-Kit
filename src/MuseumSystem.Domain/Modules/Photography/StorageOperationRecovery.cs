namespace MuseumSystem.Domain.Modules.Photography;

public sealed class StorageOperationRecovery
{
    private StorageOperationRecovery()
    {
    }

    private StorageOperationRecovery(StorageOperationRecoveryType operationType, Guid artifactId, IReadOnlyCollection<ImageStorageObjectKey> objectKeys, string failureSummary, Guid? artifactImageId)
    {
        StorageOperationRecoveryId = Guid.NewGuid();
        OperationType = operationType;
        ArtifactId = RequireGuid(artifactId, nameof(artifactId));
        ArtifactImageId = artifactImageId == Guid.Empty ? null : artifactImageId;
        ObjectKeys = [.. objectKeys ?? throw new ArgumentNullException(nameof(objectKeys))];
        if (ObjectKeys.Count == 0)
        {
            throw new ArgumentException("At least one object key is required.", nameof(objectKeys));
        }

        FailureSummary = RequireText(failureSummary, nameof(failureSummary));
        Status = StorageOperationRecoveryStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid StorageOperationRecoveryId { get; private set; }
    public StorageOperationRecoveryType OperationType { get; private set; }
    public Guid ArtifactId { get; private set; }
    public Guid? ArtifactImageId { get; private set; }
    public IReadOnlyCollection<ImageStorageObjectKey> ObjectKeys { get; private set; } = [];
    public StorageOperationRecoveryStatus Status { get; private set; }
    public string FailureSummary { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastAttemptedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public int ConcurrencyToken { get; private set; }

    public static StorageOperationRecovery Create(StorageOperationRecoveryType operationType, Guid artifactId, IReadOnlyCollection<ImageStorageObjectKey> objectKeys, string failureSummary, Guid? artifactImageId = null) =>
        new(operationType, artifactId, objectKeys, failureSummary, artifactImageId);

    public void MarkRetrying(DateTimeOffset attemptedAt)
    {
        EnsureOpen();
        Status = StorageOperationRecoveryStatus.Retrying;
        LastAttemptedAt = attemptedAt;
        Touch();
    }

    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        EnsureOpen();
        Status = StorageOperationRecoveryStatus.Resolved;
        ResolvedAt = resolvedAt;
        LastAttemptedAt = resolvedAt;
        Touch();
    }

    public void MarkFailedNeedsAttention(DateTimeOffset attemptedAt, string failureSummary)
    {
        EnsureOpen();
        Status = StorageOperationRecoveryStatus.FailedNeedsAttention;
        LastAttemptedAt = attemptedAt;
        FailureSummary = RequireText(failureSummary, nameof(failureSummary));
        Touch();
    }

    private void EnsureOpen()
    {
        if (Status == StorageOperationRecoveryStatus.Resolved)
        {
            throw new InvalidOperationException("Resolved recovery records cannot be changed.");
        }
    }

    private void Touch() => ConcurrencyToken++;

    private static Guid RequireGuid(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("A value is required.", paramName) : value;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

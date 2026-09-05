namespace MuseumSystem.Domain.Modules.Photography;

public sealed class StorageOperationRecovery
{
    private StorageOperationRecovery()
    {
    }

    private StorageOperationRecovery(
        StorageOperationRecoveryType operationType,
        Guid artifactId,
        IReadOnlyCollection<ImageStorageObjectKey> objectKeys,
        string failureSummary,
        Guid? artifactImageId,
        Guid? photographyUploadOperationId,
        Guid? photographyUploadFileOutcomeId)
    {
        StorageOperationRecoveryId = Guid.NewGuid();
        OperationType = operationType;
        ArtifactId = RequireGuid(artifactId, nameof(artifactId));
        ArtifactImageId = artifactImageId == Guid.Empty ? null : artifactImageId;
        PhotographyUploadOperationId = photographyUploadOperationId == Guid.Empty ? null : photographyUploadOperationId;
        PhotographyUploadFileOutcomeId = photographyUploadFileOutcomeId == Guid.Empty ? null : photographyUploadFileOutcomeId;
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

    /// <summary>
    /// Historical durable correlation to the upload operation that produced this recovery row, when known.
    /// Intentionally not a relational FK: recovery rows are retained as operational/audit history even after
    /// the correlated <see cref="PhotographyUploadOperation"/> is purged by idempotency retention, so a real
    /// FK would force either deleting recovery history or nulling historical correlation. Legacy rows created
    /// before this correlation existed remain valid with a null value; it is never inferred after the fact.
    /// </summary>
    public Guid? PhotographyUploadOperationId { get; private set; }

    /// <summary>
    /// Historical durable correlation to the upload file outcome that produced this recovery row, when known.
    /// See <see cref="PhotographyUploadOperationId"/> for why this is not a relational FK.
    /// </summary>
    public Guid? PhotographyUploadFileOutcomeId { get; private set; }

    public IReadOnlyCollection<ImageStorageObjectKey> ObjectKeys { get; private set; } = [];
    public StorageOperationRecoveryStatus Status { get; private set; }
    public string FailureSummary { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastAttemptedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public int ConcurrencyToken { get; private set; }

    public static StorageOperationRecovery Create(
        StorageOperationRecoveryType operationType,
        Guid artifactId,
        IReadOnlyCollection<ImageStorageObjectKey> objectKeys,
        string failureSummary,
        Guid? artifactImageId = null,
        Guid? photographyUploadOperationId = null,
        Guid? photographyUploadFileOutcomeId = null) =>
        new(operationType, artifactId, objectKeys, failureSummary, artifactImageId, photographyUploadOperationId, photographyUploadFileOutcomeId);

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

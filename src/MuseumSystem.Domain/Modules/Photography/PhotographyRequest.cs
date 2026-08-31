namespace MuseumSystem.Domain.Modules.Photography;

public enum PhotographyRequestStatus
{
    Pending = 1,
    Completed = 2,
    Cancelled = 3
}

public sealed class PhotographyRequest
{
    private PhotographyRequest()
    {
    }

    private PhotographyRequest(Guid artifactId, PhotographyPurpose purpose, string requestedByUserId, DateTimeOffset requestedAt)
    {
        PhotographyRequestId = Guid.NewGuid();
        ArtifactId = RequireGuid(artifactId, nameof(artifactId));
        Purpose = RequirePurpose(purpose, nameof(purpose));
        RequestedByUserId = RequireText(requestedByUserId, nameof(requestedByUserId));
        RequestedAt = RequireTimestamp(requestedAt, nameof(requestedAt));
        Status = PhotographyRequestStatus.Pending;
    }

    public Guid PhotographyRequestId { get; private set; }
    public Guid ArtifactId { get; private set; }
    public PhotographyPurpose Purpose { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private set; }
    public PhotographyRequestStatus Status { get; private set; }
    public Guid? FulfillingPhotographySetId { get; private set; }
    public string? CompletedByUserId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CancelledByUserId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public int ConcurrencyToken { get; private set; }

    public static PhotographyRequest Create(
        Guid artifactId,
        PhotographyPurpose purpose,
        string requestedByUserId,
        DateTimeOffset requestedAt) =>
        new(artifactId, purpose, requestedByUserId, requestedAt);

    public void Complete(
        Guid fulfillingPhotographySetId,
        Guid fulfillingSetArtifactId,
        PhotographyPurpose fulfillingSetPurpose,
        bool fulfillingSetHasAvailableImage,
        string completedByUserId,
        DateTimeOffset completedAt)
    {
        EnsurePending("Only pending photography requests can be completed.");

        var setId = RequireGuid(fulfillingPhotographySetId, nameof(fulfillingPhotographySetId));
        if (RequireGuid(fulfillingSetArtifactId, nameof(fulfillingSetArtifactId)) != ArtifactId)
        {
            throw new InvalidOperationException("Fulfilling photography set must belong to the requested artifact.");
        }

        if (RequirePurpose(fulfillingSetPurpose, nameof(fulfillingSetPurpose)) != Purpose)
        {
            throw new InvalidOperationException("Fulfilling photography set must have the requested purpose.");
        }

        if (!fulfillingSetHasAvailableImage)
        {
            throw new InvalidOperationException("Fulfilling photography set must contain at least one available image.");
        }

        var actor = RequireText(completedByUserId, nameof(completedByUserId));
        var timestamp = RequireTimestamp(completedAt, nameof(completedAt));

        FulfillingPhotographySetId = setId;
        CompletedByUserId = actor;
        CompletedAt = timestamp;
        Status = PhotographyRequestStatus.Completed;
        Touch();
    }

    public void Cancel(string cancelledByUserId, DateTimeOffset cancelledAt, bool actorHasManageAuthority)
    {
        EnsurePending("Only pending photography requests can be cancelled.");

        var actor = RequireText(cancelledByUserId, nameof(cancelledByUserId));
        if (!actorHasManageAuthority && !string.Equals(actor, RequestedByUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Only the requester or a manager can cancel this photography request.");
        }

        CancelledByUserId = actor;
        CancelledAt = RequireTimestamp(cancelledAt, nameof(cancelledAt));
        Status = PhotographyRequestStatus.Cancelled;
        Touch();
    }

    private void EnsurePending(string message)
    {
        if (Status != PhotographyRequestStatus.Pending)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Touch() => ConcurrencyToken++;

    private static Guid RequireGuid(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("A value is required.", paramName) : value;

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string paramName) =>
        value == default ? throw new ArgumentException("A value is required.", paramName) : value;

    private static PhotographyPurpose RequirePurpose(PhotographyPurpose value, string paramName) =>
        value is PhotographyPurpose.GeneralDocumentation
            or PhotographyPurpose.PreMaintenance
            or PhotographyPurpose.DuringMaintenance
            or PhotographyPurpose.PostMaintenance
            ? value
            : throw new ArgumentOutOfRangeException(paramName, "Unsupported photography purpose.");

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

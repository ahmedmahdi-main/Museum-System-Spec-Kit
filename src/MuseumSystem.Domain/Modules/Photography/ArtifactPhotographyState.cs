namespace MuseumSystem.Domain.Modules.Photography;

public sealed class ArtifactPhotographyState
{
    private ArtifactPhotographyState()
    {
    }

    private ArtifactPhotographyState(Guid artifactId)
    {
        ArtifactId = artifactId == Guid.Empty ? throw new ArgumentException("Artifact is required.", nameof(artifactId)) : artifactId;
    }

    public Guid ArtifactId { get; private set; }
    public Guid? PrimaryImageId { get; private set; }
    public int ConcurrencyToken { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedByUserId { get; private set; }

    public static ArtifactPhotographyState Create(Guid artifactId) => new(artifactId);

    public void SetPrimaryImage(Guid artifactImageId, string? actorUserId = null)
    {
        if (artifactImageId == Guid.Empty)
        {
            throw new ArgumentException("Primary image is required.", nameof(artifactImageId));
        }

        PrimaryImageId = artifactImageId;
        Touch(actorUserId);
    }

    public void ClearPrimaryImage(string? actorUserId = null)
    {
        PrimaryImageId = null;
        Touch(actorUserId);
    }

    private void Touch(string? actorUserId)
    {
        ConcurrencyToken++;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = NormalizeOptional(actorUserId);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

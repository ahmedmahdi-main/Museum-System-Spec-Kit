namespace MuseumSystem.Domain.Modules.Photography;

public sealed class PhotographySet
{
    private PhotographySet()
    {
    }

    private PhotographySet(Guid artifactId, PhotographyPurpose purpose, DateOnly photographyDate, string photographerUserId, string? createdByUserId)
    {
        PhotographySetId = Guid.NewGuid();
        ArtifactId = RequireGuid(artifactId, nameof(artifactId));
        Purpose = PhotographyEnumValidation.RequireDefined(purpose, nameof(purpose));
        PhotographyDate = RequireDate(photographyDate, nameof(photographyDate));
        PhotographerUserId = RequireText(photographerUserId, nameof(photographerUserId));
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedByUserId = NormalizeOptional(createdByUserId);
    }

    public Guid PhotographySetId { get; private set; }
    public Guid ArtifactId { get; private set; }
    public PhotographyPurpose Purpose { get; private set; }
    public DateOnly PhotographyDate { get; private set; }
    public string PhotographerUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public int ConcurrencyToken { get; private set; }

    public static PhotographySet Create(Guid artifactId, PhotographyPurpose purpose, DateOnly photographyDate, string photographerUserId, string? createdByUserId = null) =>
        new(artifactId, purpose, photographyDate, photographerUserId, createdByUserId);

    private static Guid RequireGuid(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("A value is required.", paramName) : value;

    private static DateOnly RequireDate(DateOnly value, string paramName) =>
        value == default ? throw new ArgumentException("A value is required.", paramName) : value;

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

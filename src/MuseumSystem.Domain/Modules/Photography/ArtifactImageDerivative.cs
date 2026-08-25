namespace MuseumSystem.Domain.Modules.Photography;

public sealed class ArtifactImageDerivative
{
    private ArtifactImageDerivative()
    {
    }

    private ArtifactImageDerivative(Guid artifactImageId, ImageDerivativeKind kind, ImageStorageObjectKey objectKey, string contentType, long fileSizeBytes, int pixelWidth, int pixelHeight)
    {
        ArtifactImageDerivativeId = Guid.NewGuid();
        ArtifactImageId = RequireGuid(artifactImageId, nameof(artifactImageId));
        Kind = kind;
        ObjectKey = objectKey ?? throw new ArgumentNullException(nameof(objectKey));
        ContentType = RequireText(contentType, nameof(contentType));
        FileSizeBytes = RequirePositive(fileSizeBytes, nameof(fileSizeBytes));
        PixelWidth = RequirePositive(pixelWidth, nameof(pixelWidth));
        PixelHeight = RequirePositive(pixelHeight, nameof(pixelHeight));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ArtifactImageDerivativeId { get; private set; }
    public Guid ArtifactImageId { get; private set; }
    public ImageDerivativeKind Kind { get; private set; }
    public ImageStorageObjectKey ObjectKey { get; private set; } = ImageStorageObjectKey.Create("uninitialized");
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ArtifactImageDerivative Create(Guid artifactImageId, ImageDerivativeKind kind, ImageStorageObjectKey objectKey, string contentType, long fileSizeBytes, int pixelWidth, int pixelHeight) =>
        new(artifactImageId, kind, objectKey, contentType, fileSizeBytes, pixelWidth, pixelHeight);

    private static Guid RequireGuid(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("A value is required.", paramName) : value;

    private static int RequirePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Value must be greater than zero.");
        }

        return value;
    }

    private static long RequirePositive(long value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Value must be greater than zero.");
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
}

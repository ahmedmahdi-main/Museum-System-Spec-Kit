namespace MuseumSystem.Domain.Modules.Photography;

public sealed class ArtifactImage
{
    private readonly List<ArtifactImageDerivative> _derivatives = [];

    private ArtifactImage()
    {
    }

    private ArtifactImage(
        Guid artifactId,
        Guid photographySetId,
        ImageStorageObjectKey originalObjectKey,
        string originalFilename,
        string contentType,
        long fileSizeBytes,
        int pixelWidth,
        int pixelHeight,
        string uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        ArtifactImageId = Guid.NewGuid();
        ArtifactId = RequireGuid(artifactId, nameof(artifactId));
        PhotographySetId = RequireGuid(photographySetId, nameof(photographySetId));
        OriginalObjectKey = originalObjectKey ?? throw new ArgumentNullException(nameof(originalObjectKey));
        OriginalFilename = RequireText(originalFilename, nameof(originalFilename));
        ContentType = RequireText(contentType, nameof(contentType));
        FileSizeBytes = RequirePositive(fileSizeBytes, nameof(fileSizeBytes));
        PixelWidth = RequirePositive(pixelWidth, nameof(pixelWidth));
        PixelHeight = RequirePositive(pixelHeight, nameof(pixelHeight));
        UploadedByUserId = RequireText(uploadedByUserId, nameof(uploadedByUserId));
        UploadedAt = uploadedAt;
        Status = ArtifactImageStatus.Available;
    }

    public Guid ArtifactImageId { get; private set; }
    public Guid ArtifactId { get; private set; }
    public Guid PhotographySetId { get; private set; }
    public ImageStorageObjectKey OriginalObjectKey { get; private set; } = ImageStorageObjectKey.Create("uninitialized");
    public string OriginalFilename { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public string UploadedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }
    public string? Caption { get; private set; }
    public ArtifactImageStatus Status { get; private set; }
    public string? DeletionRequestedByUserId { get; private set; }
    public DateTimeOffset? DeletionRequestedAt { get; private set; }
    public string? DeletedByUserId { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public ArtifactImageDeletionMode? DeletionMode { get; private set; }
    public string? DeletionReason { get; private set; }
    public int ConcurrencyToken { get; private set; }
    public IReadOnlyCollection<ArtifactImageDerivative> Derivatives => _derivatives.AsReadOnly();

    public static ArtifactImage Create(
        Guid artifactId,
        Guid photographySetId,
        ImageStorageObjectKey originalObjectKey,
        string originalFilename,
        string contentType,
        long fileSizeBytes,
        int pixelWidth,
        int pixelHeight,
        string uploadedByUserId,
        DateTimeOffset uploadedAt) =>
        new(
            artifactId,
            photographySetId,
            originalObjectKey,
            originalFilename,
            contentType,
            fileSizeBytes,
            pixelWidth,
            pixelHeight,
            uploadedByUserId,
            uploadedAt);

    public void AddDerivative(ArtifactImageDerivative derivative)
    {
        if (Status != ArtifactImageStatus.Available)
        {
            throw new InvalidOperationException("Derivatives can only be added to available images.");
        }

        ArgumentNullException.ThrowIfNull(derivative);
        if (derivative.ArtifactImageId != ArtifactImageId)
        {
            throw new InvalidOperationException("Derivative must belong to this artifact image.");
        }

        _derivatives.Add(derivative);
        Touch();
    }

    public void UpdateCaption(string? caption)
    {
        if (Status != ArtifactImageStatus.Available)
        {
            throw new InvalidOperationException("Only available images can be edited.");
        }

        Caption = NormalizeOptional(caption);
        Touch();
    }

    public void MarkDeletePending(
        ArtifactImageDeletionMode mode,
        string deletionRequestedByUserId,
        DateTimeOffset deletionRequestedAt,
        string? deletionReason = null)
    {
        if (Status != ArtifactImageStatus.Available)
        {
            throw new InvalidOperationException("Only available images can be marked for deletion.");
        }

        var validatedMode = PhotographyEnumValidation.RequireDefined(mode, nameof(mode));
        if (!PhotographyRules.HasRequiredDeletionReason(validatedMode, deletionReason))
        {
            throw new ArgumentException("Privileged deletion requires a reason.", nameof(deletionReason));
        }

        var normalizedDeletionRequestedByUserId = RequireText(deletionRequestedByUserId, nameof(deletionRequestedByUserId));

        Status = ArtifactImageStatus.DeletePending;
        DeletionMode = validatedMode;
        DeletionReason = NormalizeOptional(deletionReason);
        DeletionRequestedByUserId = normalizedDeletionRequestedByUserId;
        DeletionRequestedAt = deletionRequestedAt;
        Touch();
    }

    public void MarkDeleted(ArtifactImageDeletionMode mode)
    {
        var validatedMode = PhotographyEnumValidation.RequireDefined(mode, nameof(mode));

        if (Status == ArtifactImageStatus.Deleted)
        {
            throw new InvalidOperationException("Image is already deleted.");
        }

        if (Status != ArtifactImageStatus.DeletePending)
        {
            throw new InvalidOperationException("Image must be pending deletion before deletion can be finalized.");
        }

        if (DeletionMode != validatedMode)
        {
            throw new InvalidOperationException("Deletion finalization must use the deletion mode recorded by the pending intent.");
        }

        if (!PhotographyRules.HasRequiredDeletionReason(validatedMode, DeletionReason))
        {
            throw new InvalidOperationException("Privileged deletion requires a reason.");
        }

        if (string.IsNullOrWhiteSpace(DeletionRequestedByUserId) || DeletionRequestedAt is null)
        {
            throw new InvalidOperationException("Deletion intent attribution is required before deletion can be finalized.");
        }

        Status = ArtifactImageStatus.Deleted;
        DeletedByUserId = DeletionRequestedByUserId;
        DeletedAt = DeletionRequestedAt;
        Touch();
    }

    private void Touch() => ConcurrencyToken++;

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

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

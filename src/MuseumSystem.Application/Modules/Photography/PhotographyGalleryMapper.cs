using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyGalleryMapper
{
    public ArtifactImageGalleryDto ToGallery(
        Artifact artifact,
        IReadOnlyList<PhotographyGalleryImageDto> images) =>
        new(
            ToArtifactSummary(artifact),
            images.Count == 0 ? ArtifactImageGalleryState.NoImages : ArtifactImageGalleryState.HasImages,
            images);

    public PhotographyGalleryImageDto ToImage(
        ArtifactImage image,
        PhotographySet set,
        PhotographyGalleryRenditionDto thumbnail,
        PhotographyGalleryRenditionDto preview) =>
        new(
            image.ArtifactImageId,
            image.PhotographySetId,
            set.Purpose,
            set.PhotographyDate,
            set.PhotographerUserId,
            image.OriginalFilename,
            image.Caption,
            image.ContentType,
            image.PixelWidth,
            image.PixelHeight,
            image.UploadedAt,
            image.UploadedByUserId,
            thumbnail,
            preview);

    public PhotographyGalleryRenditionDto ToAvailableRendition(
        PhotographyImageRendition rendition,
        Guid artifactImageId,
        string contentType,
        long contentLength,
        int pixelWidth,
        int pixelHeight) =>
        new(
            rendition,
            PhotographyImageRenditionAvailability.Available,
            contentType,
            contentLength,
            pixelWidth,
            pixelHeight,
            new PhotographyImageAccessReferenceDto(artifactImageId, rendition));

    public PhotographyGalleryRenditionDto ToUnavailableRendition(
        PhotographyImageRendition rendition,
        ArtifactImageDerivative? derivative) =>
        new(
            rendition,
            PhotographyImageRenditionAvailability.Unavailable,
            derivative?.ContentType,
            derivative?.FileSizeBytes,
            derivative?.PixelWidth,
            derivative?.PixelHeight,
            null);

    private static PhotographyGalleryArtifactDto ToArtifactSummary(Artifact artifact) =>
        new(
            artifact.ArtifactId,
            artifact.CategoryId,
            artifact.Category?.CategoryCode ?? string.Empty,
            artifact.Category?.NameArabic ?? "Unknown category",
            artifact.ItemNumber,
            artifact.MuseumNumberDisplay,
            artifact.BasicDescription,
            artifact.CurrentStatus,
            artifact.CurrentLocationId,
            artifact.CurrentLocation?.NameArabic,
            artifact.CurrentHolderType,
            artifact.CurrentHolderName,
            artifact.LastKnownStorageLocationId);
}

public enum ArtifactImageGalleryState
{
    NoImages = 1,
    HasImages = 2
}

public enum PhotographyImageRendition
{
    Thumbnail = 1,
    Preview = 2
}

public enum PhotographyImageRenditionAvailability
{
    Available = 1,
    Unavailable = 2
}

public enum PhotographyImageStreamStatus
{
    Available = 1,
    NotFound = 2,
    Unavailable = 3
}

public sealed record ViewArtifactImagesQuery(Guid ArtifactId);

public sealed record ReadArtifactImageRenditionQuery(Guid ArtifactImageId, PhotographyImageRendition Rendition);

public sealed record ArtifactImageGalleryDto(
    PhotographyGalleryArtifactDto Artifact,
    ArtifactImageGalleryState State,
    IReadOnlyList<PhotographyGalleryImageDto> Images);

public sealed record PhotographyGalleryArtifactDto(
    Guid ArtifactId,
    Guid CategoryId,
    string CategoryCode,
    string CategoryNameArabic,
    int ItemNumber,
    string MuseumNumber,
    string BasicDescription,
    ArtifactCurrentStatus CurrentStatus,
    Guid? CurrentLocationId,
    string? CurrentLocationName,
    string? CurrentHolderType,
    string? CurrentHolderName,
    Guid? LastKnownStorageLocationId);

public sealed record PhotographyGalleryImageDto(
    Guid ArtifactImageId,
    Guid PhotographySetId,
    PhotographyPurpose Purpose,
    DateOnly PhotographyDate,
    string PhotographerUserId,
    string OriginalFilename,
    string? Caption,
    string ContentType,
    int PixelWidth,
    int PixelHeight,
    DateTimeOffset UploadedAt,
    string UploadedByUserId,
    PhotographyGalleryRenditionDto Thumbnail,
    PhotographyGalleryRenditionDto Preview);

public sealed record PhotographyGalleryRenditionDto(
    PhotographyImageRendition Rendition,
    PhotographyImageRenditionAvailability Availability,
    string? ContentType,
    long? ContentLength,
    int? PixelWidth,
    int? PixelHeight,
    PhotographyImageAccessReferenceDto? Access);

public sealed record PhotographyImageAccessReferenceDto(
    Guid ArtifactImageId,
    PhotographyImageRendition Rendition);

public sealed class ArtifactImageSafeReadResult : IDisposable, IAsyncDisposable
{
    private ArtifactImageSafeReadResult(
        PhotographyImageStreamStatus status,
        Stream? content,
        string? contentType,
        long? contentLength,
        string? filename)
    {
        Status = status;
        Content = content;
        ContentType = contentType;
        ContentLength = contentLength;
        Filename = filename;
    }

    public PhotographyImageStreamStatus Status { get; }
    public Stream? Content { get; }
    public string? ContentType { get; }
    public long? ContentLength { get; }
    public string? Filename { get; }

    public static ArtifactImageSafeReadResult Available(
        Stream content,
        string contentType,
        long contentLength,
        string filename) =>
        new(
            PhotographyImageStreamStatus.Available,
            content ?? throw new ArgumentNullException(nameof(content)),
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            contentLength,
            string.IsNullOrWhiteSpace(filename) ? "artifact-image" : filename);

    public static ArtifactImageSafeReadResult NotFound() =>
        new(PhotographyImageStreamStatus.NotFound, null, null, null, null);

    public static ArtifactImageSafeReadResult Unavailable() =>
        new(PhotographyImageStreamStatus.Unavailable, null, null, null, null);

    public void Dispose() => Content?.Dispose();

    public async ValueTask DisposeAsync()
    {
        if (Content is not null)
        {
            await Content.DisposeAsync();
        }
    }
}

using System.Reflection;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class ArtifactImageTests
{
    [Fact]
    public void Valid_artifact_image_creation_records_original_metadata()
    {
        var artifactId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var uploadedAt = new DateTimeOffset(2026, 8, 25, 9, 30, 0, TimeSpan.Zero);
        var image = CreateImage(artifactId, setId, uploadedAt: uploadedAt);

        Assert.NotEqual(Guid.Empty, image.ArtifactImageId);
        Assert.Equal(artifactId, image.ArtifactId);
        Assert.Equal(setId, image.PhotographySetId);
        Assert.Equal("artifact-images/originals/image-1.jpg", image.OriginalObjectKey.Value);
        Assert.Equal("image-1.jpg", image.OriginalFilename);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(1_024, image.FileSizeBytes);
        Assert.Equal(800, image.PixelWidth);
        Assert.Equal(600, image.PixelHeight);
        Assert.Equal("photographer-1", image.UploadedByUserId);
        Assert.Equal(uploadedAt, image.UploadedAt);
        Assert.Equal(ArtifactImageStatus.Available, image.Status);
    }

    [Fact]
    public void Required_creation_values_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => CreateImage(artifactId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateImage(photographySetId: Guid.Empty));
        Assert.Throws<ArgumentNullException>(() => ArtifactImage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!,
            "image-1.jpg",
            "image/jpeg",
            1_024,
            800,
            600,
            "photographer-1",
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => CreateImage(originalFilename: " "));
        Assert.Throws<ArgumentException>(() => CreateImage(contentType: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateImage(fileSizeBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateImage(pixelWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateImage(pixelHeight: 0));
        Assert.Throws<ArgumentException>(() => CreateImage(uploadedByUserId: " "));
    }

    [Fact]
    public void Original_metadata_cannot_be_replaced_or_mutated_after_creation()
    {
        var immutableMetadata = new[]
        {
            nameof(ArtifactImage.ArtifactId),
            nameof(ArtifactImage.PhotographySetId),
            nameof(ArtifactImage.OriginalObjectKey),
            nameof(ArtifactImage.OriginalFilename),
            nameof(ArtifactImage.ContentType),
            nameof(ArtifactImage.FileSizeBytes),
            nameof(ArtifactImage.PixelWidth),
            nameof(ArtifactImage.PixelHeight),
            nameof(ArtifactImage.UploadedByUserId),
            nameof(ArtifactImage.UploadedAt)
        };

        foreach (var propertyName in immutableMetadata)
        {
            var property = typeof(ArtifactImage).GetProperty(propertyName)!;
            Assert.False(property.SetMethod?.IsPublic == true, $"{propertyName} must not expose a public setter.");
        }

        Assert.DoesNotContain(typeof(ArtifactImage).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(method => !method.IsSpecialName), method => method.Name.Contains("Original", StringComparison.Ordinal));
    }

    [Fact]
    public void Caption_is_photography_owned_editable_metadata_only()
    {
        var image = CreateImage();

        image.UpdateCaption("  front view  ");

        Assert.Equal("front view", image.Caption);
        Assert.Equal("artifact-images/originals/image-1.jpg", image.OriginalObjectKey.Value);
        Assert.Equal("image-1.jpg", image.OriginalFilename);
        Assert.Equal("image/jpeg", image.ContentType);
    }

    [Fact]
    public void Derivative_must_belong_to_same_artifact_image()
    {
        var image = CreateImage();
        var derivative = ArtifactImageDerivative.Create(
            image.ArtifactImageId,
            ImageDerivativeKind.Thumbnail,
            ImageStorageObjectKey.Create("artifact-images/derivatives/thumb-1.jpg"),
            "image/jpeg",
            128,
            120,
            90);

        image.AddDerivative(derivative);

        Assert.Equal(derivative, Assert.Single(image.Derivatives));
        Assert.Throws<InvalidOperationException>(() => image.AddDerivative(ArtifactImageDerivative.Create(
            Guid.NewGuid(),
            ImageDerivativeKind.Preview,
            ImageStorageObjectKey.Create("artifact-images/derivatives/preview-1.jpg"),
            "image/jpeg",
            512,
            640,
            480)));
    }

    [Fact]
    public void Multiple_derivatives_of_same_kind_are_not_blocked_by_domain()
    {
        var image = CreateImage();

        image.AddDerivative(CreateDerivative(image.ArtifactImageId, ImageDerivativeKind.Thumbnail, "thumb-a.jpg"));
        image.AddDerivative(CreateDerivative(image.ArtifactImageId, ImageDerivativeKind.Thumbnail, "thumb-b.jpg"));

        Assert.Equal(2, image.Derivatives.Count(derivative => derivative.Kind == ImageDerivativeKind.Thumbnail));
    }

    [Fact]
    public void Derivative_metadata_does_not_replace_original_metadata()
    {
        var image = CreateImage();
        image.AddDerivative(CreateDerivative(image.ArtifactImageId, ImageDerivativeKind.Preview, "preview-1.jpg"));

        Assert.Equal("artifact-images/originals/image-1.jpg", image.OriginalObjectKey.Value);
        Assert.Equal(1_024, image.FileSizeBytes);
        Assert.Equal(800, image.PixelWidth);
        Assert.Equal(600, image.PixelHeight);
    }

    [Fact]
    public void Deletion_state_transitions_remain_valid()
    {
        var image = CreateImage();
        var deletedAt = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod);

        Assert.Equal(ArtifactImageStatus.Deleted, image.Status);
        Assert.Equal("photographer-1", image.DeletedByUserId);
        Assert.Equal(deletedAt, image.DeletedAt);
    }

    [Fact]
    public void Rejected_file_outcome_does_not_require_artifact_image_creation()
    {
        var outcome = PhotographyUploadFileOutcome.Rejected(
            Guid.NewGuid(),
            0,
            "not-an-image.txt",
            "file-fingerprint-0",
            "Unsupported file type.");

        Assert.Equal(PhotographyUploadFileOutcomeStatus.Rejected, outcome.Status);
        Assert.Null(outcome.ArtifactImageId);
        Assert.Null(outcome.OriginalObjectKey);
    }

    [Fact]
    public void Artifact_image_has_no_primary_or_custody_movement_meaning()
    {
        var memberNames = typeof(ArtifactImage)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();

        Assert.DoesNotContain(memberNames, name => name.Equals("IsPrimary", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Custody", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Movement", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Location", StringComparison.OrdinalIgnoreCase));
    }

    private static ArtifactImage CreateImage(
        Guid? artifactId = null,
        Guid? photographySetId = null,
        ImageStorageObjectKey? objectKey = null,
        string originalFilename = "image-1.jpg",
        string contentType = "image/jpeg",
        long fileSizeBytes = 1_024,
        int pixelWidth = 800,
        int pixelHeight = 600,
        string uploadedByUserId = "photographer-1",
        DateTimeOffset? uploadedAt = null) =>
        ArtifactImage.Create(
            artifactId ?? Guid.NewGuid(),
            photographySetId ?? Guid.NewGuid(),
            objectKey ?? ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg"),
            originalFilename,
            contentType,
            fileSizeBytes,
            pixelWidth,
            pixelHeight,
            uploadedByUserId,
            uploadedAt ?? new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero));

    private static ArtifactImageDerivative CreateDerivative(Guid artifactImageId, ImageDerivativeKind kind, string keySuffix) =>
        ArtifactImageDerivative.Create(
            artifactImageId,
            kind,
            ImageStorageObjectKey.Create($"artifact-images/derivatives/{keySuffix}"),
            "image/jpeg",
            128,
            120,
            90);
}

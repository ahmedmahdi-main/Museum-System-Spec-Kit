using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class ArtifactImageDeletionRulesTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(59, 0, true)]
    [InlineData(60, 0, true)]
    [InlineData(60, 1, false)]
    public void Grace_period_boundary_is_inclusive_until_exactly_sixty_minutes(int minutes, long extraTicks, bool expected)
    {
        var serverNow = UploadedAt.AddMinutes(minutes).AddTicks(extraTicks);

        Assert.Equal(expected, PhotographyRules.IsWithinUploaderGracePeriod(UploadedAt, serverNow));
        Assert.Equal(expected, PhotographyRules.CanUseUploaderGraceDeletion(
            "photographer-1",
            "photographer-1",
            UploadedAt,
            serverNow,
            currentUserHasUploadPermission: true));
    }

    [Fact]
    public void Future_uploaded_time_is_not_within_grace_period()
    {
        Assert.False(PhotographyRules.IsWithinUploaderGracePeriod(UploadedAt, UploadedAt.AddTicks(-1)));
        Assert.False(PhotographyRules.CanUseUploaderGraceDeletion(
            "photographer-1",
            "photographer-1",
            UploadedAt,
            UploadedAt.AddTicks(-1),
            currentUserHasUploadPermission: true));
    }

    [Fact]
    public void Grace_deletion_requires_same_original_uploader_with_current_upload_permission()
    {
        Assert.True(PhotographyRules.CanUseUploaderGraceDeletion(
            " photographer-1 ",
            "photographer-1",
            UploadedAt,
            UploadedAt.AddMinutes(59),
            currentUserHasUploadPermission: true));
    }

    [Fact]
    public void Another_uploader_with_upload_permission_cannot_use_grace_deletion()
    {
        Assert.False(PhotographyRules.CanUseUploaderGraceDeletion(
            "user-a",
            "user-b",
            UploadedAt,
            UploadedAt.AddMinutes(59),
            currentUserHasUploadPermission: true));
    }

    [Fact]
    public void Permission_revocation_blocks_grace_deletion_even_for_original_uploader_within_grace()
    {
        Assert.False(PhotographyRules.CanUseUploaderGraceDeletion(
            "photographer-1",
            "photographer-1",
            UploadedAt,
            UploadedAt.AddMinutes(59),
            currentUserHasUploadPermission: false));
    }

    [Fact]
    public void Uploader_identity_matching_is_ordinal_after_required_whitespace_normalization()
    {
        Assert.False(PhotographyRules.CanUseUploaderGraceDeletion(
            "User-A",
            "user-a",
            UploadedAt,
            UploadedAt.AddMinutes(59),
            currentUserHasUploadPermission: true));
    }

    [Fact]
    public void Privileged_deletion_requires_a_reason_and_uploader_grace_does_not()
    {
        Assert.True(PhotographyRules.IsDeletionReasonRequired(ArtifactImageDeletionMode.Privileged));
        Assert.False(PhotographyRules.IsDeletionReasonRequired(ArtifactImageDeletionMode.UploaderGracePeriod));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("duplicate upload correction", true)]
    public void Privileged_deletion_reason_validation_requires_non_empty_text(string? reason, bool expected)
    {
        Assert.Equal(expected, PhotographyRules.HasRequiredDeletionReason(ArtifactImageDeletionMode.Privileged, reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("optional context")]
    public void Uploader_grace_deletion_reason_is_optional(string? reason)
    {
        Assert.True(PhotographyRules.HasRequiredDeletionReason(ArtifactImageDeletionMode.UploaderGracePeriod, reason));
    }

    [Fact]
    public void Mark_delete_pending_for_uploader_grace_records_mode_without_manual_reason_and_advances_concurrency()
    {
        var image = CreateImage();
        var beforeToken = image.ConcurrencyToken;

        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);

        Assert.Equal(ArtifactImageStatus.DeletePending, image.Status);
        Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, image.DeletionMode);
        Assert.Null(image.DeletionReason);
        Assert.Equal(beforeToken + 1, image.ConcurrencyToken);
    }

    [Fact]
    public void Mark_delete_pending_for_privileged_deletion_retains_normalized_reason_and_advances_concurrency()
    {
        var image = CreateImage();
        var beforeToken = image.ConcurrencyToken;

        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "  duplicate accession photo  ");

        Assert.Equal(ArtifactImageStatus.DeletePending, image.Status);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, image.DeletionMode);
        Assert.Equal("duplicate accession photo", image.DeletionReason);
        Assert.Equal(beforeToken + 1, image.ConcurrencyToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Privileged_delete_pending_requires_valid_reason(string? reason)
    {
        var image = CreateImage();

        Assert.Throws<ArgumentException>(() => image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, reason));
        Assert.Equal(ArtifactImageStatus.Available, image.Status);
        Assert.Null(image.DeletionMode);
    }

    [Fact]
    public void Delete_pending_image_cannot_be_marked_pending_again()
    {
        var image = CreateImage();
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);

        Assert.Throws<InvalidOperationException>(() => image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod));
    }

    [Fact]
    public void Deleted_image_cannot_be_marked_pending_again()
    {
        var image = CreateImage();
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", UploadedAt.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() => image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "late reason"));
    }

    [Fact]
    public void Available_image_cannot_be_finalized_as_deleted_directly()
    {
        var image = CreateImage();

        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.UploaderGracePeriod,
            "photographer-1",
            UploadedAt.AddMinutes(10)));
        Assert.Equal(ArtifactImageStatus.Available, image.Status);
    }

    [Fact]
    public void Deletion_finalization_mode_must_match_pending_intent()
    {
        var graceImage = CreateImage();
        graceImage.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        Assert.Throws<InvalidOperationException>(() => graceImage.MarkDeleted(
            ArtifactImageDeletionMode.Privileged,
            "supervisor-1",
            UploadedAt.AddMinutes(10),
            "privileged reason"));

        var privilegedImage = CreateImage();
        privilegedImage.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "duplicate upload");
        Assert.Throws<InvalidOperationException>(() => privilegedImage.MarkDeleted(
            ArtifactImageDeletionMode.UploaderGracePeriod,
            "photographer-1",
            UploadedAt.AddMinutes(10)));
    }

    [Fact]
    public void Privileged_deletion_reason_cannot_be_replaced_during_finalization()
    {
        var image = CreateImage();
        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "reason a");

        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.Privileged,
            "supervisor-1",
            UploadedAt.AddMinutes(10),
            "reason b"));
        Assert.Equal("reason a", image.DeletionReason);
    }

    [Fact]
    public void Privileged_deletion_finalization_preserves_existing_reason_when_reason_is_omitted_or_matches()
    {
        var omitted = CreateImage();
        omitted.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "reason a");
        omitted.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", UploadedAt.AddMinutes(10));
        Assert.Equal("reason a", omitted.DeletionReason);

        var matching = CreateImage();
        matching.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "reason a");
        matching.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", UploadedAt.AddMinutes(10), "  reason a  ");
        Assert.Equal("reason a", matching.DeletionReason);
    }

    [Fact]
    public void Final_deleted_metadata_is_recorded_and_deletion_metadata_remains()
    {
        var image = CreateImage();
        var pendingToken = image.ConcurrencyToken;
        var deletedAt = UploadedAt.AddMinutes(10);

        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "duplicate upload");
        image.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", deletedAt);

        Assert.Equal(ArtifactImageStatus.Deleted, image.Status);
        Assert.Equal("supervisor-1", image.DeletedByUserId);
        Assert.Equal(deletedAt, image.DeletedAt);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, image.DeletionMode);
        Assert.Equal("duplicate upload", image.DeletionReason);
        Assert.Equal(pendingToken + 2, image.ConcurrencyToken);
    }

    [Fact]
    public void Deleted_image_cannot_be_finalized_again()
    {
        var image = CreateImage();
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", UploadedAt.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.UploaderGracePeriod,
            "photographer-1",
            UploadedAt.AddMinutes(11)));
    }

    [Theory]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public void Delete_pending_and_deleted_images_are_not_primary_eligible(ArtifactImageStatus terminalStatus)
    {
        var artifactId = Guid.NewGuid();
        var image = CreateImage(artifactId: artifactId);
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        if (terminalStatus == ArtifactImageStatus.Deleted)
        {
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", UploadedAt.AddMinutes(10));
        }

        Assert.False(PhotographyRules.IsPrimaryImageEligible(image, artifactId));
    }

    [Fact]
    public void Deletion_lifecycle_does_not_mutate_original_image_metadata()
    {
        var artifactId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var image = CreateImage(artifactId: artifactId, photographySetId: setId);
        var original = OriginalMetadata.From(image);

        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "duplicate upload");
        image.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", UploadedAt.AddMinutes(10));

        original.AssertUnchanged(image);
    }

    private static ArtifactImage CreateImage(
        Guid? artifactId = null,
        Guid? photographySetId = null,
        string objectKey = "artifact-images/originals/deletion-test.jpg",
        string originalFilename = "deletion-test.jpg",
        string contentType = "image/jpeg",
        long fileSizeBytes = 2048,
        int pixelWidth = 1200,
        int pixelHeight = 800,
        string uploadedByUserId = "photographer-1",
        DateTimeOffset? uploadedAt = null) =>
        ArtifactImage.Create(
            artifactId ?? Guid.NewGuid(),
            photographySetId ?? Guid.NewGuid(),
            ImageStorageObjectKey.Create(objectKey),
            originalFilename,
            contentType,
            fileSizeBytes,
            pixelWidth,
            pixelHeight,
            uploadedByUserId,
            uploadedAt ?? UploadedAt);

    private sealed record OriginalMetadata(
        Guid ArtifactId,
        Guid PhotographySetId,
        ImageStorageObjectKey OriginalObjectKey,
        string OriginalFilename,
        string ContentType,
        long FileSizeBytes,
        int PixelWidth,
        int PixelHeight,
        string UploadedByUserId,
        DateTimeOffset UploadedAt)
    {
        public static OriginalMetadata From(ArtifactImage image) =>
            new(
                image.ArtifactId,
                image.PhotographySetId,
                image.OriginalObjectKey,
                image.OriginalFilename,
                image.ContentType,
                image.FileSizeBytes,
                image.PixelWidth,
                image.PixelHeight,
                image.UploadedByUserId,
                image.UploadedAt);

        public void AssertUnchanged(ArtifactImage image)
        {
            Assert.Equal(ArtifactId, image.ArtifactId);
            Assert.Equal(PhotographySetId, image.PhotographySetId);
            Assert.Equal(OriginalObjectKey, image.OriginalObjectKey);
            Assert.Equal(OriginalFilename, image.OriginalFilename);
            Assert.Equal(ContentType, image.ContentType);
            Assert.Equal(FileSizeBytes, image.FileSizeBytes);
            Assert.Equal(PixelWidth, image.PixelWidth);
            Assert.Equal(PixelHeight, image.PixelHeight);
            Assert.Equal(UploadedByUserId, image.UploadedByUserId);
            Assert.Equal(UploadedAt, image.UploadedAt);
        }
    }
}

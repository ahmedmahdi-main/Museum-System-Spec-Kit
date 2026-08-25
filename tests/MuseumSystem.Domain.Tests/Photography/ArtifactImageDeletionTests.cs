using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class ArtifactImageDeletionTests
{
    [Fact]
    public void Available_image_cannot_be_marked_deleted_without_pending_intent()
    {
        var image = CreateImage();

        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.UploaderGracePeriod,
            "photographer-1",
            DateTimeOffset.UtcNow));

        Assert.Equal(ArtifactImageStatus.Available, image.Status);
    }

    [Fact]
    public void Available_to_delete_pending_to_deleted_succeeds_and_preserves_intent()
    {
        var image = CreateImage();
        var deletedAt = new DateTimeOffset(2026, 8, 24, 13, 0, 0, TimeSpan.Zero);

        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, " Wrong file uploaded ");
        image.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", deletedAt);

        Assert.Equal(ArtifactImageStatus.Deleted, image.Status);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, image.DeletionMode);
        Assert.Equal("Wrong file uploaded", image.DeletionReason);
        Assert.Equal("supervisor-1", image.DeletedByUserId);
        Assert.Equal(deletedAt, image.DeletedAt);
    }

    [Fact]
    public void Privileged_pending_deletion_requires_reason()
    {
        var image = CreateImage();

        Assert.Throws<ArgumentException>(() => image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, " "));

        Assert.Equal(ArtifactImageStatus.Available, image.Status);
        Assert.Null(image.DeletionMode);
        Assert.Null(image.DeletionReason);
    }

    [Fact]
    public void Pending_privileged_deletion_cannot_be_finalized_as_uploader_grace()
    {
        var image = CreateImage();

        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "Wrong file uploaded");

        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.UploaderGracePeriod,
            "photographer-1",
            DateTimeOffset.UtcNow));

        Assert.Equal(ArtifactImageStatus.DeletePending, image.Status);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, image.DeletionMode);
        Assert.Equal("Wrong file uploaded", image.DeletionReason);
    }

    [Fact]
    public void Pending_deletion_reason_cannot_be_replaced_during_finalization()
    {
        var image = CreateImage();

        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "Wrong file uploaded");

        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.Privileged,
            "supervisor-1",
            DateTimeOffset.UtcNow,
            "Different reason"));

        Assert.Equal(ArtifactImageStatus.DeletePending, image.Status);
        Assert.Equal("Wrong file uploaded", image.DeletionReason);
    }

    [Fact]
    public void Deleted_image_cannot_return_to_another_state_or_be_finalized_again()
    {
        var image = CreateImage();

        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "reason"));
        Assert.Throws<InvalidOperationException>(() => image.MarkDeleted(
            ArtifactImageDeletionMode.UploaderGracePeriod,
            "photographer-1",
            DateTimeOffset.UtcNow));
        Assert.Equal(ArtifactImageStatus.Deleted, image.Status);
    }

    [Fact]
    public void Undefined_deletion_mode_is_rejected_by_image_transitions()
    {
        var image = CreateImage();

        Assert.Throws<ArgumentOutOfRangeException>(() => image.MarkDeletePending((ArtifactImageDeletionMode)99));
        Assert.Equal(ArtifactImageStatus.Available, image.Status);
    }

    private static ArtifactImage CreateImage() =>
        ArtifactImage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg"),
            "image-1.jpg",
            "image/jpeg",
            1024,
            800,
            600,
            "photographer-1",
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
}

using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Photography.Storage;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class ArtifactImageStorageDeletionTests(MinioArtifactImageStorageTestFixture fixture) : IClassFixture<MinioArtifactImageStorageTestFixture>
{
    [Fact]
    public async Task Deleting_original_thumbnail_and_preview_removes_all_three_objects_from_minio()
    {
        var storage = fixture.CreateStorage();
        var originalKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("originals/original.jpg"));
        var thumbnailKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/thumbnail.jpg"));
        var previewKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/preview.jpg"));
        await StoreAsync(storage, originalKey);
        await StoreAsync(storage, thumbnailKey);
        await StoreAsync(storage, previewKey);

        var result = await storage.DeleteImageObjectsAsync(originalKey, [thumbnailKey, previewKey]);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.ObjectResults.Count);
        Assert.All(result.ObjectResults, objectResult => Assert.True(objectResult.Succeeded));
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(originalKey)).Kind);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(thumbnailKey)).Kind);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(previewKey)).Kind);
    }

    [Fact]
    public async Task Already_missing_original_is_accepted_as_idempotent_cleanup()
    {
        var storage = fixture.CreateStorage();
        var originalKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("originals/missing-original.jpg"));
        var thumbnailKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/missing-original-thumbnail.jpg"));
        var previewKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/missing-original-preview.jpg"));
        await StoreAsync(storage, thumbnailKey);
        await StoreAsync(storage, previewKey);

        var result = await storage.DeleteImageObjectsAsync(originalKey, [thumbnailKey, previewKey]);

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(originalKey)).Kind);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(thumbnailKey)).Kind);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(previewKey)).Kind);
    }

    [Fact]
    public async Task Already_missing_derivative_does_not_prevent_overall_success()
    {
        var storage = fixture.CreateStorage();
        var originalKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("originals/missing-derivative-original.jpg"));
        var missingThumbnailKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/missing-thumbnail.jpg"));
        var presentPreviewKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/present-preview.jpg"));
        await StoreAsync(storage, originalKey);
        await StoreAsync(storage, presentPreviewKey);

        var result = await storage.DeleteImageObjectsAsync(originalKey, [missingThumbnailKey, presentPreviewKey]);

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(originalKey)).Kind);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(presentPreviewKey)).Kind);
    }

    [Fact]
    public async Task Repeated_deletion_of_the_same_object_set_is_idempotent()
    {
        var storage = fixture.CreateStorage();
        var originalKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("originals/repeated-original.jpg"));
        var thumbnailKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/repeated-thumbnail.jpg"));
        var previewKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/repeated-preview.jpg"));
        await StoreAsync(storage, originalKey);
        await StoreAsync(storage, thumbnailKey);
        await StoreAsync(storage, previewKey);

        var first = await storage.DeleteImageObjectsAsync(originalKey, [thumbnailKey, previewKey]);
        var second = await storage.DeleteImageObjectsAsync(originalKey, [thumbnailKey, previewKey]);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.All(second.ObjectResults, objectResult => Assert.Equal(ArtifactImageStorageResultKind.NotFound, objectResult.Kind));
    }

    private static async Task StoreAsync(MinioArtifactImageStorage storage, ImageStorageObjectKey key)
    {
        var bytes = PhotographyIntegrationTestImages.Jpeg(64, 64);
        var write = await storage.StoreOriginalAsync(key, PhotographyIntegrationTestImages.Stream(bytes), "image/jpeg", bytes.LongLength, null);
        Assert.True(write.Succeeded);
    }
}

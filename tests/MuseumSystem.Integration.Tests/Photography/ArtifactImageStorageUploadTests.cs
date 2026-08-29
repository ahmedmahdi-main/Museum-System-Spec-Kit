using System.Net;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class ArtifactImageStorageUploadTests(MinioArtifactImageStorageTestFixture fixture) : IClassFixture<MinioArtifactImageStorageTestFixture>
{
    [Fact]
    public async Task Store_stat_read_existing_key_conflict_private_access_and_delete_round_trip_through_minio()
    {
        var storage = fixture.CreateStorage();
        var originalBytes = PhotographyIntegrationTestImages.Jpeg(640, 480);
        var originalKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("originals/original.jpg"));
        var thumbnailKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/thumbnail.jpg"));
        var previewKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("derivatives/preview.jpg"));

        var originalWrite = await storage.StoreOriginalAsync(originalKey, PhotographyIntegrationTestImages.Stream(originalBytes), "image/jpeg", originalBytes.LongLength, "sha256-test");
        var thumbnailWrite = await storage.StoreDerivativeAsync(thumbnailKey, PhotographyIntegrationTestImages.Stream([1, 2, 3]), "image/jpeg", 3, ImageDerivativeKind.Thumbnail, null);
        var previewWrite = await storage.StoreDerivativeAsync(previewKey, PhotographyIntegrationTestImages.Stream([4, 5, 6, 7]), "image/jpeg", 4, ImageDerivativeKind.Preview, null);

        Assert.True(originalWrite.Succeeded);
        Assert.True(thumbnailWrite.Succeeded);
        Assert.True(previewWrite.Succeeded);
        Assert.Equal(originalKey, originalWrite.StoredObject!.ObjectKey);
        Assert.Equal(thumbnailKey, thumbnailWrite.StoredObject!.ObjectKey);
        Assert.Equal(previewKey, previewWrite.StoredObject!.ObjectKey);
        Assert.Equal("image/jpeg", originalWrite.StoredObject.ContentType);
        Assert.Equal(originalBytes.LongLength, originalWrite.StoredObject.LengthBytes);

        var stat = await storage.StatAsync(originalKey);
        Assert.True(stat.Exists);
        Assert.Equal(originalBytes.LongLength, stat.StoredObject!.LengthBytes);

        var read = await storage.OpenReadAsync(originalKey);
        Assert.True(read.Succeeded);
        await using (read.ReadStream!.Content)
        {
            using var copy = new MemoryStream();
            await read.ReadStream.Content.CopyToAsync(copy);
            Assert.Equal(originalBytes, copy.ToArray());
        }

        var existingWrite = await storage.StoreOriginalAsync(originalKey, PhotographyIntegrationTestImages.Stream(PhotographyIntegrationTestImages.Jpeg(10, 10)), "image/jpeg", PhotographyIntegrationTestImages.Jpeg(10, 10).LongLength, null);
        Assert.Equal(ArtifactImageStorageResultKind.AlreadyExists, existingWrite.Kind);

        using var anonymousClient = new HttpClient();
        var anonymousRead = await anonymousClient.GetAsync($"{fixture.Options.Endpoint.TrimEnd('/')}/{fixture.Options.BucketName}/{originalKey.Value}");
        Assert.Contains(anonymousRead.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });

        var shortLivedAccess = await storage.CreateShortLivedReadAccessAsync(originalKey, TimeSpan.FromMinutes(5));
        Assert.Equal(ArtifactImageStorageResultKind.NotSupported, shortLivedAccess.Kind);
        Assert.Null(shortLivedAccess.Access);
        Assert.DoesNotContain(fixture.Options.BucketName, shortLivedAccess.Failure!.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(originalKey.Value, shortLivedAccess.Failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);

        var delete = await storage.DeleteImageObjectsAsync(originalKey, [thumbnailKey, previewKey]);
        Assert.True(delete.Succeeded);

        var missing = await storage.StatAsync(originalKey);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, missing.Kind);
    }

    [Fact]
    public async Task Not_found_and_configuration_failures_are_mapped_to_structured_results()
    {
        var storage = fixture.CreateStorage();
        var missingKey = ImageStorageObjectKey.Create(fixture.CreateObjectKey("missing/not-found.jpg"));

        var readMissing = await storage.OpenReadAsync(missingKey);
        var deleteMissing = await storage.DeleteObjectAsync(missingKey);

        Assert.Equal(ArtifactImageStorageResultKind.NotFound, readMissing.Kind);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, deleteMissing.Kind);
        Assert.DoesNotContain(fixture.Options.BucketName, readMissing.Failure!.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);

        var badOptions = new MuseumSystem.Infrastructure.Photography.Storage.MinioArtifactImageStorageOptions
        {
            Provider = "Minio",
            Endpoint = "http://127.0.0.1:1",
            BucketName = fixture.Options.BucketName,
            AccessKey = fixture.Options.AccessKey,
            SecretKey = fixture.Options.SecretKey,
            UseTls = false,
            RequestTimeoutSeconds = 1
        };
        var misconfiguredStorage = new MuseumSystem.Infrastructure.Photography.Storage.MinioArtifactImageStorage(Microsoft.Extensions.Options.Options.Create(badOptions));
        var result = await misconfiguredStorage.StatAsync(ImageStorageObjectKey.Create("artifact-images/integration/misconfigured.jpg"));

        Assert.Contains(result.Kind, new[] { ArtifactImageStorageResultKind.RetryableFailure, ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured });
        Assert.NotNull(result.Failure);
        Assert.DoesNotContain("127.0.0.1", result.Failure!.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Options.BucketName, result.Failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
    }
}

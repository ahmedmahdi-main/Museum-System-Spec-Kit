using System.Net;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Photography.Storage;

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
        AssertSafeStorageFailure(readMissing.Failure, missingKey);

        var badOptions = new MinioArtifactImageStorageOptions
        {
            Provider = "Minio",
            Endpoint = "http://127.0.0.1:1",
            BucketName = fixture.Options.BucketName,
            AccessKey = fixture.Options.AccessKey,
            SecretKey = fixture.Options.SecretKey,
            UseTls = false,
            RequestTimeoutSeconds = 1
        };
        var misconfiguredStorage = new MinioArtifactImageStorage(Options.Create(badOptions));
        var result = await misconfiguredStorage.StatAsync(ImageStorageObjectKey.Create("artifact-images/integration/misconfigured.jpg"));

        Assert.Equal(ArtifactImageStorageResultKind.RetryableFailure, result.Kind);
        Assert.NotNull(result.Failure);
        AssertSafeStorageFailure(result.Failure!, ImageStorageObjectKey.Create("artifact-images/integration/misconfigured.jpg"), "127.0.0.1");

        var repeatedResult = await misconfiguredStorage.StatAsync(ImageStorageObjectKey.Create("artifact-images/integration/misconfigured-again.jpg"));
        Assert.Equal(ArtifactImageStorageResultKind.RetryableFailure, repeatedResult.Kind);
        Assert.NotNull(repeatedResult.Failure);
        AssertSafeStorageFailure(repeatedResult.Failure!, ImageStorageObjectKey.Create("artifact-images/integration/misconfigured-again.jpg"), "127.0.0.1");

        var missingBucketOptions = new MinioArtifactImageStorageOptions
        {
            Provider = "Minio",
            Endpoint = fixture.Options.Endpoint,
            BucketName = $"{fixture.Options.BucketName}-missing-{Guid.NewGuid():N}",
            AccessKey = fixture.Options.AccessKey,
            SecretKey = fixture.Options.SecretKey,
            UseTls = false,
            RequestTimeoutSeconds = fixture.Options.RequestTimeoutSeconds
        };
        var missingBucketStorage = new MinioArtifactImageStorage(Options.Create(missingBucketOptions));
        var missingBucketResult = await missingBucketStorage.StatAsync(ImageStorageObjectKey.Create("artifact-images/integration/missing-bucket.jpg"));

        Assert.Equal(ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured, missingBucketResult.Kind);
        Assert.NotNull(missingBucketResult.Failure);
        AssertSafeStorageFailure(
            missingBucketResult.Failure!,
            ImageStorageObjectKey.Create("artifact-images/integration/missing-bucket.jpg"),
            missingBucketOptions.BucketName);

        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await storage.StatAsync(ImageStorageObjectKey.Create(fixture.CreateObjectKey("canceled/stat.jpg")), source.Token));
    }

    private static void AssertSafeStorageFailure(
        ArtifactImageStorageFailure failure,
        ImageStorageObjectKey objectKey,
        params string[] additionalForbiddenFragments)
    {
        Assert.DoesNotContain(objectKey.Value, failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(objectKey.Value, failure.OperationalSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifact-images", failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifact-images", failure.OperationalSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", failure.OperationalSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", failure.OperationalSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", failure.OperationalSummary, StringComparison.OrdinalIgnoreCase);

        foreach (var fragment in additionalForbiddenFragments)
        {
            Assert.DoesNotContain(fragment, failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fragment, failure.OperationalSummary, StringComparison.OrdinalIgnoreCase);
        }
    }
}

using System.Net;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class ArtifactImageAccessBoundaryTests(MinioArtifactImageStorageTestFixture fixture) : IClassFixture<MinioArtifactImageStorageTestFixture>
{
    [Fact]
    public async Task Stored_image_objects_are_private_and_not_exposed_as_short_lived_storage_urls()
    {
        var storage = fixture.CreateStorage();
        var bytes = PhotographyIntegrationTestImages.Jpeg(320, 240);
        var key = ImageStorageObjectKey.Create(fixture.CreateObjectKey("access-boundary/private-original.jpg"));

        var write = await storage.StoreOriginalAsync(key, PhotographyIntegrationTestImages.Stream(bytes), "image/jpeg", bytes.LongLength, "sha256-access");

        Assert.True(write.Succeeded);
        using var anonymousClient = new HttpClient();
        var anonymousRead = await anonymousClient.GetAsync($"{fixture.Options.Endpoint.TrimEnd('/')}/{fixture.Options.BucketName}/{key.Value}");
        Assert.Contains(anonymousRead.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });

        var directAccess = await storage.CreateShortLivedReadAccessAsync(key, TimeSpan.FromMinutes(5));
        Assert.Equal(ArtifactImageStorageResultKind.NotSupported, directAccess.Kind);
        Assert.Null(directAccess.Access);
        Assert.DoesNotContain(fixture.Options.BucketName, directAccess.Failure!.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(key.Value, directAccess.Failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Options.Endpoint, directAccess.Failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Application_storage_client_can_read_private_derivative_binary_without_public_url()
    {
        var storage = fixture.CreateStorage();
        var key = ImageStorageObjectKey.Create(fixture.CreateObjectKey("access-boundary/preview.jpg"));
        var bytes = new byte[] { 10, 20, 30, 40 };

        var write = await storage.StoreDerivativeAsync(
            key,
            PhotographyIntegrationTestImages.Stream(bytes),
            "image/jpeg",
            bytes.LongLength,
            ImageDerivativeKind.Preview,
            "sha256-preview");
        var read = await storage.OpenReadAsync(key);

        Assert.True(write.Succeeded);
        Assert.True(read.Succeeded);
        Assert.Equal("image/jpeg", read.ReadStream!.Metadata.ContentType);
        await using (read.ReadStream.Content)
        {
            using var copy = new MemoryStream();
            await read.ReadStream.Content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }
    }

    [Fact]
    public async Task Missing_private_binary_returns_structured_not_found_without_provider_details()
    {
        var storage = fixture.CreateStorage();
        var key = ImageStorageObjectKey.Create(fixture.CreateObjectKey("access-boundary/missing-preview.jpg"));

        var read = await storage.OpenReadAsync(key);

        Assert.Equal(ArtifactImageStorageResultKind.NotFound, read.Kind);
        Assert.NotNull(read.Failure);
        Assert.DoesNotContain(fixture.Options.BucketName, read.Failure!.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(key.Value, read.Failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Options.Endpoint, read.Failure.StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
    }
}

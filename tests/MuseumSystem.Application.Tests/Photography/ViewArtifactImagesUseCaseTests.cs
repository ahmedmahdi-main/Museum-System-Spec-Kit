using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class ViewArtifactImagesUseCaseTests
{
    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyManage)]
    [InlineData(PermissionNames.PhotographyRequest)]
    [InlineData(PermissionNames.PhotographyDelete)]
    public async Task View_requires_exact_photography_view_permission(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        var useCase = CreateUseCase(db, storage, permissions);

        var result = await useCase.ViewArtifactImages(new ViewArtifactImagesQuery(artifact.ArtifactId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Empty(storage.StatCalls);
        Assert.Empty(storage.ReadCalls);
    }

    [Fact]
    public async Task View_lists_available_images_with_central_artifact_context_and_safe_rendition_references()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var available = AddImageWithDerivatives(db, artifact);
        AddImageWithDerivatives(db, artifact, status: ArtifactImageStatus.DeletePending);
        storageSeedAvailableRenditions(available);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        storage.AddObject(available.Thumbnail!.ObjectKey, [1, 2, 3], "image/jpeg");
        storage.AddObject(available.Preview!.ObjectKey, [4, 5, 6, 7], "image/jpeg");
        var useCase = CreateUseCase(db, storage, [PermissionNames.PhotographyView]);

        var result = await useCase.ViewArtifactImages(new ViewArtifactImagesQuery(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactImageGalleryState.HasImages, result.Value!.State);
        Assert.Equal(artifact.MuseumNumberDisplay, result.Value.Artifact.MuseumNumber);
        Assert.Equal(artifact.BasicDescription, result.Value.Artifact.BasicDescription);
        var image = Assert.Single(result.Value.Images);
        Assert.Equal(available.Image.ArtifactImageId, image.ArtifactImageId);
        Assert.Equal(PhotographyImageRenditionAvailability.Available, image.Thumbnail.Availability);
        Assert.Equal(PhotographyImageRenditionAvailability.Available, image.Preview.Availability);
        Assert.Equal(new PhotographyImageAccessReferenceDto(image.ArtifactImageId, PhotographyImageRendition.Thumbnail), image.Thumbnail.Access);
        Assert.Equal(new PhotographyImageAccessReferenceDto(image.ArtifactImageId, PhotographyImageRendition.Preview), image.Preview.Access);
        AssertPublicGalleryContractHasNoStorageInternals();
        var serialized = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("artifact-images/", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", serialized, StringComparison.OrdinalIgnoreCase);

        static void storageSeedAvailableRenditions(SeededGalleryImage seeded)
        {
            Assert.NotNull(seeded.Thumbnail);
            Assert.NotNull(seeded.Preview);
        }
    }

    [Fact]
    public async Task View_marks_missing_derivatives_unavailable_and_records_idempotent_recovery()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var seeded = AddImageWithDerivatives(db, artifact);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        var useCase = CreateUseCase(db, storage, [PermissionNames.PhotographyView]);

        var first = await useCase.ViewArtifactImages(new ViewArtifactImagesQuery(artifact.ArtifactId));
        var second = await useCase.ViewArtifactImages(new ViewArtifactImagesQuery(artifact.ArtifactId));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var image = Assert.Single(first.Value!.Images);
        Assert.Equal(PhotographyImageRenditionAvailability.Unavailable, image.Thumbnail.Availability);
        Assert.Null(image.Thumbnail.Access);
        Assert.Equal(PhotographyImageRenditionAvailability.Unavailable, image.Preview.Availability);
        Assert.Null(image.Preview.Access);
        Assert.Equal(2, await db.StorageOperationRecoveries.CountAsync());
        var recoveries = await db.StorageOperationRecoveries.OrderBy(recovery => recovery.CreatedAt).ToListAsync();
        Assert.All(recoveries, recovery =>
        {
            Assert.Equal(StorageOperationRecoveryType.MissingObject, recovery.OperationType);
            Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
            Assert.Equal(artifact.ArtifactId, recovery.ArtifactId);
            Assert.Equal(seeded.Image.ArtifactImageId, recovery.ArtifactImageId);
            Assert.Equal("Stored image rendition is missing.", recovery.FailureSummary);
        });
        Assert.Contains(seeded.Thumbnail!.ObjectKey, recoveries.SelectMany(recovery => recovery.ObjectKeys));
        Assert.Contains(seeded.Preview!.ObjectKey, recoveries.SelectMany(recovery => recovery.ObjectKeys));
    }

    [Fact]
    public async Task Read_stream_requires_exact_photography_view_permission()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var seeded = AddImageWithDerivatives(db, artifact);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        storage.AddObject(seeded.Preview!.ObjectKey, [9, 8, 7], "image/jpeg");
        var useCase = CreateUseCase(db, storage, [PermissionNames.PhotographyUpload, PermissionNames.PhotographyManage]);

        var result = await useCase.ReadArtifactImageRendition(new ReadArtifactImageRenditionQuery(
            seeded.Image.ArtifactImageId,
            PhotographyImageRendition.Preview));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Empty(storage.ReadCalls);
    }

    [Fact]
    public async Task Read_stream_returns_safe_content_without_storage_internals_or_short_lived_access()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var seeded = AddImageWithDerivatives(db, artifact);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        storage.AddObject(seeded.Preview!.ObjectKey, [9, 8, 7], "image/jpeg");
        var useCase = CreateUseCase(db, storage, [PermissionNames.PhotographyView]);

        var result = await useCase.ReadArtifactImageRendition(new ReadArtifactImageRenditionQuery(
            seeded.Image.ArtifactImageId,
            PhotographyImageRendition.Preview));

        Assert.True(result.Succeeded);
        await using var read = result.Value!;
        Assert.Equal(PhotographyImageStreamStatus.Available, read.Status);
        Assert.Equal("image/jpeg", read.ContentType);
        Assert.Equal(3, read.ContentLength);
        Assert.Equal("front-preview.jpg", read.Filename);
        using var copy = new MemoryStream();
        await read.Content!.CopyToAsync(copy);
        Assert.Equal([9, 8, 7], copy.ToArray());
        Assert.Equal([seeded.Preview.ObjectKey], storage.ReadCalls);
        Assert.Empty(storage.ShortLivedAccessCalls);
        AssertPublicReadContractHasNoStorageInternals();
    }

    [Fact]
    public async Task Read_stream_returns_controlled_unavailable_and_recovery_for_missing_binary()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var seeded = AddImageWithDerivatives(db, artifact);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        var useCase = CreateUseCase(db, storage, [PermissionNames.PhotographyView]);

        var result = await useCase.ReadArtifactImageRendition(new ReadArtifactImageRenditionQuery(
            seeded.Image.ArtifactImageId,
            PhotographyImageRendition.Preview));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyImageStreamStatus.Unavailable, result.Value!.Status);
        Assert.Null(result.Value.Content);
        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryType.MissingObject, recovery.OperationType);
        Assert.Equal(seeded.Preview!.ObjectKey, Assert.Single(recovery.ObjectKeys));
    }

    [Fact]
    public async Task Deleted_images_and_missing_derivative_metadata_are_not_streamed()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var deleted = AddImageWithDerivatives(db, artifact, status: ArtifactImageStatus.Deleted);
        var withoutPreview = AddImageWithDerivatives(db, artifact, addPreview: false);
        await db.SaveChangesAsync();
        var storage = new GalleryReadFakeStorage();
        storage.AddObject(deleted.Preview!.ObjectKey, [1], "image/jpeg");
        var useCase = CreateUseCase(db, storage, [PermissionNames.PhotographyView]);

        var deletedRead = await useCase.ReadArtifactImageRendition(new ReadArtifactImageRenditionQuery(
            deleted.Image.ArtifactImageId,
            PhotographyImageRendition.Preview));
        var missingMetadataRead = await useCase.ReadArtifactImageRendition(new ReadArtifactImageRenditionQuery(
            withoutPreview.Image.ArtifactImageId,
            PhotographyImageRendition.Preview));

        Assert.True(deletedRead.Succeeded);
        Assert.Equal(PhotographyImageStreamStatus.NotFound, deletedRead.Value!.Status);
        Assert.True(missingMetadataRead.Succeeded);
        Assert.Equal(PhotographyImageStreamStatus.NotFound, missingMetadataRead.Value!.Status);
        Assert.Empty(storage.ReadCalls);
        Assert.Equal(0, await db.StorageOperationRecoveries.CountAsync());
    }

    private static ViewArtifactImagesUseCase CreateUseCase(
        MuseumDbContext db,
        GalleryReadFakeStorage storage,
        IReadOnlyCollection<string> permissions) =>
        new(db, new FakeCurrentActorPermissionChecker(permissions), storage, new PhotographyGalleryMapper());

    private static SeededGalleryImage AddImageWithDerivatives(
        MuseumDbContext db,
        Artifact artifact,
        PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation,
        ArtifactImageStatus status = ArtifactImageStatus.Available,
        bool addThumbnail = true,
        bool addPreview = true)
    {
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact, purpose);
        var image = ArtifactImage.Create(
            artifact.ArtifactId,
            set.PhotographySetId,
            ImageStorageObjectKey.Create($"artifact-images/application/{Guid.NewGuid():N}/original.jpg"),
            "front.jpg",
            "image/jpeg",
            128,
            800,
            600,
            "photographer-1",
            PhotographyRequestApplicationTestHost.CompletedAt);

        ArtifactImageDerivative? thumbnail = null;
        if (addThumbnail)
        {
            thumbnail = ArtifactImageDerivative.Create(
                image.ArtifactImageId,
                ImageDerivativeKind.Thumbnail,
                ImageStorageObjectKey.Create($"artifact-images/application/{Guid.NewGuid():N}/thumbnail.jpg"),
                "image/jpeg",
                32,
                120,
                90);
            image.AddDerivative(thumbnail);
        }

        ArtifactImageDerivative? preview = null;
        if (addPreview)
        {
            preview = ArtifactImageDerivative.Create(
                image.ArtifactImageId,
                ImageDerivativeKind.Preview,
                ImageStorageObjectKey.Create($"artifact-images/application/{Guid.NewGuid():N}/preview.jpg"),
                "image/jpeg",
                64,
                640,
                480);
            image.AddDerivative(preview);
        }

        if (status == ArtifactImageStatus.DeletePending)
        {
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        }
        else if (status == ArtifactImageStatus.Deleted)
        {
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod);
        }

        db.ArtifactImages.Add(image);
        return new SeededGalleryImage(set, image, thumbnail, preview);
    }

    private static void AssertPublicGalleryContractHasNoStorageInternals()
    {
        var types = new[]
        {
            typeof(ArtifactImageGalleryDto),
            typeof(PhotographyGalleryArtifactDto),
            typeof(PhotographyGalleryImageDto),
            typeof(PhotographyGalleryRenditionDto),
            typeof(PhotographyImageAccessReferenceDto)
        };
        AssertNoPublicStorageInternals(types);
    }

    private static void AssertPublicReadContractHasNoStorageInternals() =>
        AssertNoPublicStorageInternals([typeof(ArtifactImageSafeReadResult)]);

    private static void AssertNoPublicStorageInternals(IReadOnlyCollection<Type> types)
    {
        var forbiddenFragments = new[] { "ObjectKey", "Bucket", "Endpoint", "Presigned", "Minio" };
        var memberNames = types
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name))
            .ToArray();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}

internal sealed record SeededGalleryImage(
    PhotographySet Set,
    ArtifactImage Image,
    ArtifactImageDerivative? Thumbnail,
    ArtifactImageDerivative? Preview);

internal sealed class GalleryReadFakeStorage : IArtifactImageStorage
{
    private readonly Dictionary<string, GalleryStoredObject> objects = new(StringComparer.Ordinal);

    public List<ImageStorageObjectKey> StatCalls { get; } = [];
    public List<ImageStorageObjectKey> ReadCalls { get; } = [];
    public List<ImageStorageObjectKey> ShortLivedAccessCalls { get; } = [];

    public void AddObject(ImageStorageObjectKey key, byte[] bytes, string contentType) =>
        objects[key.Value] = new GalleryStoredObject(
            bytes,
            new ArtifactImageStoredObjectMetadata(key, contentType, bytes.LongLength, "sha256-test", DateTimeOffset.UtcNow));

    public ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        string? checksum,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        ImageDerivativeKind derivativeKind,
        string? checksum,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageStatResult> StatAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default)
    {
        StatCalls.Add(objectKey);
        return ValueTask.FromResult(objects.TryGetValue(objectKey.Value, out var stored)
            ? ArtifactImageStorageStatResult.Success(stored.Metadata)
            : ArtifactImageStorageStatResult.Failed(ArtifactImageStorageResultKind.NotFound, "Storage.NotFound", "Image storage is currently unavailable."));
    }

    public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default)
    {
        ReadCalls.Add(objectKey);
        return ValueTask.FromResult(objects.TryGetValue(objectKey.Value, out var stored)
            ? ArtifactImageStorageReadResult.Success(new ArtifactImageStorageReadStream(new MemoryStream(stored.Bytes, writable: false), stored.Metadata))
            : ArtifactImageStorageReadResult.Failed(ArtifactImageStorageResultKind.NotFound, "Storage.NotFound", "Image storage is currently unavailable."));
    }

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(
        ImageStorageObjectKey objectKey,
        TimeSpan requestedLifetime,
        CancellationToken cancellationToken = default)
    {
        ShortLivedAccessCalls.Add(objectKey);
        return ValueTask.FromResult(ArtifactImageShortLivedReadAccessResult.Failed(
            ArtifactImageStorageResultKind.NotSupported,
            "Storage.ShortLivedAccessNotSupported",
            "Direct image storage access is not supported."));
    }

    public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(
        ImageStorageObjectKey originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed record GalleryStoredObject(byte[] Bytes, ArtifactImageStoredObjectMetadata Metadata);

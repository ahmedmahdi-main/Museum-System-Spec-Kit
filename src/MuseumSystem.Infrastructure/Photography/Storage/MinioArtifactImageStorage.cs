using System.Net.Http;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Infrastructure.Photography.Storage;

public sealed class MinioArtifactImageStorage : IArtifactImageStorage
{
    private const string StaffUnavailable = "Image storage is currently unavailable.";
    private readonly MinioArtifactImageStorageOptions options;
    private readonly IMinioClient client;

    public MinioArtifactImageStorage(IOptions<MinioArtifactImageStorageOptions> options)
    {
        this.options = options.Value;
        client = CreateClient(this.options);
    }

    public async ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        string? checksum,
        CancellationToken cancellationToken = default) =>
        await StoreAsync(objectKey, content, contentType, lengthBytes, checksum, cancellationToken);

    public async ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        ImageDerivativeKind derivativeKind,
        string? checksum,
        CancellationToken cancellationToken = default) =>
        await StoreAsync(objectKey, content, contentType, lengthBytes, checksum, cancellationToken);

    public async ValueTask<ArtifactImageStorageStatResult> StatAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectKey);

        try
        {
            var stat = await client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey.Value), cancellationToken);

            return ArtifactImageStorageStatResult.Success(new ArtifactImageStoredObjectMetadata(
                objectKey,
                string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType,
                stat.Size,
                null,
                stat.LastModified));
        }
        catch (Exception ex) when (TryMapFailure(ex, out var failure))
        {
            return ArtifactImageStorageStatResult.Failed(failure.Kind, failure.Code, failure.StaffFacingMessage, failure.OperationalSummary);
        }
    }

    public async ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectKey);

        var stat = await StatAsync(objectKey, cancellationToken);
        if (!stat.Exists)
        {
            return ArtifactImageStorageReadResult.Failed(
                stat.Kind,
                stat.Failure?.Code ?? "Storage.ReadUnavailable",
                stat.Failure?.StaffFacingMessage ?? StaffUnavailable,
                stat.Failure?.OperationalSummary);
        }

        try
        {
            var buffer = new MemoryStream();
            await client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey.Value)
                .WithCallbackStream(stream => stream.CopyTo(buffer)), cancellationToken);
            buffer.Position = 0;

            return ArtifactImageStorageReadResult.Success(new ArtifactImageStorageReadStream(buffer, stat.StoredObject!));
        }
        catch (Exception ex) when (TryMapFailure(ex, out var failure))
        {
            return ArtifactImageStorageReadResult.Failed(failure.Kind, failure.Code, failure.StaffFacingMessage, failure.OperationalSummary);
        }
    }

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(
        ImageStorageObjectKey objectKey,
        TimeSpan requestedLifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectKey);
        return ValueTask.FromResult(ArtifactImageShortLivedReadAccessResult.Failed(
            ArtifactImageStorageResultKind.NotSupported,
            "Storage.ShortLivedAccessNotSupported",
            "Direct image storage access is not supported."));
    }

    public async ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectKey);

        var stat = await StatAsync(objectKey, cancellationToken);
        if (stat.Kind == ArtifactImageStorageResultKind.NotFound)
        {
            return ArtifactImageStorageDeleteResult.Failed(objectKey, ArtifactImageStorageResultKind.NotFound, "Storage.NotFound", "Stored object was not found.");
        }

        if (!stat.Exists)
        {
            return ArtifactImageStorageDeleteResult.Failed(
                objectKey,
                stat.Kind,
                stat.Failure?.Code ?? "Storage.DeleteUnavailable",
                stat.Failure?.StaffFacingMessage ?? StaffUnavailable,
                stat.Failure?.OperationalSummary);
        }

        try
        {
            await client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey.Value), cancellationToken);
            return ArtifactImageStorageDeleteResult.Success(objectKey);
        }
        catch (Exception ex) when (TryMapFailure(ex, out var failure))
        {
            return ArtifactImageStorageDeleteResult.Failed(objectKey, failure.Kind, failure.Code, failure.StaffFacingMessage, failure.OperationalSummary);
        }
    }

    public async ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(
        ImageStorageObjectKey originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalObjectKey);
        ArgumentNullException.ThrowIfNull(derivativeObjectKeys);

        var results = new List<ArtifactImageStorageDeleteResult>(1 + derivativeObjectKeys.Count);
        foreach (var objectKey in new[] { originalObjectKey }.Concat(derivativeObjectKeys))
        {
            results.Add(await DeleteObjectAsync(objectKey, cancellationToken));
        }

        if (results.All(result => result.Kind is ArtifactImageStorageResultKind.Success or ArtifactImageStorageResultKind.NotFound))
        {
            return ArtifactImageObjectsDeleteResult.Success(results);
        }

        var firstFailure = results
            .First(result => result.Kind is not ArtifactImageStorageResultKind.Success and not ArtifactImageStorageResultKind.NotFound)
            .Failure;
        return ArtifactImageObjectsDeleteResult.PartialFailure(
            results,
            "Storage.DeletePartialFailure",
            "One or more stored image objects could not be deleted.",
            firstFailure?.OperationalSummary);
    }

    private async Task<ArtifactImageStorageWriteResult> StoreAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        string? checksum,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectKey);
        ArgumentNullException.ThrowIfNull(content);

        if (lengthBytes <= 0)
        {
            return ArtifactImageStorageWriteResult.Failed(ArtifactImageStorageResultKind.PermanentFailure, "Storage.InvalidLength", "Stored object length is invalid.");
        }

        var existing = await StatAsync(objectKey, cancellationToken);
        if (existing.Exists)
        {
            return ArtifactImageStorageWriteResult.Failed(ArtifactImageStorageResultKind.AlreadyExists, "Storage.ObjectAlreadyExists", "Stored object already exists.");
        }

        if (existing.Kind != ArtifactImageStorageResultKind.NotFound)
        {
            return ArtifactImageStorageWriteResult.Failed(
                existing.Kind,
                existing.Failure?.Code ?? "Storage.StatBeforeWriteFailed",
                existing.Failure?.StaffFacingMessage ?? StaffUnavailable,
                existing.Failure?.OperationalSummary);
        }

        try
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["If-None-Match"] = "*"
            };
            if (!string.IsNullOrWhiteSpace(checksum))
            {
                headers["x-amz-meta-sha256"] = checksum;
            }

            await client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey.Value)
                .WithStreamData(content)
                .WithObjectSize(lengthBytes)
                .WithContentType(contentType)
                .WithHeaders(headers), cancellationToken);

            var stored = await StatAsync(objectKey, cancellationToken);
            return stored.Exists
                ? ArtifactImageStorageWriteResult.Success(stored.StoredObject!)
                : ArtifactImageStorageWriteResult.Failed(ArtifactImageStorageResultKind.RetryableFailure, "Storage.WriteVerificationFailed", StaffUnavailable);
        }
        catch (Exception ex) when (TryMapFailure(ex, out var failure))
        {
            return ArtifactImageStorageWriteResult.Failed(failure.Kind, failure.Code, failure.StaffFacingMessage, failure.OperationalSummary);
        }
    }

    private static IMinioClient CreateClient(MinioArtifactImageStorageOptions options)
    {
        var client = new MinioClient()
            .WithEndpoint(NormalizeEndpoint(options.Endpoint))
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseTls)
            .WithTimeout(options.RequestTimeoutSeconds * 1000);

        if (!string.IsNullOrWhiteSpace(options.Region))
        {
            client.WithRegion(options.Region);
        }

        return client.Build();
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        return endpoint;
    }


    private static bool TryMapFailure(Exception exception, out ArtifactImageStorageFailure failure)
    {
        failure = exception switch
        {
            ObjectNotFoundException => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.NotFound,
                "Storage.NotFound",
                "Stored object was not found.",
                SafeSummary(exception)),
            BucketNotFoundException => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured,
                "Storage.BucketMisconfigured",
                StaffUnavailable,
                SafeSummary(exception)),
            AuthorizationException or AccessDeniedException => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured,
                "Storage.UnauthorizedOrMisconfigured",
                StaffUnavailable,
                SafeSummary(exception)),
            InvalidEndpointException or InvalidBucketNameException or ConnectionException => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured,
                "Storage.Misconfigured",
                StaffUnavailable,
                SafeSummary(exception)),
            InternalClientException or HttpRequestException or IOException or TimeoutException => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.RetryableFailure,
                "Storage.RetryableFailure",
                StaffUnavailable,
                SafeSummary(exception)),
            MinioException minioException when IsAlreadyExists(minioException) => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.AlreadyExists,
                "Storage.ObjectAlreadyExists",
                "Stored object already exists.",
                SafeSummary(exception)),
            MinioException => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.PermanentFailure,
                "Storage.ProviderFailure",
                StaffUnavailable,
                SafeSummary(exception)),
            _ => new ArtifactImageStorageFailure(
                ArtifactImageStorageResultKind.RetryableFailure,
                "Storage.UnknownFailure",
                StaffUnavailable,
                SafeSummary(exception))
        };

        return true;
    }

    private static bool IsAlreadyExists(MinioException exception) =>
        exception.Message.Contains("Precondition", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("already", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("exists", StringComparison.OrdinalIgnoreCase);

    private static string SafeSummary(Exception exception)
    {
        var value = $"{exception.GetType().Name}: {exception.Message}".Trim();
        return value.Length <= 500 ? value : value[..500];
    }
}

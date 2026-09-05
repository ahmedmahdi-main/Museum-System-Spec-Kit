using System.Net.Http;
using Minio.Exceptions;
using MuseumSystem.Application.Modules.Photography.Storage;

namespace MuseumSystem.Infrastructure.Photography.Storage;

public static class MinioStorageErrorMapper
{
    public static bool TryMap(Exception exception, out ArtifactImageStorageFailure failure)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
        {
            failure = null!;
            return false;
        }

        failure = exception switch
        {
            ObjectNotFoundException => Failure(
                ArtifactImageStorageResultKind.NotFound,
                "Storage.NotFound",
                "Stored object was not found.",
                "Object storage reported the object was missing."),
            BucketNotFoundException => Failure(
                ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured,
                "Storage.BucketMisconfigured",
                StaffUnavailable,
                "Object storage reported an unavailable bucket configuration."),
            AuthorizationException or AccessDeniedException => Failure(
                ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured,
                "Storage.UnauthorizedOrMisconfigured",
                StaffUnavailable,
                "Object storage rejected the configured authorization."),
            InvalidEndpointException or InvalidBucketNameException => Failure(
                ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured,
                "Storage.Misconfigured",
                StaffUnavailable,
                "Object storage reported invalid provider configuration."),
            ConnectionException or InternalClientException or HttpRequestException or IOException or TimeoutException => Failure(
                ArtifactImageStorageResultKind.RetryableFailure,
                "Storage.RetryableFailure",
                TemporaryUnavailable,
                "Object storage reported a transient availability failure."),
            MinioException minioException when IsAlreadyExists(minioException) => Failure(
                ArtifactImageStorageResultKind.AlreadyExists,
                "Storage.ObjectAlreadyExists",
                "Stored object already exists.",
                "Object storage reported an object identity conflict."),
            MinioException => Failure(
                ArtifactImageStorageResultKind.PermanentFailure,
                "Storage.ProviderFailure",
                StaffUnavailable,
                "Object storage reported a non-transient provider failure."),
            _ => Failure(
                ArtifactImageStorageResultKind.RetryableFailure,
                "Storage.UnknownFailure",
                TemporaryUnavailable,
                "Object storage reported an unclassified operational failure.")
        };

        return true;
    }


    public static bool TryMap(Exception exception, CancellationToken cancellationToken, out ArtifactImageStorageFailure failure)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            failure = Failure(
                ArtifactImageStorageResultKind.RetryableFailure,
                "Storage.RetryableFailure",
                TemporaryUnavailable,
                "Object storage reported a transient availability failure.");
            return true;
        }

        return TryMap(exception, out failure);
    }
    private static ArtifactImageStorageFailure Failure(
        ArtifactImageStorageResultKind kind,
        string code,
        string staffFacingMessage,
        string operationalSummary) =>
        new(kind, code, staffFacingMessage, operationalSummary);

    private static bool IsAlreadyExists(MinioException exception) =>
        exception.Message.Contains("Precondition", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("already", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("exists", StringComparison.OrdinalIgnoreCase);

    private const string StaffUnavailable = "Image storage is currently unavailable.";
    private const string TemporaryUnavailable = "Image storage is temporarily unavailable. Please try again.";
}

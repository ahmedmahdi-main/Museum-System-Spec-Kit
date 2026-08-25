using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography.Storage;

public interface IArtifactImageStorage
{
    ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        string? checksum,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(
        ImageStorageObjectKey objectKey,
        Stream content,
        string contentType,
        long lengthBytes,
        ImageDerivativeKind derivativeKind,
        string? checksum,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageStorageStatResult> StatAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(
        ImageStorageObjectKey objectKey,
        TimeSpan requestedLifetime,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(
        ImageStorageObjectKey objectKey,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(
        ImageStorageObjectKey originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        CancellationToken cancellationToken = default);
}

public enum ArtifactImageStorageResultKind
{
    Success = 1,
    NotFound = 2,
    AlreadyExists = 3,
    RetryableFailure = 4,
    PermanentFailure = 5,
    UnauthorizedOrMisconfigured = 6,
    NotSupported = 7,
    PartialFailure = 8
}

public sealed record ArtifactImageStorageFailure(
    ArtifactImageStorageResultKind Kind,
    string Code,
    string StaffFacingMessage,
    string? OperationalSummary = null);

public sealed record ArtifactImageStoredObjectMetadata(
    ImageStorageObjectKey ObjectKey,
    string ContentType,
    long LengthBytes,
    string? Checksum,
    DateTimeOffset? LastModifiedAt);

public sealed record ArtifactImageStorageWriteResult(
    ArtifactImageStorageResultKind Kind,
    ArtifactImageStoredObjectMetadata? StoredObject,
    ArtifactImageStorageFailure? Failure)
{
    public bool Succeeded => Kind == ArtifactImageStorageResultKind.Success;

    public static ArtifactImageStorageWriteResult Success(ArtifactImageStoredObjectMetadata storedObject) =>
        new(ArtifactImageStorageResultKind.Success, storedObject ?? throw new ArgumentNullException(nameof(storedObject)), null);

    public static ArtifactImageStorageWriteResult Failed(ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string? operationalSummary = null) =>
        new(kind, null, new ArtifactImageStorageFailure(kind, code, staffFacingMessage, operationalSummary));
}

public sealed record ArtifactImageStorageStatResult(
    ArtifactImageStorageResultKind Kind,
    ArtifactImageStoredObjectMetadata? StoredObject,
    ArtifactImageStorageFailure? Failure)
{
    public bool Exists => Kind == ArtifactImageStorageResultKind.Success;

    public static ArtifactImageStorageStatResult Success(ArtifactImageStoredObjectMetadata storedObject) =>
        new(ArtifactImageStorageResultKind.Success, storedObject ?? throw new ArgumentNullException(nameof(storedObject)), null);

    public static ArtifactImageStorageStatResult Failed(ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string? operationalSummary = null) =>
        new(kind, null, new ArtifactImageStorageFailure(kind, code, staffFacingMessage, operationalSummary));
}

public sealed record ArtifactImageStorageReadStream(
    Stream Content,
    ArtifactImageStoredObjectMetadata Metadata);

public sealed record ArtifactImageStorageReadResult(
    ArtifactImageStorageResultKind Kind,
    ArtifactImageStorageReadStream? ReadStream,
    ArtifactImageStorageFailure? Failure)
{
    public bool Succeeded => Kind == ArtifactImageStorageResultKind.Success;

    public static ArtifactImageStorageReadResult Success(ArtifactImageStorageReadStream readStream) =>
        new(ArtifactImageStorageResultKind.Success, readStream ?? throw new ArgumentNullException(nameof(readStream)), null);

    public static ArtifactImageStorageReadResult Failed(ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string? operationalSummary = null) =>
        new(kind, null, new ArtifactImageStorageFailure(kind, code, staffFacingMessage, operationalSummary));
}

public sealed record ArtifactImageShortLivedReadAccess(
    string OpaqueAccessReference,
    DateTimeOffset ExpiresAt,
    string ContentType);

public sealed record ArtifactImageShortLivedReadAccessResult(
    ArtifactImageStorageResultKind Kind,
    ArtifactImageShortLivedReadAccess? Access,
    ArtifactImageStorageFailure? Failure)
{
    public bool Succeeded => Kind == ArtifactImageStorageResultKind.Success;

    public static ArtifactImageShortLivedReadAccessResult Success(ArtifactImageShortLivedReadAccess access) =>
        new(ArtifactImageStorageResultKind.Success, access ?? throw new ArgumentNullException(nameof(access)), null);

    public static ArtifactImageShortLivedReadAccessResult Failed(ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string? operationalSummary = null) =>
        new(kind, null, new ArtifactImageStorageFailure(kind, code, staffFacingMessage, operationalSummary));
}

public sealed record ArtifactImageStorageDeleteResult(
    ImageStorageObjectKey ObjectKey,
    ArtifactImageStorageResultKind Kind,
    ArtifactImageStorageFailure? Failure)
{
    public bool Succeeded => Kind == ArtifactImageStorageResultKind.Success;

    public static ArtifactImageStorageDeleteResult Success(ImageStorageObjectKey objectKey) =>
        new(objectKey ?? throw new ArgumentNullException(nameof(objectKey)), ArtifactImageStorageResultKind.Success, null);

    public static ArtifactImageStorageDeleteResult Failed(ImageStorageObjectKey objectKey, ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string? operationalSummary = null) =>
        new(objectKey ?? throw new ArgumentNullException(nameof(objectKey)), kind, new ArtifactImageStorageFailure(kind, code, staffFacingMessage, operationalSummary));
}

public sealed record ArtifactImageObjectsDeleteResult(
    ArtifactImageStorageResultKind Kind,
    IReadOnlyList<ArtifactImageStorageDeleteResult> ObjectResults,
    ArtifactImageStorageFailure? Failure)
{
    public bool Succeeded => Kind == ArtifactImageStorageResultKind.Success;

    public static ArtifactImageObjectsDeleteResult Success(IReadOnlyList<ArtifactImageStorageDeleteResult> objectResults) =>
        new(ArtifactImageStorageResultKind.Success, objectResults ?? throw new ArgumentNullException(nameof(objectResults)), null);

    public static ArtifactImageObjectsDeleteResult PartialFailure(IReadOnlyList<ArtifactImageStorageDeleteResult> objectResults, string code, string staffFacingMessage, string? operationalSummary = null) =>
        new(ArtifactImageStorageResultKind.PartialFailure, objectResults ?? throw new ArgumentNullException(nameof(objectResults)), new ArtifactImageStorageFailure(ArtifactImageStorageResultKind.PartialFailure, code, staffFacingMessage, operationalSummary));
}

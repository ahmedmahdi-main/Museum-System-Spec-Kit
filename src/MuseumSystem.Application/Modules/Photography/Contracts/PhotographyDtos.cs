using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography.Contracts;

public sealed record PhotographySetSummaryDto(
    Guid PhotographySetId,
    Guid ArtifactId,
    PhotographyPurpose Purpose,
    DateOnly PhotographyDate,
    string PhotographerUserId,
    DateTimeOffset CreatedAt,
    string? CreatedByUserId,
    int ImageCount,
    int ConcurrencyToken);

public sealed record ArtifactImageDerivativeSummaryDto(
    Guid ArtifactImageDerivativeId,
    ImageDerivativeKind Kind,
    string ContentType,
    long FileSizeBytes,
    int PixelWidth,
    int PixelHeight,
    DateTimeOffset CreatedAt);

public sealed record ArtifactImageSummaryDto(
    Guid ArtifactImageId,
    Guid ArtifactId,
    Guid PhotographySetId,
    string OriginalFilename,
    string ContentType,
    long FileSizeBytes,
    int PixelWidth,
    int PixelHeight,
    string UploadedByUserId,
    DateTimeOffset UploadedAt,
    string? Caption,
    ArtifactImageStatus Status,
    IReadOnlyList<ArtifactImageDerivativeSummaryDto> Derivatives,
    int ConcurrencyToken);

public sealed record ArtifactImageViewingAccessDto(
    Guid ArtifactImageId,
    ImageDerivativeKind? DerivativeKind,
    string OpaqueAccessReference,
    string ContentType,
    long? FileSizeBytes,
    int? PixelWidth,
    int? PixelHeight,
    DateTimeOffset? ExpiresAt);

public sealed record PrimaryImageSummaryDto(
    Guid ArtifactId,
    Guid? PrimaryImageId,
    ArtifactImageSummaryDto? PrimaryImage,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByUserId,
    int ConcurrencyToken);

public sealed record PhotographyUploadFileResultDto(
    int ClientFileOrdinal,
    string OriginalFilename,
    PhotographyUploadFileOutcomeStatus Status,
    Guid? ArtifactImageId,
    string StaffFacingMessage,
    ArtifactImageSummaryDto? Image);

public sealed record PhotographyUploadOperationResultDto(
    Guid PhotographyUploadOperationId,
    PhotographyUploadOperationKind OperationKind,
    PhotographyUploadOperationStatus Status,
    Guid ArtifactId,
    Guid? PhotographySetId,
    PhotographySetSummaryDto? PhotographySet,
    IReadOnlyList<PhotographyUploadFileResultDto> FileResults,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record ArtifactImageDeletionResultDto(
    Guid ArtifactImageId,
    Guid ArtifactId,
    ArtifactImageDeletionMode DeletionMode,
    DateTimeOffset DeletedAt,
    string DeletedByUserId,
    string StaffFacingMessage,
    bool StorageRecoveryRequired,
    StorageOperationRecoveryStatus? StorageRecoveryStatus);

public sealed record StorageRecoveryStatusDto(
    StorageOperationRecoveryStatus Status,
    string StaffFacingMessage,
    DateTimeOffset? LastAttemptedAt,
    DateTimeOffset? ResolvedAt);

using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations.Contracts;

public sealed record DeliveryEligibilityRequest(
    IReadOnlyList<Guid> ArtifactIds,
    MovementRecipientType RecipientType,
    Guid? DestinationLocationId = null);

public sealed record DeliverArtifactsRequest(
    IReadOnlyList<Guid> ArtifactIds,
    MovementRecipientType RecipientType,
    string? RecipientName,
    Guid? DestinationLocationId,
    string Purpose,
    string? Note = null);

public sealed record ReturnEligibilityRequest(
    IReadOnlyList<Guid> ArtifactIds,
    Guid ReturnLocationId);

public sealed record ReturnArtifactsRequest(
    IReadOnlyList<Guid> ArtifactIds,
    Guid ReturnLocationId,
    string? Note = null);

public sealed record ArtifactEligibilityDto(
    Guid ArtifactId,
    string MuseumNumber,
    bool IsEligible,
    string Message);

public sealed record MovementPreviewDto(
    IReadOnlyList<ArtifactEligibilityDto> Artifacts,
    bool CanCommit,
    string Message);

public sealed record MovementOperationDto(
    Guid MovementGroupId,
    IReadOnlyList<Guid> ArtifactIds,
    string Message);

public sealed record MovementHistoryDto(
    Guid MovementId,
    MovementType MovementType,
    Guid MovementGroupId,
    string MuseumNumber,
    MovementRecipientType? RecipientType,
    string? RecipientName,
    string? Purpose,
    Guid? ReturnLocationId,
    string? ReturnLocationName,
    string? Note,
    DateTimeOffset OccurredAt,
    string? RecordedBy);

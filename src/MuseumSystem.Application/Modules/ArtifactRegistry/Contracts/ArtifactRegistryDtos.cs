using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;

public sealed record CategoryDto(Guid CategoryId, string CategoryCode, string NameArabic, string? Description, bool IsActive);
public sealed record CreateCategoryRequest(string CategoryCode, string NameArabic, string? Description);
public sealed record UpdateCategoryRequest(Guid CategoryId, string CategoryCode, string NameArabic, string? Description);

public sealed record ArtifactDetailsDto(
    Guid ArtifactId,
    Guid CategoryId,
    string CategoryCode,
    int ItemNumber,
    string MuseumNumber,
    string BasicDescription,
    ArtifactCurrentStatus CurrentStatus,
    Guid? CurrentLocationId,
    string? CurrentLocationName,
    string? CurrentHolderType,
    string? CurrentHolderName,
    Guid? LastKnownStorageLocationId);

public sealed record ArtifactSearchResultDto(
    Guid ArtifactId,
    string MuseumNumber,
    string CategoryCode,
    int ItemNumber,
    string BasicDescription,
    ArtifactCurrentStatus CurrentStatus,
    string? CurrentLocationName,
    string? CurrentHolderName,
    Guid? LastKnownStorageLocationId);

public sealed record CreateArtifactRequest(Guid CategoryId, int ItemNumber, string BasicDescription, Guid InitialLocationId);
public sealed record UpdateArtifactBasicInfoRequest(Guid ArtifactId, string BasicDescription);

public sealed record LocationDto(Guid LocationId, string NameArabic, LocationType LocationType, Guid? ParentLocationId, bool IsActive);
public sealed record CreateLocationRequest(string NameArabic, LocationType LocationType, Guid? ParentLocationId = null);
public sealed record UpdateLocationRequest(Guid LocationId, string NameArabic, LocationType LocationType, Guid? ParentLocationId = null);

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class LocationUseCases(IMuseumDbContext dbContext, IAuditWriter? auditWriter = null)
{
    public async Task<UseCaseResult<LocationDto>> CreateLocation(CreateLocationRequest request, CancellationToken cancellationToken = default)
    {
        var requestValidation = ValidateSaveRequest(request.NameArabic);
        if (requestValidation.Count > 0)
        {
            return UseCaseResult<LocationDto>.Failure(requestValidation.ToArray());
        }

        var location = Location.Create(request.NameArabic, request.LocationType, request.ParentLocationId);
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Location.Create", location.LocationId, $"Created {location.LocationType} location.", $"ParentLocationId={location.ParentLocationId}", cancellationToken);
        return UseCaseResult<LocationDto>.Success(ToDto(location));
    }

    public async Task<UseCaseResult<LocationDto>> UpdateLocation(UpdateLocationRequest request, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.Locations.FindAsync([request.LocationId], cancellationToken);
        if (location is null)
        {
            return UseCaseResult<LocationDto>.Failure(new ValidationIssue("Location.NotFound", "الموقع غير موجود.", nameof(request.LocationId)));
        }

        var requestValidation = ValidateSaveRequest(request.NameArabic);
        if (requestValidation.Count > 0)
        {
            return UseCaseResult<LocationDto>.Failure(requestValidation.ToArray());
        }

        location.Update(request.NameArabic, request.LocationType, request.ParentLocationId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Location.Update", location.LocationId, $"Updated {location.LocationType} location.", $"ParentLocationId={location.ParentLocationId}", cancellationToken);
        return UseCaseResult<LocationDto>.Success(ToDto(location));
    }

    public async Task<UseCaseResult> DisableLocationForNewUse(Guid locationId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.Locations.FindAsync([locationId], cancellationToken);
        if (location is null)
        {
            return UseCaseResult.Failure(new ValidationIssue("Location.NotFound", "الموقع غير موجود.", nameof(locationId)));
        }

        location.DisableForNewUse();
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Location.DisableForNewUse", location.LocationId, $"Disabled {location.LocationType} location for new use.", null, cancellationToken);
        return UseCaseResult.Success("تم تعطيل الموقع للاستخدام الجديد.");
    }

    public async Task<IReadOnlyList<LocationDto>> ListSelectableLocations(CancellationToken cancellationToken = default) =>
        await dbContext.Locations
            .AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.NameArabic)
            .Select(l => new LocationDto(l.LocationId, l.NameArabic, l.LocationType, l.ParentLocationId, l.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LocationDto>> ListLocations(CancellationToken cancellationToken = default) =>
        await dbContext.Locations
            .AsNoTracking()
            .OrderBy(l => l.NameArabic)
            .Select(l => new LocationDto(l.LocationId, l.NameArabic, l.LocationType, l.ParentLocationId, l.IsActive))
            .ToListAsync(cancellationToken);

    private static LocationDto ToDto(Location location) =>
        new(location.LocationId, location.NameArabic, location.LocationType, location.ParentLocationId, location.IsActive);

    private static List<ValidationIssue> ValidateSaveRequest(string nameArabic)
    {
        List<ValidationIssue> issues = [];
        if (string.IsNullOrWhiteSpace(nameArabic))
        {
            issues.Add(new ValidationIssue("Location.NameRequired", "اكتب اسم الموقع.", nameof(CreateLocationRequest.NameArabic)));
        }

        return issues;
    }

    private Task WriteAuditAsync(string actionName, Guid locationId, string summary, string? changeSummary, CancellationToken cancellationToken) =>
        auditWriter?.WriteAsync(new AuditWriteRequest(
            actionName,
            "StorehouseOperations",
            nameof(Location),
            locationId.ToString(),
            summary,
            changeSummary), cancellationToken) ?? Task.CompletedTask;
}

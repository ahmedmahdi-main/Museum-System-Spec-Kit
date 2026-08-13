using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.ArtifactRegistry;

public sealed class ArtifactWriteUseCases(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ArtifactDetailsDto>> CreateArtifact(CreateArtifactRequest request, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ArtifactCategories.FindAsync([request.CategoryId], cancellationToken);
        if (category is null || !category.IsActive)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Category.NotSelectable", "الفئة غير متاحة للاستخدام.", nameof(request.CategoryId)));
        }

        var location = await dbContext.Locations.FindAsync([request.InitialLocationId], cancellationToken);
        if (location is null || !location.IsActive || location.LocationType != LocationType.Storage)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Location.NotSelectable", "موقع الخزن غير متاح.", nameof(request.InitialLocationId)));
        }

        var duplicate = await dbContext.Artifacts.AnyAsync(a => a.CategoryId == request.CategoryId && a.ItemNumber == request.ItemNumber, cancellationToken);
        if (duplicate)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("MuseumNumber.Duplicate", "رقم القطعة مكرر داخل الفئة.", nameof(request.ItemNumber)));
        }

        var artifact = ArtifactFactory.Create(category, request.ItemNumber, request.BasicDescription, location);
        dbContext.Artifacts.Add(artifact);
        await dbContext.SaveChangesAsync(cancellationToken);

        return UseCaseResult<ArtifactDetailsDto>.Success(ToDetailsDto(artifact, category, location));
    }

    public async Task<UseCaseResult<ArtifactDetailsDto>> UpdateArtifactBasicInfo(UpdateArtifactBasicInfoRequest request, CancellationToken cancellationToken = default)
    {
        var artifact = await dbContext.Artifacts
            .Include(a => a.Category)
            .Include(a => a.CurrentLocation)
            .FirstOrDefaultAsync(a => a.ArtifactId == request.ArtifactId, cancellationToken);

        if (artifact is null || artifact.Category is null)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Artifact.NotFound", "القطعة غير موجودة.", nameof(request.ArtifactId)));
        }

        artifact.UpdateBasicDescription(request.BasicDescription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return UseCaseResult<ArtifactDetailsDto>.Success(ToDetailsDto(artifact, artifact.Category, artifact.CurrentLocation));
    }

    private static ArtifactDetailsDto ToDetailsDto(Artifact artifact, ArtifactCategory category, Location? location) =>
        new(
            artifact.ArtifactId,
            artifact.CategoryId,
            category.CategoryCode,
            artifact.ItemNumber,
            artifact.MuseumNumberDisplay,
            artifact.BasicDescription,
            artifact.CurrentStatus,
            artifact.CurrentLocationId,
            location?.NameArabic,
            artifact.CurrentHolderType,
            artifact.CurrentHolderName,
            artifact.LastKnownStorageLocationId);
}


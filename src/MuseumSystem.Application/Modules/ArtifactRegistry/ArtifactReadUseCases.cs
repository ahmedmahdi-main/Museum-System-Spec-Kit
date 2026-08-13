using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;

namespace MuseumSystem.Application.Modules.ArtifactRegistry;

public sealed class ArtifactReadUseCases(IMuseumDbContext dbContext)
{
    public async Task<IReadOnlyList<ArtifactSearchResultDto>> SearchArtifacts(string? query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim();
        var artifacts = dbContext.Artifacts
            .Include(a => a.Category)
            .Include(a => a.CurrentLocation)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            artifacts = artifacts.Where(a =>
                a.MuseumNumberDisplay.Contains(normalizedQuery) ||
                a.BasicDescription.Contains(normalizedQuery) ||
                a.ItemNumber.ToString().Contains(normalizedQuery) ||
                (a.Category != null && a.Category.CategoryCode.Contains(normalizedQuery)));
        }

        return await artifacts
            .OrderBy(a => a.Category!.CategoryCode)
            .ThenBy(a => a.ItemNumber)
            .Select(a => new ArtifactSearchResultDto(
                a.ArtifactId,
                a.MuseumNumberDisplay,
                a.Category!.CategoryCode,
                a.ItemNumber,
                a.BasicDescription,
                a.CurrentStatus,
                a.CurrentLocation != null ? a.CurrentLocation.NameArabic : null,
                a.CurrentHolderName,
                a.LastKnownStorageLocationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ArtifactDetailsDto?> GetArtifactDetails(Guid artifactId, CancellationToken cancellationToken = default) =>
        await dbContext.Artifacts
            .Include(a => a.Category)
            .Include(a => a.CurrentLocation)
            .AsNoTracking()
            .Where(a => a.ArtifactId == artifactId)
            .Select(a => new ArtifactDetailsDto(
                a.ArtifactId,
                a.CategoryId,
                a.Category!.CategoryCode,
                a.ItemNumber,
                a.MuseumNumberDisplay,
                a.BasicDescription,
                a.CurrentStatus,
                a.CurrentLocationId,
                a.CurrentLocation != null ? a.CurrentLocation.NameArabic : null,
                a.CurrentHolderType,
                a.CurrentHolderName,
                a.LastKnownStorageLocationId))
            .FirstOrDefaultAsync(cancellationToken);
}

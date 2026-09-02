using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PrimaryImageProjectionQueries(
    IMuseumDbContext dbContext,
    ICurrentActorPermissionChecker permissionChecker)
{
    public async Task<UseCaseResult<PrimaryImageProjectionDto?>> GetPrimaryImageForArtifact(
        PrimaryImageForArtifactQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ArtifactId == Guid.Empty)
        {
            return UseCaseResult<PrimaryImageProjectionDto?>.Failure(new ValidationIssue("Artifact.Required", "Artifact is required.", nameof(query.ArtifactId)));
        }

        var batch = await GetPrimaryImagesForArtifacts(new PrimaryImagesForArtifactsQuery([query.ArtifactId]), cancellationToken);
        if (!batch.Succeeded)
        {
            return UseCaseResult<PrimaryImageProjectionDto?>.Failure([.. batch.ValidationIssues]);
        }

        return UseCaseResult<PrimaryImageProjectionDto?>.Success(batch.Value![query.ArtifactId]);
    }

    public async Task<UseCaseResult<IReadOnlyDictionary<Guid, PrimaryImageProjectionDto?>>> GetPrimaryImagesForArtifacts(
        PrimaryImagesForArtifactsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyView))
        {
            return UseCaseResult<IReadOnlyDictionary<Guid, PrimaryImageProjectionDto?>>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyView));
        }

        var artifactIds = query.ArtifactIds
            .Distinct()
            .ToArray();
        if (artifactIds.Any(id => id == Guid.Empty))
        {
            return UseCaseResult<IReadOnlyDictionary<Guid, PrimaryImageProjectionDto?>>.Failure(new ValidationIssue("Artifact.Required", "Artifact is required.", nameof(query.ArtifactIds)));
        }

        if (artifactIds.Length == 0)
        {
            return UseCaseResult<IReadOnlyDictionary<Guid, PrimaryImageProjectionDto?>>.Success(new Dictionary<Guid, PrimaryImageProjectionDto?>());
        }

        var projections = artifactIds.ToDictionary(id => id, _ => (PrimaryImageProjectionDto?)null);
        var states = await dbContext.ArtifactPhotographyStates
            .AsNoTracking()
            .Where(state => artifactIds.Contains(state.ArtifactId))
            .ToListAsync(cancellationToken);
        var primaryImageIds = states
            .Where(state => state.PrimaryImageId.HasValue)
            .Select(state => state.PrimaryImageId!.Value)
            .Distinct()
            .ToArray();
        if (primaryImageIds.Length == 0)
        {
            return UseCaseResult<IReadOnlyDictionary<Guid, PrimaryImageProjectionDto?>>.Success(projections);
        }

        var images = await dbContext.ArtifactImages
            .Include(image => image.Derivatives)
            .AsNoTracking()
            .Where(image => primaryImageIds.Contains(image.ArtifactImageId))
            .ToDictionaryAsync(image => image.ArtifactImageId, cancellationToken);
        var setIds = images.Values
            .Select(image => image.PhotographySetId)
            .Distinct()
            .ToArray();
        var sets = await dbContext.PhotographySets
            .AsNoTracking()
            .Where(set => setIds.Contains(set.PhotographySetId))
            .ToDictionaryAsync(set => set.PhotographySetId, cancellationToken);

        foreach (var state in states)
        {
            if (state.PrimaryImageId is not { } primaryImageId
                || !images.TryGetValue(primaryImageId, out var image)
                || !sets.TryGetValue(image.PhotographySetId, out var set)
                || !PhotographyRules.IsPrimaryImageEligible(image, state.ArtifactId))
            {
                continue;
            }

            projections[state.ArtifactId] = ToProjection(state.ArtifactId, image, set);
        }

        return UseCaseResult<IReadOnlyDictionary<Guid, PrimaryImageProjectionDto?>>.Success(projections);
    }

    private static PrimaryImageProjectionDto ToProjection(
        Guid artifactId,
        ArtifactImage image,
        PhotographySet set) =>
        new(
            artifactId,
            image.ArtifactImageId,
            image.Caption,
            set.Purpose,
            set.PhotographyDate,
            set.PhotographerUserId,
            image.PixelWidth,
            image.PixelHeight,
            ToAccessReference(image, ImageDerivativeKind.Thumbnail),
            ToAccessReference(image, ImageDerivativeKind.Preview));

    private static PhotographyImageAccessReferenceDto? ToAccessReference(ArtifactImage image, ImageDerivativeKind kind)
    {
        var derivative = image.Derivatives
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefault(candidate => candidate.Kind == kind);
        if (derivative is null)
        {
            return null;
        }

        return new PhotographyImageAccessReferenceDto(
            image.ArtifactImageId,
            kind == ImageDerivativeKind.Thumbnail ? PhotographyImageRendition.Thumbnail : PhotographyImageRendition.Preview);
    }
}

public sealed record PrimaryImageForArtifactQuery(Guid ArtifactId);

public sealed record PrimaryImagesForArtifactsQuery(IReadOnlyCollection<Guid> ArtifactIds);

public sealed record PrimaryImageProjectionDto(
    Guid ArtifactId,
    Guid ArtifactImageId,
    string? Caption,
    PhotographyPurpose PhotographyPurpose,
    DateOnly PhotographyDate,
    string PhotographerUserId,
    int PixelWidth,
    int PixelHeight,
    PhotographyImageAccessReferenceDto? Thumbnail,
    PhotographyImageAccessReferenceDto? Preview);

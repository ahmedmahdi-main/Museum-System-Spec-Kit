using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyRequestQueries(IMuseumDbContext dbContext)
{
    public async Task<IReadOnlyList<PhotographyRequestSummaryDto>> ListPhotographyRequests(
        PhotographyRequestListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestsQuery = dbContext.PhotographyRequests.AsNoTracking();
        if (query.Status.HasValue)
        {
            requestsQuery = requestsQuery.Where(request => request.Status == query.Status.Value);
        }

        if (query.ArtifactId.HasValue)
        {
            requestsQuery = requestsQuery.Where(request => request.ArtifactId == query.ArtifactId.Value);
        }

        var requests = await requestsQuery
            .OrderBy(request => request.Status)
            .ThenBy(request => request.RequestedAt)
            .ThenBy(request => request.PhotographyRequestId)
            .ToListAsync(cancellationToken);

        return await ToSummariesAsync(requests, cancellationToken);
    }

    public async Task<UseCaseResult<PhotographyRequestSummaryDto>> GetPhotographyRequestDetail(
        Guid photographyRequestId,
        CancellationToken cancellationToken = default)
    {
        if (photographyRequestId == Guid.Empty)
        {
            return UseCaseResult<PhotographyRequestSummaryDto>.Failure(new ValidationIssue("PhotographyRequest.Required", "Photography request is required.", nameof(photographyRequestId)));
        }

        var request = await dbContext.PhotographyRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.PhotographyRequestId == photographyRequestId, cancellationToken);

        if (request is null)
        {
            return UseCaseResult<PhotographyRequestSummaryDto>.Failure(new ValidationIssue("PhotographyRequest.NotFound", "Photography request was not found.", nameof(photographyRequestId)));
        }

        var summary = (await ToSummariesAsync([request], cancellationToken)).Single();
        return UseCaseResult<PhotographyRequestSummaryDto>.Success(summary);
    }

    public async Task<UseCaseResult<IReadOnlyList<PhotographyRequestFulfillingSetSummaryDto>>> ListEligibleFulfillingSetsForRequest(
        Guid photographyRequestId,
        CancellationToken cancellationToken = default)
    {
        if (photographyRequestId == Guid.Empty)
        {
            return UseCaseResult<IReadOnlyList<PhotographyRequestFulfillingSetSummaryDto>>.Failure(new ValidationIssue("PhotographyRequest.Required", "Photography request is required.", nameof(photographyRequestId)));
        }

        var request = await dbContext.PhotographyRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.PhotographyRequestId == photographyRequestId, cancellationToken);
        if (request is null)
        {
            return UseCaseResult<IReadOnlyList<PhotographyRequestFulfillingSetSummaryDto>>.Failure(new ValidationIssue("PhotographyRequest.NotFound", "Photography request was not found.", nameof(photographyRequestId)));
        }

        var candidateSetIds = await dbContext.PhotographySets
            .AsNoTracking()
            .Where(set => set.ArtifactId == request.ArtifactId && set.Purpose == request.Purpose)
            .Select(set => set.PhotographySetId)
            .ToArrayAsync(cancellationToken);
        var candidates = await LoadSetSummariesAsync(candidateSetIds, cancellationToken);

        var eligible = candidates.Values
            .Where(set => set.AvailableImageCount > 0)
            .OrderByDescending(set => set.PhotographyDate)
            .ThenByDescending(set => set.CreatedAt)
            .ThenBy(set => set.PhotographySetId)
            .ToList();

        return UseCaseResult<IReadOnlyList<PhotographyRequestFulfillingSetSummaryDto>>.Success(eligible);
    }

    internal static PhotographyRequestDto ToRequestDto(PhotographyRequest request) => new(
        request.PhotographyRequestId,
        request.ArtifactId,
        request.Purpose,
        request.Status,
        request.RequestedByUserId,
        request.RequestedAt,
        request.FulfillingPhotographySetId,
        request.CompletedByUserId,
        request.CompletedAt,
        request.CancelledByUserId,
        request.CancelledAt,
        request.ConcurrencyToken);

    private async Task<IReadOnlyList<PhotographyRequestSummaryDto>> ToSummariesAsync(
        IReadOnlyList<PhotographyRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var artifactSummaries = await LoadArtifactSummariesAsync(
            requests.Select(request => request.ArtifactId).Distinct().ToArray(),
            cancellationToken);
        var fulfillingSetIds = requests
            .Where(request => request.FulfillingPhotographySetId.HasValue)
            .Select(request => request.FulfillingPhotographySetId!.Value)
            .Distinct()
            .ToArray();
        var fulfillingSets = await LoadSetSummariesAsync(fulfillingSetIds, cancellationToken);

        return requests
            .Select(request => new PhotographyRequestSummaryDto(
                ToRequestDto(request),
                artifactSummaries.TryGetValue(request.ArtifactId, out var artifact)
                    ? artifact
                    : PhotographyRequestArtifactSummaryDto.Missing(request.ArtifactId),
                request.FulfillingPhotographySetId.HasValue
                    && fulfillingSets.TryGetValue(request.FulfillingPhotographySetId.Value, out var set)
                    ? set
                    : null))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, PhotographyRequestArtifactSummaryDto>> LoadArtifactSummariesAsync(
        IReadOnlyCollection<Guid> artifactIds,
        CancellationToken cancellationToken)
    {
        if (artifactIds.Count == 0)
        {
            return new Dictionary<Guid, PhotographyRequestArtifactSummaryDto>();
        }

        return await dbContext.Artifacts
            .Include(artifact => artifact.Category)
            .Include(artifact => artifact.CurrentLocation)
            .AsNoTracking()
            .Where(artifact => artifactIds.Contains(artifact.ArtifactId))
            .Select(artifact => new PhotographyRequestArtifactSummaryDto(
                artifact.ArtifactId,
                artifact.CategoryId,
                artifact.Category != null ? artifact.Category.CategoryCode : string.Empty,
                artifact.Category != null ? artifact.Category.NameArabic : "Unknown category",
                artifact.ItemNumber,
                artifact.MuseumNumberDisplay,
                artifact.BasicDescription,
                artifact.CurrentStatus,
                artifact.CurrentLocationId,
                artifact.CurrentLocation != null ? artifact.CurrentLocation.NameArabic : null,
                artifact.CurrentHolderType,
                artifact.CurrentHolderName,
                artifact.LastKnownStorageLocationId))
            .ToDictionaryAsync(artifact => artifact.ArtifactId, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, PhotographyRequestFulfillingSetSummaryDto>> LoadSetSummariesAsync(
        IEnumerable<Guid> setIds,
        CancellationToken cancellationToken)
    {
        var distinctSetIds = setIds.Distinct().ToArray();
        if (distinctSetIds.Length == 0)
        {
            return new Dictionary<Guid, PhotographyRequestFulfillingSetSummaryDto>();
        }

        var availableCounts = await dbContext.ArtifactImages
            .AsNoTracking()
            .Where(image => distinctSetIds.Contains(image.PhotographySetId) && image.Status == ArtifactImageStatus.Available)
            .GroupBy(image => image.PhotographySetId)
            .Select(group => new { PhotographySetId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.PhotographySetId, group => group.Count, cancellationToken);

        var sets = await dbContext.PhotographySets
            .AsNoTracking()
            .Where(set => distinctSetIds.Contains(set.PhotographySetId))
            .ToListAsync(cancellationToken);

        return sets
            .Select(set => new PhotographyRequestFulfillingSetSummaryDto(
                set.PhotographySetId,
                set.ArtifactId,
                set.Purpose,
                set.PhotographyDate,
                set.PhotographerUserId,
                set.CreatedAt,
                set.CreatedByUserId,
                availableCounts.TryGetValue(set.PhotographySetId, out var availableImageCount) ? availableImageCount : 0,
                set.ConcurrencyToken))
            .ToDictionary(set => set.PhotographySetId);
    }
}

public sealed record PhotographyRequestListQuery(
    PhotographyRequestStatus? Status = null,
    Guid? ArtifactId = null);

public sealed record PhotographyRequestDto(
    Guid PhotographyRequestId,
    Guid ArtifactId,
    PhotographyPurpose Purpose,
    PhotographyRequestStatus Status,
    string RequestedByUserId,
    DateTimeOffset RequestedAt,
    Guid? FulfillingPhotographySetId,
    string? CompletedByUserId,
    DateTimeOffset? CompletedAt,
    string? CancelledByUserId,
    DateTimeOffset? CancelledAt,
    int ConcurrencyToken);

public sealed record PhotographyRequestArtifactSummaryDto(
    Guid ArtifactId,
    Guid CategoryId,
    string CategoryCode,
    string CategoryNameArabic,
    int ItemNumber,
    string MuseumNumber,
    string BasicDescription,
    ArtifactCurrentStatus CurrentStatus,
    Guid? CurrentLocationId,
    string? CurrentLocationName,
    string? CurrentHolderType,
    string? CurrentHolderName,
    Guid? LastKnownStorageLocationId)
{
    public static PhotographyRequestArtifactSummaryDto Missing(Guid artifactId) => new(
        artifactId,
        Guid.Empty,
        string.Empty,
        "Unknown category",
        0,
        string.Empty,
        "Artifact reference is missing.",
        ArtifactCurrentStatus.InStorage,
        null,
        null,
        null,
        null,
        null);
}

public sealed record PhotographyRequestFulfillingSetSummaryDto(
    Guid PhotographySetId,
    Guid ArtifactId,
    PhotographyPurpose Purpose,
    DateOnly PhotographyDate,
    string PhotographerUserId,
    DateTimeOffset CreatedAt,
    string? CreatedByUserId,
    int AvailableImageCount,
    int ConcurrencyToken);

public sealed record PhotographyRequestSummaryDto(
    PhotographyRequestDto Request,
    PhotographyRequestArtifactSummaryDto Artifact,
    PhotographyRequestFulfillingSetSummaryDto? FulfillingSet);

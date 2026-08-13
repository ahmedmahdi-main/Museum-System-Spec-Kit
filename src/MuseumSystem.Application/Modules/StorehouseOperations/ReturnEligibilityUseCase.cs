using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class ReturnEligibilityUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<MovementPreviewDto>> PreviewReturnEligibility(ReturnEligibilityRequest request, CancellationToken cancellationToken = default)
    {
        var locationIssue = await ValidateReturnLocationAsync(request.ReturnLocationId, cancellationToken);
        var ids = request.ArtifactIds.Distinct().ToList();
        var artifacts = await dbContext.Artifacts.Where(a => ids.Contains(a.ArtifactId)).ToListAsync(cancellationToken);
        var items = artifacts.Select(artifact =>
        {
            var reason = CurrentStateRules.GetReturnRejectionReason(artifact);
            return new ArtifactEligibilityDto(artifact.ArtifactId, artifact.MuseumNumberDisplay, reason is null && locationIssue is null, reason ?? locationIssue ?? "جاهزة للاستلام.");
        }).ToList();

        var missingIds = ids.Except(artifacts.Select(a => a.ArtifactId)).ToList();
        items.AddRange(missingIds.Select(id => new ArtifactEligibilityDto(id, string.Empty, false, "القطعة غير موجودة.")));
        var canCommit = items.Count > 0 && items.All(item => item.IsEligible);
        return UseCaseResult<MovementPreviewDto>.Success(new MovementPreviewDto(items, canCommit, canCommit ? "كل القطع مؤهلة." : "توجد قطع غير مؤهلة."));
    }

    private async Task<string?> ValidateReturnLocationAsync(Guid returnLocationId, CancellationToken cancellationToken)
    {
        var location = await dbContext.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.LocationId == returnLocationId, cancellationToken);
        return location is not null && CurrentStateRules.IsValidStorageLocation(location) ? null : "اختر موقع خزن صالح.";
    }
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class DeliveryEligibilityUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<MovementPreviewDto>> PreviewDeliveryEligibility(DeliveryEligibilityRequest request, CancellationToken cancellationToken = default)
    {
        var targetIssue = await ValidateDeliveryTargetAsync(request.RecipientType, request.DestinationLocationId, cancellationToken);
        var artifacts = await LoadArtifactsAsync(request.ArtifactIds, cancellationToken);
        var items = artifacts.Select(artifact =>
        {
            var reason = CurrentStateRules.GetDeliveryRejectionReason(artifact);
            return new ArtifactEligibilityDto(artifact.ArtifactId, artifact.MuseumNumberDisplay, reason is null && targetIssue is null, reason ?? targetIssue ?? "جاهزة للتسليم.");
        }).ToList();

        var missingIds = request.ArtifactIds.Distinct().Except(artifacts.Select(a => a.ArtifactId)).ToList();
        items.AddRange(missingIds.Select(id => new ArtifactEligibilityDto(id, string.Empty, false, "القطعة غير موجودة.")));
        var canCommit = items.Count > 0 && items.All(item => item.IsEligible);
        return UseCaseResult<MovementPreviewDto>.Success(new MovementPreviewDto(items, canCommit, canCommit ? "كل القطع مؤهلة." : "توجد قطع غير مؤهلة."));
    }

    private async Task<string?> ValidateDeliveryTargetAsync(MovementRecipientType recipientType, Guid? destinationLocationId, CancellationToken cancellationToken)
    {
        if (recipientType != MovementRecipientType.DisplayHall)
        {
            return null;
        }

        if (destinationLocationId is null)
        {
            return "اختر قاعة عرض صالحة.";
        }

        var displayLocation = await dbContext.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.LocationId == destinationLocationId, cancellationToken);
        return displayLocation is not null && CurrentStateRules.IsValidDisplayLocation(displayLocation) ? null : "اختر قاعة عرض صالحة.";
    }

    private async Task<List<Artifact>> LoadArtifactsAsync(IReadOnlyList<Guid> artifactIds, CancellationToken cancellationToken)
    {
        var ids = artifactIds.Distinct().ToList();
        return await dbContext.Artifacts
            .Include(a => a.Category)
            .Where(a => ids.Contains(a.ArtifactId))
            .ToListAsync(cancellationToken);
    }
}

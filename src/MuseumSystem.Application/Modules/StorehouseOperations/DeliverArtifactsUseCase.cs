using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class DeliverArtifactsUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<MovementOperationDto>> DeliverArtifacts(DeliverArtifactsRequest request, CancellationToken cancellationToken = default)
    {
        var ids = request.ArtifactIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Delivery.Empty", "اختر قطعة واحدة على الأقل."));
        }

        var artifacts = await dbContext.Artifacts.Where(a => ids.Contains(a.ArtifactId)).ToListAsync(cancellationToken);
        var missing = ids.Except(artifacts.Select(a => a.ArtifactId)).ToList();
        if (missing.Count > 0)
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Artifact.NotFound", "توجد قطع غير موجودة."));
        }

        var target = await ResolveDeliveryTargetAsync(request, cancellationToken);
        if (!target.Succeeded)
        {
            return UseCaseResult<MovementOperationDto>.Failure(target.ValidationIssues.ToArray());
        }

        var ineligible = artifacts.Where(a => !CurrentStateRules.CanDeliver(a)).ToList();
        if (ineligible.Count > 0)
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Delivery.Ineligible", "العملية مرفوضة: توجد قطع غير مؤهلة للتسليم."));
        }

        var groupId = Guid.NewGuid();
        foreach (var artifact in artifacts)
        {
            if (request.RecipientType == MovementRecipientType.DisplayHall)
            {
                artifact.DeliverToDisplayHall(target.Value!.DisplayLocation!);
            }
            else
            {
                artifact.DeliverToInternalHolder(request.RecipientType, target.Value!.RecipientName);
            }

            dbContext.MovementRecords.Add(MovementRecord.CreateDelivery(groupId, artifact, request.RecipientType, target.Value!.RecipientName, request.Purpose, request.Note));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<MovementOperationDto>.Conflict("تعذر الحفظ لأن حالة قطعة تغيرت. أعد المحاولة.");
        }

        return UseCaseResult<MovementOperationDto>.Success(new MovementOperationDto(groupId, ids, "تم التسليم."));
    }

    private async Task<UseCaseResult<DeliveryTarget>> ResolveDeliveryTargetAsync(DeliverArtifactsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            return UseCaseResult<DeliveryTarget>.Failure(new ValidationIssue("Delivery.PurposeRequired", "اكتب سبب التسليم.", nameof(request.Purpose)));
        }

        if (request.RecipientType == MovementRecipientType.DisplayHall)
        {
            if (request.DestinationLocationId is null)
            {
                return UseCaseResult<DeliveryTarget>.Failure(new ValidationIssue("Delivery.DisplayLocationRequired", "اختر قاعة عرض صالحة.", nameof(request.DestinationLocationId)));
            }

            var displayLocation = await dbContext.Locations.FirstOrDefaultAsync(l => l.LocationId == request.DestinationLocationId, cancellationToken);
            if (displayLocation is null || !CurrentStateRules.IsValidDisplayLocation(displayLocation))
            {
                return UseCaseResult<DeliveryTarget>.Failure(new ValidationIssue("Delivery.DisplayLocationInvalid", "اختر قاعة عرض صالحة.", nameof(request.DestinationLocationId)));
            }

            return UseCaseResult<DeliveryTarget>.Success(new DeliveryTarget(displayLocation.NameArabic, displayLocation));
        }

        if (string.IsNullOrWhiteSpace(request.RecipientName))
        {
            return UseCaseResult<DeliveryTarget>.Failure(new ValidationIssue("Delivery.RecipientRequired", "اكتب الجهة المستلمة.", nameof(request.RecipientName)));
        }

        return UseCaseResult<DeliveryTarget>.Success(new DeliveryTarget(request.RecipientName.Trim(), null));
    }

    private sealed record DeliveryTarget(string RecipientName, Location? DisplayLocation);
}

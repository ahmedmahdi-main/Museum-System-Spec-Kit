using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class ReturnArtifactsUseCase(IMuseumDbContext dbContext, IAuditWriter? auditWriter = null)
{
    public async Task<UseCaseResult<MovementOperationDto>> ReturnArtifacts(ReturnArtifactsRequest request, CancellationToken cancellationToken = default)
    {
        var ids = request.ArtifactIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Return.Empty", "اختر قطعة واحدة على الأقل."));
        }

        var returnLocation = await dbContext.Locations.FirstOrDefaultAsync(l => l.LocationId == request.ReturnLocationId, cancellationToken);
        if (returnLocation is null || !CurrentStateRules.IsValidStorageLocation(returnLocation))
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Return.LocationInvalid", "اختر موقع خزن صالح.", nameof(request.ReturnLocationId)));
        }

        var artifacts = await dbContext.Artifacts.Where(a => ids.Contains(a.ArtifactId)).ToListAsync(cancellationToken);
        var missing = ids.Except(artifacts.Select(a => a.ArtifactId)).ToList();
        if (missing.Count > 0)
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Artifact.NotFound", "توجد قطع غير موجودة."));
        }

        var ineligible = artifacts.Where(a => !CurrentStateRules.CanReturn(a)).ToList();
        if (ineligible.Count > 0)
        {
            return UseCaseResult<MovementOperationDto>.Failure(new ValidationIssue("Return.Ineligible", "العملية مرفوضة: توجد قطع غير مؤهلة للاستلام."));
        }

        var groupId = Guid.NewGuid();
        foreach (var artifact in artifacts)
        {
            artifact.ReturnToStorage(returnLocation);
            dbContext.MovementRecords.Add(MovementRecord.CreateReturn(groupId, artifact, returnLocation, request.Note));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<MovementOperationDto>.Conflict("تعذر الحفظ لأن حالة قطعة تغيرت. أعد المحاولة.");
        }

        await WriteAuditAsync(groupId, ids.Count, returnLocation.LocationId, cancellationToken);
        return UseCaseResult<MovementOperationDto>.Success(new MovementOperationDto(groupId, ids, "?? ????????."));
    }
    private Task WriteAuditAsync(Guid movementGroupId, int artifactCount, Guid returnLocationId, CancellationToken cancellationToken) =>
        auditWriter?.WriteAsync(new AuditWriteRequest(
            "Movement.ReturnArtifacts",
            "StorehouseOperations",
            nameof(MovementRecord),
            movementGroupId.ToString(),
            $"Returned {artifactCount} artifact(s) to storage.",
            $"ReturnLocationId={returnLocationId}; ArtifactCount={artifactCount}"), cancellationToken) ?? Task.CompletedTask;
}

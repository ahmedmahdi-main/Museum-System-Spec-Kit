using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class RecordReconciliationItemsUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ReconciliationSessionDto>> RecordReconciliationItems(RecordReconciliationItemsRequest request, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ReconciliationSessions
            .IncludeDetails()
            .FirstOrDefaultAsync(s => s.ReconciliationSessionId == request.ReconciliationSessionId, cancellationToken);
        if (session is null)
        {
            return UseCaseResult<ReconciliationSessionDto>.Failure(new ValidationIssue("Reconciliation.NotFound", "جلسة الجرد غير موجودة."));
        }

        if (session.Status == ReconciliationSessionStatus.Reviewed)
        {
            return UseCaseResult<ReconciliationSessionDto>.Failure(new ValidationIssue("Reconciliation.Reviewed", "لا يمكن تعديل جلسة مراجعة."));
        }

        var artifacts = await dbContext.Artifacts.AsNoTracking().ToListAsync(cancellationToken);
        var results = ReconciliationRules.Classify(session.ReconciliationSessionId, session.LocationId, artifacts, request.ObservedMuseumNumbers);
        session.ReplaceResults(results);
        dbContext.ReconciliationResults.AddRange(results);
        await dbContext.SaveChangesAsync(cancellationToken);
        return UseCaseResult<ReconciliationSessionDto>.Success(ReconciliationDtoMapper.ToDto(session), "تم تسجيل نتائج الجرد.");
    }
}

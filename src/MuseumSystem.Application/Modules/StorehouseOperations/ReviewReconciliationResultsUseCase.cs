using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class ReviewReconciliationResultsUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ReconciliationSessionDto>> ReviewReconciliationResults(Guid reconciliationSessionId, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ReconciliationSessions
            .IncludeDetails()
            .FirstOrDefaultAsync(s => s.ReconciliationSessionId == reconciliationSessionId, cancellationToken);
        if (session is null)
        {
            return UseCaseResult<ReconciliationSessionDto>.Failure(new ValidationIssue("Reconciliation.NotFound", "جلسة الجرد غير موجودة."));
        }

        foreach (var conflict in session.Results.Where(result => result.ResultType == ReconciliationResultType.Conflict))
        {
            conflict.ConfirmConflict();
        }

        session.MarkReviewed();
        await dbContext.SaveChangesAsync(cancellationToken);
        return UseCaseResult<ReconciliationSessionDto>.Success(ReconciliationDtoMapper.ToDto(session), "تمت مراجعة نتائج الجرد.");
    }
}

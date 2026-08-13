using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Import.Contracts;
using MuseumSystem.Domain.Modules.Import;

namespace MuseumSystem.Application.Modules.Import;

public sealed class CancelImportBatchUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ImportBatchDto>> CancelImportBatch(Guid importBatchId, CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.ImportBatchId == importBatchId, cancellationToken);
        if (batch is null)
        {
            return UseCaseResult<ImportBatchDto>.Failure(new ValidationIssue("ImportBatch.NotFound", "دفعة الاستيراد غير موجودة."));
        }

        if (!ImportBatchRules.CanCancel(batch))
        {
            return UseCaseResult<ImportBatchDto>.Failure(new ValidationIssue("ImportBatch.Final", "لا يمكن إلغاء دفعة نهائية."));
        }

        batch.Cancel();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<ImportBatchDto>.Conflict("تعذر الإلغاء لأن الدفعة تغيرت. أعد المحاولة.");
        }

        return UseCaseResult<ImportBatchDto>.Success(ImportDtoMapper.ToDto(batch), "تم إلغاء الدفعة.");
    }
}

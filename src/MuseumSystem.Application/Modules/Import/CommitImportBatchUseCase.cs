using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Import.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.Import;

public sealed class CommitImportBatchUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ImportCommitDto>> CommitImportBatch(Guid importBatchId, CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.ImportBatchId == importBatchId, cancellationToken);
        if (batch is null)
        {
            return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportBatch.NotFound", "دفعة الاستيراد غير موجودة."));
        }

        if (batch.Status == ImportBatchStatus.Committed)
        {
            return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportBatch.AlreadyCommitted", "تم اعتماد هذه الدفعة سابقاً."));
        }

        if (!ImportBatchRules.CanCommit(batch))
        {
            return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportBatch.NotReady", "اعتمد الدفعة بعد التحقق فقط."));
        }

        var acceptedRows = batch.Rows.Where(row => row.Status == ImportRowStatus.Accepted).OrderBy(row => row.RowNumber).ToList();
        if (acceptedRows.Count == 0)
        {
            return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportBatch.NoAcceptedRows", "لا توجد صفوف مقبولة للاعتماد."));
        }

        var categoryIds = acceptedRows.Select(row => row.ProposedCategoryId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var locationIds = acceptedRows.Select(row => row.ProposedLocationId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var categories = await dbContext.ArtifactCategories.Where(c => categoryIds.Contains(c.CategoryId)).ToDictionaryAsync(c => c.CategoryId, cancellationToken);
        var locations = await dbContext.Locations.Where(l => locationIds.Contains(l.LocationId)).ToDictionaryAsync(l => l.LocationId, cancellationToken);
        var seenKeys = new HashSet<string>();

        foreach (var row in acceptedRows)
        {
            if (row.ProposedCategoryId is null || row.ProposedLocationId is null || !int.TryParse(row.ItemNumberValue, out var itemNumber) || itemNumber <= 0)
            {
                return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportRow.Invalid", $"الصف {row.RowNumber} غير صالح للاعتماد."));
            }

            if (!categories.TryGetValue(row.ProposedCategoryId.Value, out var category) || !category.IsActive)
            {
                return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportRow.CategoryInvalid", $"الفئة في الصف {row.RowNumber} غير صالحة."));
            }

            if (!locations.TryGetValue(row.ProposedLocationId.Value, out var location) || !location.IsActive || location.LocationType != LocationType.Storage)
            {
                return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("ImportRow.LocationInvalid", $"موقع الخزن في الصف {row.RowNumber} غير صالح."));
            }

            var key = $"{category.CategoryId}:{itemNumber}";
            if (!seenKeys.Add(key) || await dbContext.Artifacts.AnyAsync(a => a.CategoryId == category.CategoryId && a.ItemNumber == itemNumber, cancellationToken))
            {
                return UseCaseResult<ImportCommitDto>.Failure(new ValidationIssue("MuseumNumber.Duplicate", $"رقم القطعة في الصف {row.RowNumber} مكرر داخل الفئة."));
            }
        }

        foreach (var row in acceptedRows)
        {
            var category = categories[row.ProposedCategoryId!.Value];
            var location = locations[row.ProposedLocationId!.Value];
            var itemNumber = int.Parse(row.ItemNumberValue!);
            var artifact = ArtifactFactory.Create(category, itemNumber, row.DescriptionValue!, location);
            artifact.MarkCreatedFromImportBatch(batch.ImportBatchId);
            row.MarkCommittedArtifact(artifact.ArtifactId);
            dbContext.Artifacts.Add(artifact);
        }

        batch.MarkCommitted();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<ImportCommitDto>.Conflict("تعذر الاعتماد لأن الدفعة تغيرت. أعد المحاولة.");
        }

        return UseCaseResult<ImportCommitDto>.Success(new ImportCommitDto(batch.ImportBatchId, acceptedRows.Count, "تم اعتماد الصفوف المقبولة."));
    }
}

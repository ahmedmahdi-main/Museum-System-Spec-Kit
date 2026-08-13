using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Import.Contracts;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.Import;

public sealed class ValidateImportBatchUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ImportBatchDto>> ValidateImportBatch(Guid importBatchId, CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.ImportBatchId == importBatchId, cancellationToken);
        if (batch is null)
        {
            return UseCaseResult<ImportBatchDto>.Failure(new ValidationIssue("ImportBatch.NotFound", "دفعة الاستيراد غير موجودة."));
        }

        if (!ImportBatchRules.CanValidate(batch))
        {
            return UseCaseResult<ImportBatchDto>.Failure(new ValidationIssue("ImportBatch.Final", "لا يمكن التحقق من دفعة نهائية."));
        }

        var rows = batch.Rows.OrderBy(row => row.RowNumber).ToList();
        var categoryCodes = rows.Select(row => NormalizeCode(row.CategoryValue)).Where(value => value is not null).Distinct().ToList();
        var locationNames = rows.Select(row => NormalizeText(row.LocationValue)).Where(value => value is not null).Distinct().ToList();
        var categories = await dbContext.ArtifactCategories.Where(c => categoryCodes.Contains(c.CategoryCode)).ToListAsync(cancellationToken);
        var locations = await dbContext.Locations.Where(l => locationNames.Contains(l.NameArabic)).ToListAsync(cancellationToken);
        var rowKeys = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            ValidateRow(row, categories, locations, rowKeys);
        }

        batch.MarkValidated();
        await dbContext.SaveChangesAsync(cancellationToken);
        return UseCaseResult<ImportBatchDto>.Success(ImportDtoMapper.ToDto(batch), batch.Status == ImportBatchStatus.ReadyToCommit ? "الدفعة جاهزة للاعتماد." : "توجد صفوف تحتاج معالجة.");
    }

    private void ValidateRow(ImportRow row, IReadOnlyList<Domain.Modules.ArtifactRegistry.ArtifactCategory> categories, IReadOnlyList<Location> locations, Dictionary<string, int> rowKeys)
    {
        var issues = new List<string>();
        var reviewIssues = new List<string>();
        var categoryCode = NormalizeCode(row.CategoryValue);
        var itemNumberText = NormalizeText(row.ItemNumberValue);
        var locationName = NormalizeText(row.LocationValue);

        if (categoryCode is null) issues.Add("رقم الفئة مطلوب.");
        if (itemNumberText is null) issues.Add("رقم القطعة مطلوب.");
        if (locationName is null) issues.Add("موقع الخزن مطلوب.");
        if (string.IsNullOrWhiteSpace(row.DescriptionValue)) issues.Add("الوصف مطلوب.");

        var itemNumberValid = int.TryParse(itemNumberText, out var itemNumber) && itemNumber > 0;
        if (itemNumberText is not null && !itemNumberValid) issues.Add("رقم القطعة غير صالح.");

        var category = categoryCode is null ? null : categories.FirstOrDefault(c => c.CategoryCode == categoryCode);
        if (categoryCode is not null && category is null) issues.Add("الفئة غير معروفة.");
        if (category is not null && !category.IsActive) reviewIssues.Add("الفئة غير مفعلة.");

        var location = locationName is null ? null : locations.FirstOrDefault(l => l.NameArabic == locationName && l.LocationType == LocationType.Storage);
        if (locationName is not null && location is null) issues.Add("موقع الخزن غير معروف.");
        if (location is not null && !location.IsActive) reviewIssues.Add("موقع الخزن غير مفعل.");

        if (category is not null && itemNumberValid)
        {
            var key = $"{category.CategoryId}:{itemNumber}";
            if (rowKeys.TryGetValue(key, out var firstRowNumber))
            {
                issues.Add($"رقم القطعة مكرر داخل الملف مع الصف {firstRowNumber}.");
            }
            else
            {
                rowKeys[key] = row.RowNumber;
            }

            if (dbContext.Artifacts.Any(a => a.CategoryId == category.CategoryId && a.ItemNumber == itemNumber))
            {
                issues.Add("رقم القطعة مكرر داخل الفئة.");
            }
        }

        if (issues.Count > 0)
        {
            row.Reject(issues);
        }
        else if (reviewIssues.Count > 0)
        {
            row.NeedsReview(reviewIssues, category?.CategoryId, location?.LocationId);
        }
        else
        {
            row.Accept(category!.CategoryId, location!.LocationId);
        }
    }

    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

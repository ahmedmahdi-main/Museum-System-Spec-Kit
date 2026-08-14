using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Application.Modules.ArtifactRegistry;

public sealed class CategoryUseCases(IMuseumDbContext dbContext, IAuditWriter? auditWriter = null)
{
    public async Task<UseCaseResult<CategoryDto>> CreateCategory(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var code = ArtifactCategory.NormalizeCategoryCode(request.CategoryCode);
        if (await dbContext.ArtifactCategories.AnyAsync(c => c.CategoryCode == code, cancellationToken))
        {
            return UseCaseResult<CategoryDto>.Failure(new ValidationIssue("CategoryCode.Duplicate", "رقم الفئة مستخدم مسبقاً.", nameof(request.CategoryCode)));
        }

        var category = ArtifactCategory.Create(code, request.NameArabic, request.Description);
        dbContext.ArtifactCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("ArtifactCategory.Create", category.CategoryId, $"Created artifact category {category.CategoryCode}.", null, cancellationToken);
        return UseCaseResult<CategoryDto>.Success(ToDto(category));
    }

    public async Task<UseCaseResult<CategoryDto>> UpdateCategory(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ArtifactCategories.FindAsync([request.CategoryId], cancellationToken);
        if (category is null)
        {
            return UseCaseResult<CategoryDto>.Failure(new ValidationIssue("Category.NotFound", "الفئة غير موجودة.", nameof(request.CategoryId)));
        }

        var code = ArtifactCategory.NormalizeCategoryCode(request.CategoryCode);
        var duplicate = await dbContext.ArtifactCategories.AnyAsync(c => c.CategoryId != request.CategoryId && c.CategoryCode == code, cancellationToken);
        if (duplicate)
        {
            return UseCaseResult<CategoryDto>.Failure(new ValidationIssue("CategoryCode.Duplicate", "رقم الفئة مستخدم مسبقاً.", nameof(request.CategoryCode)));
        }

        var codeChanged = !string.Equals(category.CategoryCode, code, StringComparison.Ordinal);
        if (codeChanged && await dbContext.Artifacts.AnyAsync(a => a.CategoryId == category.CategoryId, cancellationToken))
        {
            return UseCaseResult<CategoryDto>.Failure(new ValidationIssue("CategoryCode.InUse", "لا يمكن تغيير رقم فئة لها قطع مسجلة.", nameof(request.CategoryCode)));
        }

        category.Update(code, request.NameArabic, request.Description);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("ArtifactCategory.Update", category.CategoryId, $"Updated artifact category {category.CategoryCode}.", "Category metadata updated.", cancellationToken);
        return UseCaseResult<CategoryDto>.Success(ToDto(category));
    }

    public async Task<UseCaseResult> DisableCategoryForNewUse(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ArtifactCategories.FindAsync([categoryId], cancellationToken);
        if (category is null)
        {
            return UseCaseResult.Failure(new ValidationIssue("Category.NotFound", "الفئة غير موجودة.", nameof(categoryId)));
        }

        category.DisableForNewUse();
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("ArtifactCategory.DisableForNewUse", category.CategoryId, $"Disabled artifact category {category.CategoryCode} for new use.", null, cancellationToken);
        return UseCaseResult.Success("تم تعطيل الفئة للاستخدام الجديد.");
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategories(CancellationToken cancellationToken = default) =>
        await dbContext.ArtifactCategories
            .OrderBy(c => c.CategoryCode)
            .Select(c => new CategoryDto(c.CategoryId, c.CategoryCode, c.NameArabic, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);

    private static CategoryDto ToDto(ArtifactCategory category) =>
        new(category.CategoryId, category.CategoryCode, category.NameArabic, category.Description, category.IsActive);

    private Task WriteAuditAsync(string actionName, Guid categoryId, string summary, string? changeSummary, CancellationToken cancellationToken) =>
        auditWriter?.WriteAsync(new AuditWriteRequest(
            actionName,
            "ArtifactRegistry",
            nameof(ArtifactCategory),
            categoryId.ToString(),
            summary,
            changeSummary), cancellationToken) ?? Task.CompletedTask;
}

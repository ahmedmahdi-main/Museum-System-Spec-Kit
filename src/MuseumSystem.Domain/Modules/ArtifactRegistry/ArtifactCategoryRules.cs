namespace MuseumSystem.Domain.Modules.ArtifactRegistry;

public static class ArtifactCategoryRules
{
    public static bool IsCategoryCodeUnique(IEnumerable<ArtifactCategory> categories, string categoryCode, Guid? excludingCategoryId = null)
    {
        var normalizedCode = ArtifactCategory.NormalizeCategoryCode(categoryCode);
        return categories.All(category =>
            category.CategoryId == excludingCategoryId ||
            !string.Equals(category.CategoryCode, normalizedCode, StringComparison.OrdinalIgnoreCase));
    }
}

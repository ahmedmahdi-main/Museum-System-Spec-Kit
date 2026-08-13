namespace MuseumSystem.Domain.Modules.ArtifactRegistry;

public sealed class ArtifactCategory
{
    private ArtifactCategory()
    {
    }

    private ArtifactCategory(Guid categoryId, string categoryCode, string nameArabic, string? description)
    {
        CategoryId = categoryId;
        CategoryCode = NormalizeCategoryCode(categoryCode);
        NameArabic = RequireText(nameArabic, nameof(nameArabic));
        Description = NormalizeOptional(description);
        IsActive = true;
    }

    public Guid CategoryId { get; private set; }
    public string CategoryCode { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public static ArtifactCategory Create(string categoryCode, string nameArabic, string? description = null) =>
        new(Guid.NewGuid(), categoryCode, nameArabic, description);

    public void Update(string categoryCode, string nameArabic, string? description = null)
    {
        CategoryCode = NormalizeCategoryCode(categoryCode);
        NameArabic = RequireText(nameArabic, nameof(nameArabic));
        Description = NormalizeOptional(description);
        Touch();
    }

    public void DisableForNewUse()
    {
        IsActive = false;
        Touch();
    }

    public static string NormalizeCategoryCode(string categoryCode) =>
        RequireText(categoryCode, nameof(categoryCode)).ToUpperInvariant();

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch() => LastModifiedAt = DateTimeOffset.UtcNow;
}

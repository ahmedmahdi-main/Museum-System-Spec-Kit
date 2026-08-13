using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Tests.ArtifactRegistry;

public sealed class ArtifactCategoryTests
{
    [Fact]
    public void Category_code_is_required_and_normalized()
    {
        Assert.Throws<ArgumentException>(() => ArtifactCategory.Create(" ", "فخار"));

        var category = ArtifactCategory.Create(" pot ", "فخار");

        Assert.Equal("POT", category.CategoryCode);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Category_code_uniqueness_rule_excludes_current_category_when_updating()
    {
        var first = ArtifactCategory.Create("A", "الفئة أ");
        var second = ArtifactCategory.Create("B", "الفئة ب");

        Assert.False(ArtifactCategoryRules.IsCategoryCodeUnique([first, second], "a"));
        Assert.True(ArtifactCategoryRules.IsCategoryCodeUnique([first, second], "a", first.CategoryId));
    }

    [Fact]
    public void Category_can_be_disabled_for_new_use_without_deleting_it()
    {
        var category = ArtifactCategory.Create("A", "الفئة أ");

        category.DisableForNewUse();

        Assert.False(category.IsActive);
        Assert.NotNull(category.LastModifiedAt);
    }
}

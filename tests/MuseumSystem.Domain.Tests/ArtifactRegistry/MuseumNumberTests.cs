using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Tests.ArtifactRegistry;

public sealed class MuseumNumberTests
{
    [Fact]
    public void Museum_number_uses_category_code_and_item_number_only()
    {
        var categoryId = Guid.NewGuid();
        var museumNumber = MuseumNumber.Create("CAT", 42);

        Assert.Equal("CAT", museumNumber.CategoryCode);
        Assert.Equal(42, museumNumber.ItemNumber);
        Assert.Equal("CAT/42", museumNumber.DisplayValue);
        Assert.DoesNotContain(categoryId.ToString(), museumNumber.DisplayValue);
    }

    [Fact]
    public void Museum_number_rejects_invalid_item_number()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MuseumNumber.Create("CAT", 0));
    }
}

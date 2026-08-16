namespace MuseumSystem.Web.AcceptanceTests.ArtifactRegistry;

public sealed class ArtifactRegistryFlowTests
{
    [Theory]
    [InlineData("Components", "Pages", "Artifacts", "Categories.razor")]
    [InlineData("Components", "Pages", "Artifacts", "Create.razor")]
    [InlineData("Components", "Pages", "Artifacts", "Search.razor")]
    [InlineData("Components", "Pages", "Artifacts", "Details.razor")]
    [InlineData("Components", "Pages", "Storehouse", "Locations.razor")]
    public void Phase_two_pages_exist(params string[] segments)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine([root.FullName, "src", "MuseumSystem.Web", .. segments]);

        Assert.True(File.Exists(path), $"Expected page at {path}");
    }

    [Fact]
    public void Artifact_create_uses_staff_labels_and_normal_required_validation()
    {
        var root = FindRepositoryRoot();
        var createPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Create.razor"));

        Assert.Contains("رقم المتحف", createPage);
        Assert.Contains("رمز الفئة", createPage);
        Assert.Contains("رقم التسلسل داخل الفئة", createPage);
        Assert.Contains("الوصف الأساسي", createPage);
        Assert.Contains("required", createPage);
        Assert.DoesNotContain("MuseumNumber يُنشأ", createPage);
        Assert.DoesNotContain("CategoryCode + ItemNumber", createPage);
        Assert.DoesNotContain("<span>ItemNumber</span>", createPage);
    }

    [Fact]
    public void Artifact_search_has_empty_states_and_staff_facing_headings()
    {
        var root = FindRepositoryRoot();
        var searchPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Search.razor"));

        Assert.Contains("اكتب رقم المتحف أو جزءاً من الوصف", searchPage);
        Assert.Contains("لا توجد قطع تطابق هذه العبارة", searchPage);
        Assert.Contains("<th>رقم المتحف</th>", searchPage);
        Assert.Contains("<th>رمز الفئة</th>", searchPage);
        Assert.Contains("<th>الإجراءات</th>", searchPage);
        Assert.DoesNotContain("<th>MuseumNumber</th>", searchPage);
        Assert.DoesNotContain("CategoryCode أو ItemNumber", searchPage);
        Assert.DoesNotContain("placeholder=\"MuseumNumber", searchPage);
    }

    [Fact]
    public void Categories_page_uses_staff_facing_category_code_label()
    {
        var root = FindRepositoryRoot();
        var categories = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Categories.razor"));

        Assert.Contains("رمز الفئة", categories);
        Assert.Contains("<th>الإجراءات</th>", categories);
        Assert.DoesNotContain("<th>CategoryCode</th>", categories);
        Assert.DoesNotContain("CategoryCode هو", categories);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
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
    public void Artifact_pages_keep_arabic_rtl_staff_copy()
    {
        var root = FindRepositoryRoot();
        var createPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Create.razor"));
        var searchPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Search.razor"));

        Assert.Contains("إنشاء قطعة", createPage);
        Assert.Contains("البحث عن القطع", searchPage);
        Assert.Contains("MuseumNumber", searchPage);
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

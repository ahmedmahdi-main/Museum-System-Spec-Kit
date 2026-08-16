namespace MuseumSystem.Web.AcceptanceTests.Usability;

public sealed class RtlPrimaryScreensTests
{
    [Theory]
    [InlineData("Artifacts", "Search.razor")]
    [InlineData("Artifacts", "Create.razor")]
    [InlineData("Storehouse", "Delivery.razor")]
    [InlineData("Storehouse", "Return.razor")]
    [InlineData("Imports", "ExcelImport.razor")]
    [InlineData("Storehouse", "Reconciliation.razor")]
    public void Primary_staff_screens_do_not_expose_developer_errors(string folder, string fileName)
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", folder, fileName));

        Assert.DoesNotContain("stack trace", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Operational_pages_do_not_expose_foundation_or_english_placeholder_headings()
    {
        var root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Home.razor"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation", "Index.razor"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation", "Templates.razor"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor")
        ];

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Documentation Workspace", text);
            Assert.DoesNotContain("Documentation Templates", text);
            Assert.DoesNotContain("Foundation", text);
            Assert.DoesNotContain("المنصة التجريبية", text);
        }
    }

    [Fact]
    public void Home_is_operational_and_permission_aware()
    {
        var root = FindRepositoryRoot();
        var home = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Home.razor"));

        Assert.Contains("<AuthorizeView Policy=", home);
        Assert.Contains("href=\"artifacts\"", home);
        Assert.Contains("href=\"documentation\"", home);
        Assert.Contains("href=\"documentation/templates\"", home);
        Assert.DoesNotContain("fake", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chart", home, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Layout_and_document_shell_are_rtl()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "App.razor"));
        var layout = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("dir=\"rtl\"", app);
        Assert.Contains("lang=\"ar\"", app);
        Assert.Contains("app-shell", layout);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

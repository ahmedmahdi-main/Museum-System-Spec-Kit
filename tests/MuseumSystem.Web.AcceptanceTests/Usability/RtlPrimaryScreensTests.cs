namespace MuseumSystem.Web.AcceptanceTests.Usability;

public sealed class RtlPrimaryScreensTests
{
    [Theory]
    [InlineData("Artifacts", "Search.razor", "ابحث")]
    [InlineData("Artifacts", "Create.razor", "MuseumNumber")]
    [InlineData("Storehouse", "Delivery.razor", "تسليم")]
    [InlineData("Storehouse", "Return.razor", "استلام")]
    [InlineData("Imports", "ExcelImport.razor", "استيراد")]
    [InlineData("Storehouse", "Reconciliation.razor", "الجرد")]
    public void Primary_staff_screens_keep_arabic_operational_copy(string folder, string fileName, string expectedCopy)
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", folder, fileName));

        Assert.Contains(expectedCopy, page);
        Assert.DoesNotContain("stack trace", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", page, StringComparison.OrdinalIgnoreCase);
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

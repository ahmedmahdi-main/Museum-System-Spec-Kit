namespace MuseumSystem.Web.AcceptanceTests.Foundation;

public sealed class RtlShellTests
{
    [Fact]
    public void App_shell_declares_arabic_rtl_document()
    {
        var root = FindRepositoryRoot();
        var appPath = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "App.razor");
        var app = File.ReadAllText(appPath);

        Assert.Contains("lang=\"ar\"", app);
        Assert.Contains("dir=\"rtl\"", app);
        Assert.Contains("bootstrap.rtl.min.css", app);
    }

    [Fact]
    public void Main_layout_contains_authorization_status()
    {
        var root = FindRepositoryRoot();
        var layoutPath = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor");
        var layout = File.ReadAllText(layoutPath);

        Assert.Contains("<AuthorizeView>", layout);
        Assert.Contains("auth-chip", layout);
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

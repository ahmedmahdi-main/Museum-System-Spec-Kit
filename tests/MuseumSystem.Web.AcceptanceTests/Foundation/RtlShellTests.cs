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
    public void Main_layout_uses_nav_menu_as_authoritative_navigation()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("<NavMenu />", layout);
        Assert.Contains("<AuthorizeView", layout);
        Assert.Contains("ApplicationUserClaimsPrincipalFactory.DisplayNameClaimType", layout);
        Assert.Contains("/Account/Logout", layout);
        Assert.DoesNotContain("nav-list", layout);
        Assert.DoesNotContain("is-disabled", layout);
        Assert.DoesNotContain("Documentation", layout);
        Assert.DoesNotContain("Counter", layout);
        Assert.DoesNotContain("Weather", layout);
    }

    [Fact]
    public void Display_name_claim_is_preferred_with_username_fallback()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor"));
        var factory = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Infrastructure", "Identity", "ApplicationUserClaimsPrincipalFactory.cs"));

        Assert.Contains("ApplicationUserClaimsPrincipalFactory.DisplayNameClaimType", layout);
        Assert.Contains("user.Identity?.Name", layout);
        Assert.Contains("user.DisplayName", factory);
        Assert.Contains("identity.AddClaim", factory);
        Assert.DoesNotContain("مدير النظام", layout);
    }

    [Fact]
    public void Development_startup_migrates_before_admin_seed()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Program.cs"));
        var initializer = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Infrastructure", "DevelopmentDatabaseInitializer.cs"));

        var migrateIndex = program.IndexOf("EnsureDevelopmentDatabaseMigratedAsync", StringComparison.Ordinal);
        var seedIndex = program.IndexOf("SeedDevelopmentAdminAsync", StringComparison.Ordinal);

        Assert.True(migrateIndex >= 0, "Development migration initializer should be called.");
        Assert.True(seedIndex > migrateIndex, "Development admin seed should run after migrations.");
        Assert.Contains("if (!environment.IsDevelopment())", initializer);
        Assert.Contains("Database.MigrateAsync", initializer);
    }

    [Fact]
    public void Runtime_navigation_contains_permission_aware_existing_routes()
    {
        var root = FindRepositoryRoot();
        var nav = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("href=\"artifacts\"", nav);
        Assert.Contains("href=\"storehouse/delivery\"", nav);
        Assert.Contains("href=\"storehouse/return\"", nav);
        Assert.Contains("href=\"storehouse/locations\"", nav);
        Assert.Contains("href=\"storehouse/reconciliation\"", nav);
        Assert.Contains("href=\"imports/excel\"", nav);
        Assert.Contains("href=\"documentation\"", nav);
        Assert.Contains("href=\"documentation/templates\"", nav);
        Assert.Contains("href=\"admin/audit\"", nav);
        Assert.Contains("PermissionNames.DocumentationView", nav);
        Assert.Contains("PermissionNames.DocumentationTemplatesView", nav);
        Assert.DoesNotContain("counter", nav, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weather", nav, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Visible_product_name_is_unified()
    {
        var root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Home.razor"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Pages", "Account", "Login.cshtml"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Pages", "Account", "Logout.cshtml"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Pages", "Account", "AccessDenied.cshtml")
        ];

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.Contains("نظام إدارة المتحف العراقي", text);
            Assert.DoesNotContain("نظام إدارة المتحف</h1>", text);
            Assert.DoesNotContain("Museum-System", text);
        }
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

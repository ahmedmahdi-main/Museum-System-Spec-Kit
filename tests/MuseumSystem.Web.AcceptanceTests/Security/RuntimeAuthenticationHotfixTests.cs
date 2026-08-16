namespace MuseumSystem.Web.AcceptanceTests.Security;

public sealed class RuntimeAuthenticationHotfixTests
{
    [Fact]
    public void Login_route_exists_and_is_anonymous_with_safe_return_url_handling()
    {
        var root = FindRepositoryRoot();
        var loginPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Pages", "Account", "Login.cshtml"));
        var loginModel = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Pages", "Account", "Login.cshtml.cs"));

        Assert.Contains("@page \"/Account/Login\"", loginPage);
        Assert.Contains("method=\"post\"", loginPage);
        Assert.Contains("[AllowAnonymous]", loginModel);
        Assert.Contains("SignInManager<ApplicationUser>", loginModel);
        Assert.Contains("PasswordSignInAsync", loginModel);
        Assert.Contains("Url.IsLocalUrl(returnUrl)", loginModel);
        Assert.Contains("LocalRedirect(ReturnUrl)", loginModel);
    }

    [Fact]
    public void Protected_home_remains_authorized_but_not_found_and_error_are_anonymous()
    {
        var root = FindRepositoryRoot();

        Assert.Contains("@attribute [Authorize]", Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Home.razor"));
        Assert.Contains("@attribute [AllowAnonymous]", Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "NotFound.razor"));
        Assert.Contains("@attribute [AllowAnonymous]", Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Error.razor"));
    }

    [Fact]
    public void Cookie_paths_have_real_non_recursive_endpoints()
    {
        var root = FindRepositoryRoot();
        var infrastructure = Read(root, "src", "MuseumSystem.Infrastructure", "DependencyInjection.cs");
        var program = Read(root, "src", "MuseumSystem.Web", "Program.cs");

        Assert.Contains("options.LoginPath = \"/Account/Login\"", infrastructure);
        Assert.Contains("options.AccessDeniedPath = \"/Account/AccessDenied\"", infrastructure);
        Assert.Contains("UseStatusCodePagesWithReExecute(\"/not-found\"", program);
        Assert.Contains("MapRazorPages()", program);
        Assert.Contains("@page \"/Account/AccessDenied\"", Read(root, "src", "MuseumSystem.Web", "Pages", "Account", "AccessDenied.cshtml"));
    }

    [Fact]
    public void Logout_uses_post_and_existing_identity_sign_out()
    {
        var root = FindRepositoryRoot();
        var logoutPage = Read(root, "src", "MuseumSystem.Web", "Pages", "Account", "Logout.cshtml");
        var logoutModel = Read(root, "src", "MuseumSystem.Web", "Pages", "Account", "Logout.cshtml.cs");
        var layout = Read(root, "src", "MuseumSystem.Web", "Components", "Layout", "MainLayout.razor");

        Assert.Contains("method=\"post\"", logoutPage);
        Assert.Contains("[Authorize]", logoutModel);
        Assert.Contains("SignInManager<ApplicationUser>", logoutModel);
        Assert.Contains("SignOutAsync", logoutModel);
        Assert.Contains("/Account/Logout", layout);
        Assert.Contains("<AntiforgeryToken />", layout);
    }

    private static string Read(DirectoryInfo root, params string[] parts)
    {
        var allParts = new string[parts.Length + 1];
        allParts[0] = root.FullName;
        Array.Copy(parts, 0, allParts, 1, parts.Length);
        return File.ReadAllText(Path.Combine(allParts));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

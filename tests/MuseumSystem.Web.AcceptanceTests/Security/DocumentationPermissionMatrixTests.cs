using System.Text.RegularExpressions;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Security;

public sealed partial class DocumentationPermissionMatrixTests
{
    public static TheoryData<string, string, string> DocumentationPages => new()
    {
        { "Templates.razor", "@page \"/documentation/templates\"", PermissionNames.DocumentationTemplatesView },
        { "TemplateVersionEditor.razor", "@page \"/documentation/templates/{VersionId:guid}/edit\"", PermissionNames.DocumentationTemplatesManage },
        { "TemplateVersionDetails.razor", "@page \"/documentation/templates/{VersionId:guid}\"", PermissionNames.DocumentationTemplatesView },
        { "Index.razor", "@page \"/documentation\"", PermissionNames.DocumentationView },
        { "EditRecord.razor", "@page \"/documentation/records/{RecordId:guid}/edit\"", PermissionNames.DocumentationView },
        { "CorrectRecord.razor", "@page \"/documentation/records/{RecordId:guid}/correct\"", PermissionNames.DocumentationView },
        { "History.razor", "@page \"/documentation/records/{RecordId:guid}/history\"", PermissionNames.DocumentationHistoryView },
        { "RevisionDetails.razor", "@page \"/documentation/records/{RecordId:guid}/history/{RevisionNumber:int}\"", PermissionNames.DocumentationHistoryView }
    };

    [Theory]
    [MemberData(nameof(DocumentationPages))]
    public void Routable_documentation_pages_declare_explicit_registered_permission_policies(
        string fileName,
        string route,
        string expectedPolicy)
    {
        var text = File.ReadAllText(Path.Combine(DocumentationPagesRoot().FullName, fileName));
        var expectedFieldName = FieldNameFor(expectedPolicy);

        Assert.Contains(route, text);
        Assert.Contains($"@attribute [Authorize(Policy = PermissionNames.{expectedFieldName})]", text);
        Assert.DoesNotContain("@attribute [Authorize]", text);
        Assert.DoesNotContain("@attribute [Authorize()]", text);
        Assert.All(ExtractPermissionConstantPolicyNames(text), policy => Assert.Contains(policy, PermissionNames.All));
    }

    [Fact]
    public void Documentation_navigation_uses_view_policies_for_workspace_and_template_entries()
    {
        var nav = File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.DocumentationView)}\">", nav);
        Assert.Contains("href=\"documentation\"", nav);
        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.DocumentationTemplatesView)}\">", nav);
        Assert.Contains("href=\"documentation/templates\"", nav);
        Assert.DoesNotContain($"href=\"documentation/templates\" Match=\"NavLinkMatch.All\">قوالب التوثيق</NavLink>\n            </Authorized>\n        </AuthorizeView>\n        <AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.DocumentationTemplatesManage)}\">", nav);
    }

    [Fact]
    public void Routes_use_authorize_route_view_to_enforce_page_attributes()
    {
        var routes = File.ReadAllText(Path.Combine(RepositoryRoot().FullName, "src", "MuseumSystem.Web", "Components", "Routes.razor"));

        Assert.Contains("<AuthorizeRouteView RouteData=\"routeData\" DefaultLayout=\"typeof(Layout.MainLayout)\">", routes);
        Assert.DoesNotContain("<RouteView", routes);
    }

    private static IEnumerable<string> ExtractPermissionConstantPolicyNames(string text)
    {
        foreach (Match match in PermissionPolicyRegex().Matches(text))
        {
            var fieldName = match.Groups["field"].Value;
            var field = typeof(PermissionNames).GetFields().SingleOrDefault(field => field.Name == fieldName);
            Assert.NotNull(field);
            yield return (string)field!.GetValue(null)!;
        }
    }

    private static string FieldNameFor(string permission) =>
        typeof(PermissionNames).GetFields().Single(field => (string)field.GetValue(null)! == permission).Name;

    private static DirectoryInfo DocumentationPagesRoot() =>
        new(Path.Combine(RepositoryRoot().FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation"));

    private static DirectoryInfo RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    [GeneratedRegex("""Policy\s*=\s*PermissionNames\.(?<field>[A-Za-z0-9_]+)""")]
    private static partial Regex PermissionPolicyRegex();
}

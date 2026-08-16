using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationTemplateAdminFlowTests
{
    [Fact]
    public void Template_administration_pages_are_routed_with_expected_policies()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");

        AssertPage(Path.Combine(pagesRoot, "Templates.razor"), "@page \"/documentation/templates\"", PermissionNames.DocumentationTemplatesView);
        AssertPage(Path.Combine(pagesRoot, "TemplateVersionEditor.razor"), "@page \"/documentation/templates/{VersionId:guid}/edit\"", PermissionNames.DocumentationTemplatesManage);
        AssertPage(Path.Combine(pagesRoot, "TemplateVersionDetails.razor"), "@page \"/documentation/templates/{VersionId:guid}\"", PermissionNames.DocumentationTemplatesView);
    }

    [Fact]
    public void Template_administration_sources_include_manage_actions_without_record_workspace()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var templates = File.ReadAllText(Path.Combine(pagesRoot, "Templates.razor"));
        var editor = File.ReadAllText(Path.Combine(pagesRoot, "TemplateVersionEditor.razor"));
        var nav = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("canManage = await IsAuthorizedAsync(PermissionNames.DocumentationTemplatesManage)", templates);
        Assert.True(CountOccurrences(templates, "if (!await IsAuthorizedAsync(PermissionNames.DocumentationTemplatesManage))") >= 4);
        Assert.Contains("ActivateTemplateVersion", templates);
        Assert.Contains("RetireTemplateVersion", templates);
        Assert.Contains("DocumentationFieldType", editor);
        Assert.Contains(nameof(PermissionNames.DocumentationTemplatesView), nav);
        Assert.Contains("href=\"documentation/templates\"", nav);

        Assert.True(File.Exists(Path.Combine(pagesRoot, "Index.razor")));
        Assert.True(File.Exists(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Documentation", "DynamicDocumentationForm.razor")));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static void AssertPage(string path, string route, string policy)
    {
        Assert.True(File.Exists(path), $"{path} should exist.");
        var text = File.ReadAllText(path);
        Assert.Contains(route, text);
        Assert.Contains($"Policy = PermissionNames.{FieldNameFor(policy)}", text);
    }

    private static string FieldNameFor(string permission) =>
        typeof(PermissionNames).GetFields().Single(field => (string)field.GetValue(null)! == permission).Name;

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

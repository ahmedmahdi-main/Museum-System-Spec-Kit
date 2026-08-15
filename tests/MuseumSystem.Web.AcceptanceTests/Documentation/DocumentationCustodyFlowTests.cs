using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationCustodyFlowTests
{
    [Fact]
    public void Workspace_and_edit_pages_surface_custody_template_authorization_and_stale_state_messages()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var index = File.ReadAllText(Path.Combine(pagesRoot, "Index.razor"));
        var edit = File.ReadAllText(Path.Combine(pagesRoot, "EditRecord.razor"));

        Assert.Contains("CreateBlockedReason", index);
        Assert.Contains("DraftEditBlockedReason", index);
        Assert.Contains("No Active", index + edit);
        Assert.Contains("not currently held by Documentation", index + edit);
        Assert.Contains("not authorized", index + edit);
        Assert.Contains("ConcurrencyConflict", index + edit);
    }

    [Fact]
    public void Management_handlers_use_existing_action_level_authorization_service()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var index = File.ReadAllText(Path.Combine(pagesRoot, "Index.razor"));
        var edit = File.ReadAllText(Path.Combine(pagesRoot, "EditRecord.razor"));

        Assert.Contains("@inject IAuthorizationService AuthorizationService", index);
        Assert.Contains("@inject IAuthorizationService AuthorizationService", edit);
        Assert.Contains("if (!await IsAuthorizedAsync(PermissionNames.DocumentationCreate))", index);
        Assert.Contains("if (!await IsAuthorizedAsync(PermissionNames.DocumentationEdit))", edit);
        Assert.Contains("if (!await IsAuthorizedAsync(PermissionNames.DocumentationEdit) || !await IsAuthorizedAsync(PermissionNames.DocumentationComplete))", edit);
        Assert.DoesNotContain("IAuthorizationContext", index + edit);
    }


    [Fact]
    public void Create_failure_message_is_preserved_after_workspace_refresh()
    {
        var root = FindRepositoryRoot();
        var index = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation", "Index.razor"));

        Assert.Contains("var failureMessage = FormatResult(result);", index);
        Assert.Contains("await RefreshWorkspaceAsync(clearMessage: false);", index);
        Assert.Contains("message = failureMessage;", index);
        Assert.Contains("private async Task RefreshWorkspaceAsync(bool clearMessage)", index);
    }
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

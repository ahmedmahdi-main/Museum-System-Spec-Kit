using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class CompleteDocumentationAuthorizationTests
{
    [Fact]
    public void Complete_action_availability_requires_both_edit_and_complete_permissions()
    {
        var editOnly = new DocumentationActionPermissionSet(CanEdit: true, CanComplete: false);
        var completeOnly = new DocumentationActionPermissionSet(CanEdit: false, CanComplete: true);
        var both = new DocumentationActionPermissionSet(CanEdit: true, CanComplete: true);

        Assert.False(CanComplete(editOnly));
        Assert.False(CanComplete(completeOnly));
        Assert.True(CanComplete(both));
    }

    [Fact]
    public void Edit_record_page_uses_existing_authorization_service_at_action_time_for_complete()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation", "EditRecord.razor"));

        Assert.Contains("@inject IAuthorizationService AuthorizationService", page);
        Assert.Contains("@attribute [Authorize(Policy = PermissionNames.DocumentationView)]", page);
        Assert.Contains("if (!await IsAuthorizedAsync(PermissionNames.DocumentationEdit) || !await IsAuthorizedAsync(PermissionNames.DocumentationComplete))", page);
        Assert.Contains("CompleteUseCase.CompleteDocumentationRecord", page);
        Assert.Contains(PermissionNames.DocumentationEdit, PermissionNames.All);
        Assert.Contains(PermissionNames.DocumentationComplete, PermissionNames.All);
    }

    private static bool CanComplete(DocumentationActionPermissionSet permissions) =>
        permissions.CanEdit && permissions.CanComplete;

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

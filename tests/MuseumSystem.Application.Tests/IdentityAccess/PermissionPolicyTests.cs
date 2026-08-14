using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Application.Tests.IdentityAccess;

public sealed class PermissionPolicyTests
{
    [Fact]
    public async Task Planned_permissions_are_registered_as_authorization_policies()
    {
        var options = new AuthorizationOptions().AddMuseumPolicies();
        var provider = new DefaultAuthorizationPolicyProvider(Options.Create(options));

        foreach (var permission in PermissionNames.All)
        {
            var policy = await provider.GetPolicyAsync(permission);
            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, requirement => requirement.GetType().Name.Contains("ClaimsAuthorizationRequirement"));
        }
    }

    [Fact]
    public void Permission_list_matches_phase_one_authorization_boundaries()
    {
        string[] expected =
        [
            "Artifacts.View",
            "Artifacts.Manage",
            "Storehouse.Locations.Manage",
            "Storehouse.Deliver",
            "Storehouse.Return",
            "Imports.Preview",
            "Imports.Commit",
            "Storehouse.Reconciliation.Manage",
            "Storehouse.Corrections.Create",
            "Audit.View",
            "Identity.Manage",
            "Documentation.View",
            "Documentation.Create",
            "Documentation.Edit",
            "Documentation.Complete",
            "Documentation.History.View",
            "Documentation.Templates.View",
            "Documentation.Templates.Manage"
        ];

        Assert.Equal(expected.Order(), PermissionNames.All.Order());
    }

    [Fact]
    public void Documentation_staff_role_has_approved_permissions_without_template_manage()
    {
        var permissions = MuseumRolePresets.PermissionsByRole[MuseumRoleNames.DocumentationStaff];

        Assert.Equal([
            PermissionNames.DocumentationView,
            PermissionNames.DocumentationCreate,
            PermissionNames.DocumentationEdit,
            PermissionNames.DocumentationComplete,
            PermissionNames.DocumentationHistoryView,
            PermissionNames.DocumentationTemplatesView
        ], permissions);
        Assert.DoesNotContain(PermissionNames.DocumentationTemplatesManage, permissions);
        Assert.Contains(PermissionNames.DocumentationTemplatesManage, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Admin]);
    }

    [Theory]
    [InlineData(MuseumRoleNames.Storekeeper)]
    [InlineData(MuseumRoleNames.Viewer)]
    [InlineData(MuseumRoleNames.RegistryManager)]
    [InlineData(MuseumRoleNames.InventoryOfficer)]
    public void Existing_default_roles_do_not_receive_documentation_permissions(string roleName)
    {
        var permissions = MuseumRolePresets.PermissionsByRole[roleName];

        Assert.DoesNotContain(permissions, permission => permission.StartsWith("Documentation.", StringComparison.Ordinal));
    }
}

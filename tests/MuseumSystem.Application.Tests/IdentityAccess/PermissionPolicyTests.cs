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
            "Identity.Manage"
        ];

        Assert.Equal(expected.Order(), PermissionNames.All.Order());
    }
}

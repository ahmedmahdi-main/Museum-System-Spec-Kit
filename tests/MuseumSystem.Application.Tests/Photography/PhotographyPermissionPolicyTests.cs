using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class PhotographyPermissionPolicyTests
{
    private static readonly string[] PhotographyPermissions =
    [
        PermissionNames.PhotographyView,
        PermissionNames.PhotographyUpload,
        PermissionNames.PhotographyManage,
        PermissionNames.PhotographyRequest,
        PermissionNames.PhotographyDelete
    ];

    public static TheoryData<string> PhotographyPermissionData => new()
    {
        PermissionNames.PhotographyView,
        PermissionNames.PhotographyUpload,
        PermissionNames.PhotographyManage,
        PermissionNames.PhotographyRequest,
        PermissionNames.PhotographyDelete
    };

    [Fact]
    public async Task All_photography_permissions_are_registered_as_authorization_policies()
    {
        var options = new AuthorizationOptions().AddMuseumPolicies();
        var provider = new DefaultAuthorizationPolicyProvider(Options.Create(options));

        foreach (var permission in PhotographyPermissions)
        {
            var policy = await provider.GetPolicyAsync(permission);

            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, requirement => requirement.GetType().Name.Contains("ClaimsAuthorizationRequirement", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Photography_permissions_match_approved_values_exactly()
    {
        Assert.Equal([
            "Photography.View",
            "Photography.Upload",
            "Photography.Manage",
            "Photography.Request",
            "Photography.Delete"
        ], PhotographyPermissions);

        Assert.All(PhotographyPermissions, permission => Assert.StartsWith("Photography.", permission, StringComparison.Ordinal));
        Assert.Equal(PhotographyPermissions, PermissionNames.All.Where(permission => permission.StartsWith("Photography.", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public void Photography_does_not_define_recovery_or_sixth_permission()
    {
        var photographyPermissions = PermissionNames.All
            .Where(permission => permission.StartsWith("Photography.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(5, photographyPermissions.Length);
        Assert.DoesNotContain("Photography.Recovery", photographyPermissions);
        Assert.DoesNotContain(photographyPermissions, permission => permission is "Photography.Admin" or "Photography.Supervisor" or "Photography.Edit");
    }

    [Theory]
    [MemberData(nameof(PhotographyPermissionData))]
    public async Task Photography_policy_allows_principal_with_exact_permission(string permission)
    {
        var service = CreateAuthorizationService();

        var result = await service.AuthorizeAsync(PrincipalWithPermissions(permission), permission);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(PhotographyPermissionData))]
    public async Task Photography_policy_denies_display_role_name_without_permission_claim(string permission)
    {
        var service = CreateAuthorizationService();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Photography Supervisor"),
            new Claim("role", MuseumRoleNames.PhotographySupervisor)
        ], authenticationType: "Test"));

        var result = await service.AuthorizeAsync(principal, permission);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Photography_policy_is_capability_oriented_not_role_name_or_all_photography_access()
    {
        var service = CreateAuthorizationService();

        var uploadResult = await service.AuthorizeAsync(PrincipalWithPermissions(PermissionNames.PhotographyUpload), PermissionNames.PhotographyUpload);
        var deleteResult = await service.AuthorizeAsync(PrincipalWithPermissions(PermissionNames.PhotographyUpload), PermissionNames.PhotographyDelete);

        Assert.True(uploadResult.Succeeded);
        Assert.False(deleteResult.Succeeded);
    }

    [Fact]
    public void Photography_role_presets_use_permissions_without_replacing_policy_enforcement()
    {
        Assert.Equal([
            PermissionNames.PhotographyView,
            PermissionNames.PhotographyUpload,
            PermissionNames.PhotographyManage
        ], MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Photographer]);

        Assert.Equal(PhotographyPermissions, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.PhotographySupervisor]);
        Assert.Contains(PermissionNames.PhotographyDelete, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Admin]);
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddMuseumPolicies());
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal PrincipalWithPermissions(params string[] permissions)
    {
        var claims = permissions.Select(permission => new Claim(MuseumAuthorizationPolicies.PermissionClaimType, permission));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}

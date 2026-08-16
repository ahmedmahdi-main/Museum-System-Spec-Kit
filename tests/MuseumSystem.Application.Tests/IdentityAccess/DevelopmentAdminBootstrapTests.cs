using System.Security.Claims;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Infrastructure.Identity;

namespace MuseumSystem.Application.Tests.IdentityAccess;

public sealed class DevelopmentAdminBootstrapTests
{
    [Fact]
    public void Development_admin_uses_explicit_environment_configuration_keys()
    {
        Assert.Equal("MUSEUMSYSTEM_DEV_ADMIN_USERNAME", IdentitySeed.DevelopmentAdminUsernameConfigurationKey);
        Assert.Equal("MUSEUMSYSTEM_DEV_ADMIN_PASSWORD", IdentitySeed.DevelopmentAdminPasswordConfigurationKey);
        Assert.Equal("MUSEUMSYSTEM_DEV_ADMIN_DISPLAY_NAME", IdentitySeed.DevelopmentAdminDisplayNameConfigurationKey);
    }

    [Fact]
    public void Admin_role_uses_existing_permission_preset()
    {
        Assert.Equal(PermissionNames.All.Order(), IdentitySeed.RolePermissions[IdentitySeed.AdminRole].Order());
        Assert.Equal(PermissionNames.All.Order(), MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Admin].Order());
    }

    [Fact]
    public void Missing_admin_permissions_are_idempotent_and_do_not_duplicate_existing_claims()
    {
        var existingClaims = PermissionNames.All
            .Select(permission => new Claim(MuseumAuthorizationPolicies.PermissionClaimType, permission))
            .Concat([new Claim(MuseumAuthorizationPolicies.PermissionClaimType, PermissionNames.ArtifactsView)])
            .ToArray();

        Assert.Empty(IdentitySeed.GetMissingAdminPermissions(existingClaims));
    }

    [Fact]
    public void Missing_admin_permissions_are_computed_from_permission_claim_type_only()
    {
        var existingClaims = new[]
        {
            new Claim(MuseumAuthorizationPolicies.PermissionClaimType, PermissionNames.ArtifactsView),
            new Claim("role", PermissionNames.ArtifactsManage)
        };

        var missing = IdentitySeed.GetMissingAdminPermissions(existingClaims);

        Assert.DoesNotContain(PermissionNames.ArtifactsView, missing);
        Assert.Contains(PermissionNames.ArtifactsManage, missing);
    }

    [Fact]
    public void Bootstrap_is_development_only_and_does_not_hardcode_a_password()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Infrastructure", "Identity", "IdentitySeed.cs"));

        Assert.Contains("if (!environment.IsDevelopment())", source);
        Assert.Contains("configuration[DevelopmentAdminPasswordConfigurationKey]", source);
        Assert.DoesNotContain("Password123", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("change-this-local-only", source, StringComparison.OrdinalIgnoreCase);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

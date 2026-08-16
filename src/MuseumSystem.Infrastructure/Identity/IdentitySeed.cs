using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Infrastructure.Identity;

public static class IdentitySeed
{
    public const string DevelopmentAdminUsernameConfigurationKey = "MUSEUMSYSTEM_DEV_ADMIN_USERNAME";
    public const string DevelopmentAdminPasswordConfigurationKey = "MUSEUMSYSTEM_DEV_ADMIN_PASSWORD";
    public const string DevelopmentAdminDisplayNameConfigurationKey = "MUSEUMSYSTEM_DEV_ADMIN_DISPLAY_NAME";

    public const string AdminRole = MuseumRoleNames.Admin;
    public const string StorekeeperRole = MuseumRoleNames.Storekeeper;
    public const string RegistryManagerRole = MuseumRoleNames.RegistryManager;
    public const string InventoryOfficerRole = MuseumRoleNames.InventoryOfficer;
    public const string ViewerRole = MuseumRoleNames.Viewer;
    public const string DocumentationStaffRole = MuseumRoleNames.DocumentationStaff;

    public static IReadOnlyList<string> RequiredPermissions => PermissionNames.All;

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions => MuseumRolePresets.PermissionsByRole;

    public static async Task SeedDevelopmentAdminAsync(
        this IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MuseumSystem.Infrastructure.Identity.IdentitySeed");

        var password = configuration[DevelopmentAdminPasswordConfigurationKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Development admin bootstrap skipped. Set {ConfigurationKey} to create or update the local Admin account.",
                DevelopmentAdminPasswordConfigurationKey);
            return;
        }

        var userName = configuration[DevelopmentAdminUsernameConfigurationKey];
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = "admin";
        }

        var displayName = configuration[DevelopmentAdminDisplayNameConfigurationKey];
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = userName;
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var adminRole = await roleManager.FindByNameAsync(AdminRole);
        if (adminRole is null)
        {
            adminRole = new ApplicationRole { Name = AdminRole };
            var createRoleResult = await roleManager.CreateAsync(adminRole);
            EnsureSucceeded(createRoleResult, "create the development Admin role");
        }

        var existingRoleClaims = await roleManager.GetClaimsAsync(adminRole);
        foreach (var permission in GetMissingAdminPermissions(existingRoleClaims))
        {
            var addClaimResult = await roleManager.AddClaimAsync(
                adminRole,
                new Claim(MuseumAuthorizationPolicies.PermissionClaimType, permission));
            EnsureSucceeded(addClaimResult, $"add Admin permission '{permission}'");
        }

        var adminUser = await userManager.FindByNameAsync(userName);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = userName,
                DisplayName = displayName
            };

            var createUserResult = await userManager.CreateAsync(adminUser, password);
            EnsureSucceeded(createUserResult, "create the development Admin user");
        }
        else if (!string.Equals(adminUser.DisplayName, displayName, StringComparison.Ordinal))
        {
            adminUser.DisplayName = displayName;
            var updateUserResult = await userManager.UpdateAsync(adminUser);
            EnsureSucceeded(updateUserResult, "update the development Admin display name");
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(adminUser, AdminRole);
            EnsureSucceeded(addRoleResult, "assign the development Admin role");
        }
    }

    public static IReadOnlyList<string> GetMissingAdminPermissions(IEnumerable<Claim> existingClaims)
    {
        var existingPermissions = existingClaims
            .Where(claim => claim.Type == MuseumAuthorizationPolicies.PermissionClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        return RolePermissions[AdminRole]
            .Where(permission => !existingPermissions.Contains(permission))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Could not {action}: {errors}");
    }
}

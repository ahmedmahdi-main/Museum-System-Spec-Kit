using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Infrastructure.Identity;

public static class IdentitySeed
{
    public const string AdminRole = MuseumRoleNames.Admin;
    public const string StorekeeperRole = MuseumRoleNames.Storekeeper;
    public const string RegistryManagerRole = MuseumRoleNames.RegistryManager;
    public const string InventoryOfficerRole = MuseumRoleNames.InventoryOfficer;
    public const string ViewerRole = MuseumRoleNames.Viewer;
    public const string DocumentationStaffRole = MuseumRoleNames.DocumentationStaff;

    public static IReadOnlyList<string> RequiredPermissions => PermissionNames.All;

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions => MuseumRolePresets.PermissionsByRole;
}

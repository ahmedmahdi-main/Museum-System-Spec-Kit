using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Infrastructure.Identity;

public static class IdentitySeed
{
    public const string AdminRole = "Admin";
    public const string StorekeeperRole = "Storekeeper";
    public const string RegistryManagerRole = "RegistryManager";
    public const string InventoryOfficerRole = "InventoryOfficer";
    public const string ViewerRole = "Viewer";

    public static IReadOnlyList<string> RequiredPermissions => PermissionNames.All;

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [AdminRole] = PermissionNames.All,
            [StorekeeperRole] =
            [
                PermissionNames.ArtifactsView,
                PermissionNames.StorehouseLocationsManage,
                PermissionNames.StorehouseDeliver,
                PermissionNames.StorehouseReturn
            ],
            [RegistryManagerRole] =
            [
                PermissionNames.ArtifactsView,
                PermissionNames.ArtifactsManage,
                PermissionNames.ImportsPreview,
                PermissionNames.ImportsCommit,
                PermissionNames.StorehouseCorrectionsCreate,
                PermissionNames.AuditView
            ],
            [InventoryOfficerRole] =
            [
                PermissionNames.ArtifactsView,
                PermissionNames.ImportsPreview,
                PermissionNames.StorehouseReconciliationManage,
                PermissionNames.StorehouseCorrectionsCreate
            ],
            [ViewerRole] = [PermissionNames.ArtifactsView]
        };
}

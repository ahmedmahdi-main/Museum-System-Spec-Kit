namespace MuseumSystem.Application.Modules.IdentityAccess;

public static class MuseumRoleNames
{
    public const string Admin = "Admin";
    public const string Storekeeper = "Storekeeper";
    public const string RegistryManager = "RegistryManager";
    public const string InventoryOfficer = "InventoryOfficer";
    public const string Viewer = "Viewer";
    public const string DocumentationStaff = "DocumentationStaff";
    public const string Photographer = "Photographer";
    public const string PhotographySupervisor = "PhotographySupervisor";
}

public static class MuseumRolePresets
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> PermissionsByRole { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [MuseumRoleNames.Admin] = PermissionNames.All,
            [MuseumRoleNames.Storekeeper] =
            [
                PermissionNames.ArtifactsView,
                PermissionNames.StorehouseLocationsManage,
                PermissionNames.StorehouseDeliver,
                PermissionNames.StorehouseReturn
            ],
            [MuseumRoleNames.RegistryManager] =
            [
                PermissionNames.ArtifactsView,
                PermissionNames.ArtifactsManage,
                PermissionNames.ImportsPreview,
                PermissionNames.ImportsCommit,
                PermissionNames.StorehouseCorrectionsCreate,
                PermissionNames.AuditView
            ],
            [MuseumRoleNames.InventoryOfficer] =
            [
                PermissionNames.ArtifactsView,
                PermissionNames.ImportsPreview,
                PermissionNames.StorehouseReconciliationManage,
                PermissionNames.StorehouseCorrectionsCreate
            ],
            [MuseumRoleNames.Viewer] = [PermissionNames.ArtifactsView],
            [MuseumRoleNames.DocumentationStaff] =
            [
                PermissionNames.DocumentationView,
                PermissionNames.DocumentationCreate,
                PermissionNames.DocumentationEdit,
                PermissionNames.DocumentationComplete,
                PermissionNames.DocumentationHistoryView,
                PermissionNames.DocumentationTemplatesView
            ],
            [MuseumRoleNames.Photographer] =
            [
                PermissionNames.PhotographyView,
                PermissionNames.PhotographyUpload,
                PermissionNames.PhotographyManage
            ],
            [MuseumRoleNames.PhotographySupervisor] =
            [
                PermissionNames.PhotographyView,
                PermissionNames.PhotographyUpload,
                PermissionNames.PhotographyManage,
                PermissionNames.PhotographyRequest,
                PermissionNames.PhotographyDelete
            ]
        };
}

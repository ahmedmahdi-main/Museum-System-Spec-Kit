using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace MuseumSystem.Application.Modules.IdentityAccess;

public static class PermissionNames
{
    public const string ArtifactsView = "Artifacts.View";
    public const string ArtifactsManage = "Artifacts.Manage";
    public const string StorehouseLocationsManage = "Storehouse.Locations.Manage";
    public const string StorehouseDeliver = "Storehouse.Deliver";
    public const string StorehouseReturn = "Storehouse.Return";
    public const string ImportsPreview = "Imports.Preview";
    public const string ImportsCommit = "Imports.Commit";
    public const string StorehouseReconciliationManage = "Storehouse.Reconciliation.Manage";
    public const string StorehouseCorrectionsCreate = "Storehouse.Corrections.Create";
    public const string AuditView = "Audit.View";
    public const string IdentityManage = "Identity.Manage";
    public const string DocumentationView = "Documentation.View";
    public const string DocumentationCreate = "Documentation.Create";
    public const string DocumentationEdit = "Documentation.Edit";
    public const string DocumentationComplete = "Documentation.Complete";
    public const string DocumentationHistoryView = "Documentation.History.View";
    public const string DocumentationTemplatesView = "Documentation.Templates.View";
    public const string DocumentationTemplatesManage = "Documentation.Templates.Manage";

    public static IReadOnlyList<string> All { get; } =
    [
        ArtifactsView,
        ArtifactsManage,
        StorehouseLocationsManage,
        StorehouseDeliver,
        StorehouseReturn,
        ImportsPreview,
        ImportsCommit,
        StorehouseReconciliationManage,
        StorehouseCorrectionsCreate,
        AuditView,
        IdentityManage,
        DocumentationView,
        DocumentationCreate,
        DocumentationEdit,
        DocumentationComplete,
        DocumentationHistoryView,
        DocumentationTemplatesView,
        DocumentationTemplatesManage
    ];
}

public static class MuseumAuthorizationPolicies
{
    public const string PermissionClaimType = "permission";

    public static AuthorizationOptions AddMuseumPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in PermissionNames.All)
        {
            options.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(PermissionClaimType, permission));
        }

        return options;
    }
}

public static class IdentityAccessServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityAccessApplication(this IServiceCollection services) => services;
}

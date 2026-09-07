using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Infrastructure.Audit;

namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyPermissionMatrixTests
{
    private static readonly string[] ApprovedPhotographyPermissions =
    [
        PermissionNames.PhotographyView,
        PermissionNames.PhotographyUpload,
        PermissionNames.PhotographyManage,
        PermissionNames.PhotographyRequest,
        PermissionNames.PhotographyDelete
    ];

    [Fact]
    public void Photography_permissions_are_exactly_the_five_declared_permissions()
    {
        var photographyConstants = typeof(PermissionNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.GetValue(null) is string value && value.StartsWith("Photography.", StringComparison.Ordinal))
            .Select(field => new { field.Name, Value = (string)field.GetValue(null)! })
            .OrderBy(field => field.Value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([
            new { Name = nameof(PermissionNames.PhotographyDelete), Value = "Photography.Delete" },
            new { Name = nameof(PermissionNames.PhotographyManage), Value = "Photography.Manage" },
            new { Name = nameof(PermissionNames.PhotographyRequest), Value = "Photography.Request" },
            new { Name = nameof(PermissionNames.PhotographyUpload), Value = "Photography.Upload" },
            new { Name = nameof(PermissionNames.PhotographyView), Value = "Photography.View" }
        ], photographyConstants);
        Assert.Equal(ApprovedPhotographyPermissions, PermissionNames.All.Where(permission => permission.StartsWith("Photography.", StringComparison.Ordinal)).ToArray());
        Assert.Equal(5, photographyConstants.Length);
        Assert.DoesNotContain("Photography.Admin", PermissionNames.All);
        Assert.DoesNotContain("Photography.Supervise", PermissionNames.All);
        Assert.DoesNotContain("Photography.Stream", PermissionNames.All);
        Assert.DoesNotContain("Photography.Recovery", PermissionNames.All);
        Assert.DoesNotContain("Photography.Storage", PermissionNames.All);
    }

    [Fact]
    public void Existing_non_photography_permission_contract_is_preserved_and_not_duplicated()
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
            "Documentation.Templates.Manage",
            "Photography.View",
            "Photography.Upload",
            "Photography.Manage",
            "Photography.Request",
            "Photography.Delete"
        ];

        Assert.Equal(expected, PermissionNames.All);
        Assert.Equal(PermissionNames.All.Count, PermissionNames.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(PermissionNames.ArtifactsView, PermissionNames.All);
        Assert.Contains(PermissionNames.StorehouseDeliver, PermissionNames.All);
        Assert.Contains(PermissionNames.StorehouseReturn, PermissionNames.All);
        Assert.Contains(PermissionNames.DocumentationView, PermissionNames.All);
        Assert.Contains(PermissionNames.DocumentationTemplatesManage, PermissionNames.All);
        Assert.Contains(PermissionNames.AuditView, PermissionNames.All);
        Assert.Contains(PermissionNames.IdentityManage, PermissionNames.All);
    }

    [Fact]
    public void Photography_role_presets_preserve_least_privilege_matrix()
    {
        Assert.Equal(PermissionNames.All, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Admin]);

        Assert.Equal([
            PermissionNames.PhotographyView,
            PermissionNames.PhotographyUpload,
            PermissionNames.PhotographyManage
        ], MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Photographer]);
        Assert.DoesNotContain(PermissionNames.PhotographyRequest, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Photographer]);
        Assert.DoesNotContain(PermissionNames.PhotographyDelete, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Photographer]);

        Assert.Equal(ApprovedPhotographyPermissions, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.PhotographySupervisor]);
    }

    [Theory]
    [InlineData(MuseumRoleNames.Storekeeper)]
    [InlineData(MuseumRoleNames.RegistryManager)]
    [InlineData(MuseumRoleNames.InventoryOfficer)]
    [InlineData(MuseumRoleNames.Viewer)]
    [InlineData(MuseumRoleNames.DocumentationStaff)]
    public void Legacy_roles_do_not_gain_photography_permissions(string roleName)
    {
        var permissions = MuseumRolePresets.PermissionsByRole[roleName];

        Assert.DoesNotContain(permissions, permission => permission.StartsWith("Photography.", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(PhotographyPermissionData))]
    public async Task Photography_permissions_use_standard_authenticated_permission_claim_policies(string permission)
    {
        var options = new AuthorizationOptions().AddMuseumPolicies();
        var provider = new DefaultAuthorizationPolicyProvider(Options.Create(options));
        var policy = await provider.GetPolicyAsync(permission);

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
        var claimRequirement = Assert.Single(policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal(MuseumAuthorizationPolicies.PermissionClaimType, claimRequirement.ClaimType);
        Assert.Equal([permission], claimRequirement.AllowedValues);

        var service = CreateAuthorizationService();
        Assert.True((await service.AuthorizeAsync(PrincipalWithPermissions(permission), permission)).Succeeded);
        Assert.False((await service.AuthorizeAsync(PrincipalWithPermissions(PermissionNames.PhotographyView), permission)).Succeeded && permission != PermissionNames.PhotographyView);
        Assert.False((await service.AuthorizeAsync(UnauthenticatedPrincipalWithPermission(permission), permission)).Succeeded);
        Assert.False((await service.AuthorizeAsync(PrincipalWithWrongClaimType(permission), permission)).Succeeded);
    }

    [Fact]
    public void Photography_pages_and_stream_endpoint_use_expected_authorization_policies()
    {
        var root = FindRepositoryRoot();
        var gallery = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");
        var upload = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");
        var endpoint = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "ImageStreamEndpoint.cs");

        Assert.Contains("@page \"/photography/artifacts/{ArtifactId:guid}\"", gallery);
        Assert.Contains($"@attribute [Authorize(Policy = PermissionNames.{nameof(PermissionNames.PhotographyView)})]", gallery);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyUpload), Slice(gallery, "@attribute [Authorize", "@rendermode"));
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyManage), Slice(gallery, "@attribute [Authorize", "@rendermode"));
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyRequest), Slice(gallery, "@attribute [Authorize", "@rendermode"));
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyDelete), Slice(gallery, "@attribute [Authorize", "@rendermode"));

        Assert.Contains("@page \"/photography/upload\"", upload);
        Assert.Contains($"@attribute [Authorize(Policy = PermissionNames.{nameof(PermissionNames.PhotographyUpload)})]", upload);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyView), Slice(upload, "@attribute [Authorize", "@rendermode"));
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyManage), Slice(upload, "@attribute [Authorize", "@rendermode"));
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyDelete), Slice(upload, "@attribute [Authorize", "@rendermode"));

        Assert.Contains("public const string Route = \"/photography/images/{artifactImageId:guid}/{rendition}\";", endpoint);
        Assert.Contains($".RequireAuthorization(PermissionNames.{nameof(PermissionNames.PhotographyView)})", endpoint);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyUpload), endpoint);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyManage), endpoint);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyRequest), endpoint);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyDelete), endpoint);
        Assert.DoesNotContain("Presigned", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bucket", endpoint, StringComparison.OrdinalIgnoreCase);
        AssertRoutesAndNavigationPreserveExistingAuthorizationEnforcement(root);
    }

    [Fact]
    public void Photography_request_workspace_preserves_authentication_and_capability_matrix()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Requests.razor");
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyRequestPanel.razor");

        Assert.Contains("@page \"/photography/requests\"", page);
        Assert.Contains("@attribute [Authorize]", page);
        Assert.DoesNotContain("@attribute [Authorize(Policy =", page);
        Assert.Contains("@inject IAuthorizationService AuthorizationService", page);
        Assert.Contains("@inject AuthenticationStateProvider AuthenticationStateProvider", page);
        Assert.Contains($"canRequest = await IsAuthorizedAsync(state, PermissionNames.{nameof(PermissionNames.PhotographyRequest)});", page);
        Assert.Contains($"canManage = await IsAuthorizedAsync(state, PermissionNames.{nameof(PermissionNames.PhotographyManage)});", page);
        Assert.Contains($"canUpload = await IsAuthorizedAsync(state, PermissionNames.{nameof(PermissionNames.PhotographyUpload)});", page);
        Assert.Contains("hasRequestWorkflowAccess = canRequest || canManage || canUpload;", page);
        Assert.Contains("private bool CanCreateRequest => canRequest && selectedArtifact is not null && !isCreating;", page);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyView), page);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyDelete), page);

        Assert.Contains("[Parameter] public bool CanManage { get; set; }", panel);
        Assert.Contains("[Parameter] public bool CanUpload { get; set; }", panel);
        Assert.Contains("CanManage || IsOwnRequest", panel);
        Assert.Contains("Request?.Request.Status == PhotographyRequestStatus.Pending", panel);
        Assert.Contains("&& CanUpload", panel);
        Assert.DoesNotContain("MuseumRoleNames", page + panel);
        Assert.DoesNotContain("IsInRole", page + panel);
    }

    private static void AssertRoutesAndNavigationPreserveExistingAuthorizationEnforcement(DirectoryInfo root)
    {
        var routes = Read(root, "src", "MuseumSystem.Web", "Components", "Routes.razor");
        var nav = Read(root, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor");
        var photographyNav = Slice(nav, "<div class=\"nav-group\">\n        @if (canUploadPhotography || canUsePhotographyRequests)", "</div>\r\n\r\n    <div class=\"nav-group\">\r\n        <AuthorizeView Policy=\"@PermissionNames.AuditView\">");

        Assert.Contains("<AuthorizeRouteView RouteData=\"routeData\" DefaultLayout=\"typeof(Layout.MainLayout)\">", routes);
        Assert.DoesNotContain("<RouteView", routes);

        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.ArtifactsView)}\">", nav);
        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.DocumentationView)}\">", nav);
        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.AuditView)}\">", nav);
        Assert.Contains("canUploadPhotography = await IsAuthorizedAsync(state, PermissionNames.PhotographyUpload);", nav);
        Assert.Contains("await IsAuthorizedAsync(state, PermissionNames.PhotographyRequest)", nav);
        Assert.Contains("await IsAuthorizedAsync(state, PermissionNames.PhotographyManage)", nav);
        Assert.Contains("|| canUploadPhotography;", nav);
        Assert.Contains("href=\"photography/upload\"", photographyNav);
        Assert.Contains("href=\"photography/requests\"", photographyNav);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyView), photographyNav);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyDelete), photographyNav);
        Assert.DoesNotContain("@attribute [Authorize]", nav);
    }

    [Fact]
    public void Photography_delete_remains_distinct_from_manage_in_the_web_workflow()
    {
        var root = FindRepositoryRoot();
        var gallery = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");
        var deletion = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyImageDeletionDialog.razor");

        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.PhotographyManage)}\">", gallery);
        Assert.Contains("<PhotographyImageDeletionDialog SelectedImage=\"selectedImage\"", gallery);
        Assert.Contains($"PermissionNames.{nameof(PermissionNames.PhotographyUpload)}", deletion);
        Assert.Contains($"PermissionNames.{nameof(PermissionNames.PhotographyDelete)}", deletion);
        Assert.Contains("canDelete = (await AuthorizationService.AuthorizeAsync(state.User, PermissionNames.PhotographyDelete)).Succeeded;", deletion);
        Assert.Contains("new DeleteArtifactImagePrivilegedCommand(", deletion);
        Assert.Contains("new DeleteArtifactImageByUploaderGraceCommand(", deletion);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyManage), deletion);
        Assert.DoesNotContain("canManage", deletion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanManage", deletion);
    }

    [Theory]
    [InlineData(MuseumRoleNames.Photographer)]
    [InlineData(MuseumRoleNames.PhotographySupervisor)]
    public void Audit_view_remains_separate_from_photography_permissions_and_roles(string photographyRoleName)
    {
        var root = FindRepositoryRoot();
        var auditTrail = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Admin", "AuditTrail.razor");

        Assert.Contains("@page \"/admin/audit\"", auditTrail);
        Assert.Contains($"@attribute [Authorize(Policy = PermissionNames.{nameof(PermissionNames.AuditView)})]", auditTrail);
        Assert.DoesNotContain("Photography.", auditTrail);

        Assert.DoesNotContain(PermissionNames.AuditView, MuseumRolePresets.PermissionsByRole[photographyRoleName]);
        Assert.Contains(PermissionNames.AuditView, MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Admin]);
    }

    [Fact]
    public void Central_audit_contract_and_attribution_rules_are_preserved_for_photography()
    {
        var auditEntry = AuditEntry.Create(" actor-1 ", " Photography.Image.Upload ", " Photography ", " ArtifactImage ", " image-1 ", " Uploaded image. ", " change ");
        var auditWriterSource = Read(FindRepositoryRoot(), "src", "MuseumSystem.Infrastructure", "Audit", "AuditWriter.cs");

        Assert.NotEqual(Guid.Empty, auditEntry.AuditEntryId);
        Assert.Equal("actor-1", auditEntry.ActorUserId);
        Assert.Equal("Photography.Image.Upload", auditEntry.ActionName);
        Assert.Equal("Photography", auditEntry.ModuleName);
        Assert.Equal("ArtifactImage", auditEntry.EntityName);
        Assert.Equal("image-1", auditEntry.EntityId);
        Assert.True(auditEntry.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal("Uploaded image.", auditEntry.Summary);
        Assert.Equal("change", auditEntry.ChangeSummary);

        Assert.False(ShouldUseAttributedActor(new AuditWriteRequest(
            PhotographyAuditActions.ImageUpload,
            "Photography",
            "ArtifactImage",
            "image-1",
            "Upload summary.",
            AttributedActorUserId: "business-actor")));
        Assert.False(ShouldUseAttributedActor(new AuditWriteRequest(
            PhotographyAuditActions.PrimaryImageChange,
            "Photography",
            "ArtifactImage",
            "image-1",
            "Primary change.",
            AttributedActorUserId: "business-actor")));
        Assert.False(ShouldUseAttributedActor(new AuditWriteRequest(
            PhotographyAuditActions.StorageRecoveryRetry,
            "Photography",
            "StorageOperationRecovery",
            "recovery-1",
            "Recovery retry.",
            AttributedActorUserId: "business-actor")));
        Assert.True(ShouldUseAttributedActor(new AuditWriteRequest(
            PhotographyAuditActions.ImageDeleteByUploaderGrace,
            "Photography",
            "ArtifactImage",
            "image-1",
            "Deletion summary.",
            AttributedActorUserId: "uploader-1")));
        Assert.True(ShouldUseAttributedActor(new AuditWriteRequest(
            PhotographyAuditActions.ImageDeletePrivileged,
            "Photography",
            "ArtifactImage",
            "image-1",
            "Deletion summary.",
            AttributedActorUserId: "supervisor-1")));
        Assert.False(ShouldUseAttributedActor(new AuditWriteRequest(
            PhotographyAuditActions.ImageDeletePrivileged,
            "Photography",
            "ArtifactImage",
            "image-1",
            "Deletion summary.")));

        Assert.Contains("dbContext.AuditEntries.Add(entry);", auditWriterSource);
        Assert.Contains("AuditEntry.Create(actorUserId, request.ActionName, request.ModuleName, request.EntityName, request.EntityId, request.Summary, request.ChangeSummary)", auditWriterSource);
        Assert.DoesNotContain("PhotographyAuditEntry", auditWriterSource);
        Assert.DoesNotContain("BucketName", auditWriterSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OriginalObjectKey", auditWriterSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Minio", auditWriterSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PermissionClaimType", auditWriterSource);
    }

    public static TheoryData<string> PhotographyPermissionData => new()
    {
        PermissionNames.PhotographyView,
        PermissionNames.PhotographyUpload,
        PermissionNames.PhotographyManage,
        PermissionNames.PhotographyRequest,
        PermissionNames.PhotographyDelete
    };

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

    private static ClaimsPrincipal UnauthenticatedPrincipalWithPermission(string permission) =>
        new(new ClaimsIdentity([new Claim(MuseumAuthorizationPolicies.PermissionClaimType, permission)]));

    private static ClaimsPrincipal PrincipalWithWrongClaimType(string permission) =>
        new(new ClaimsIdentity([new Claim("role", permission)], authenticationType: "Test"));

    private static bool ShouldUseAttributedActor(AuditWriteRequest request)
    {
        var method = typeof(AuditWriter).GetMethod("ShouldUseAttributedActor", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [request])!;
    }

    private static string Slice(string source, string startNeedle, string endNeedle)
    {
        var start = source.IndexOf(startNeedle, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker {startNeedle}.");
        var end = source.IndexOf(endNeedle, start + startNeedle.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end marker {endNeedle}.");
        return source[start..end];
    }

    private static string Read(DirectoryInfo root, params string[] segments) =>
        File.ReadAllText(Path.Combine([root.FullName, .. segments]));

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

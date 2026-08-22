using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationAuthorizationTests
{
    private static readonly string[] DocumentationPermissionValues =
    [
        PermissionNames.DocumentationView,
        PermissionNames.DocumentationCreate,
        PermissionNames.DocumentationEdit,
        PermissionNames.DocumentationComplete,
        PermissionNames.DocumentationHistoryView,
        PermissionNames.DocumentationTemplatesView,
        PermissionNames.DocumentationTemplatesManage
    ];

    public static TheoryData<string> DocumentationPermissions => new()
    {
        PermissionNames.DocumentationView,
        PermissionNames.DocumentationCreate,
        PermissionNames.DocumentationEdit,
        PermissionNames.DocumentationComplete,
        PermissionNames.DocumentationHistoryView,
        PermissionNames.DocumentationTemplatesView,
        PermissionNames.DocumentationTemplatesManage
    };

    [Theory]
    [MemberData(nameof(DocumentationPermissions))]
    public async Task Documentation_policy_allows_principal_with_exact_permission(string permission)
    {
        var service = CreateAuthorizationService();

        var result = await service.AuthorizeAsync(PrincipalWith(permission), permission);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(DocumentationPermissions))]
    public async Task Documentation_policy_denies_principal_without_permission(string permission)
    {
        var service = CreateAuthorizationService();

        var result = await service.AuthorizeAsync(PrincipalWith(), permission);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(DocumentationPermissions))]
    public async Task Documentation_policy_denies_different_documentation_permission(string permission)
    {
        var service = CreateAuthorizationService();
        var differentPermission = DocumentationPermissionValues.First(candidate => candidate != permission);

        var result = await service.AuthorizeAsync(PrincipalWith(differentPermission), permission);

        Assert.False(result.Succeeded);
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddMuseumPolicies());
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal PrincipalWith(params string[] permissions)
    {
        var claims = permissions.Select(permission => new Claim(MuseumAuthorizationPolicies.PermissionClaimType, permission));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}

using Microsoft.AspNetCore.Http;
using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Infrastructure.Identity;

public sealed class HttpCurrentActorPermissionChecker(IHttpContextAccessor httpContextAccessor) : ICurrentActorPermissionChecker
{
    public bool HasPermission(string permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true
            && user.HasClaim(MuseumAuthorizationPolicies.PermissionClaimType, permissionName);
    }
}

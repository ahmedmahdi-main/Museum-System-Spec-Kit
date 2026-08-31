namespace MuseumSystem.Application.Modules.IdentityAccess;

public interface ICurrentActorPermissionChecker
{
    bool HasPermission(string permissionName);
}

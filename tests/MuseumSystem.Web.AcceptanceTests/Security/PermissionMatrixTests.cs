using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Security;

public sealed class PermissionMatrixTests
{
    [Fact]
    public void Every_role_preset_uses_declared_permissions_only()
    {
        var declared = PermissionNames.All.ToHashSet(StringComparer.Ordinal);

        foreach (var preset in MuseumRolePresets.PermissionsByRole)
        {
            Assert.NotEmpty(preset.Value);
            Assert.All(preset.Value, permission => Assert.Contains(permission, declared));
        }
    }

    [Fact]
    public void Admin_role_contains_every_permission()
    {
        Assert.Equal(PermissionNames.All.OrderBy(p => p), MuseumRolePresets.PermissionsByRole[MuseumRoleNames.Admin].OrderBy(p => p));
    }

    [Fact]
    public void Feature_pages_have_authorization_attributes_with_known_policies()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages");
        var pageFiles = Directory.GetFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("Error.razor", StringComparison.OrdinalIgnoreCase) && !path.EndsWith("NotFound.razor", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var knownPermissions = PermissionNames.All.ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(pageFiles);
        foreach (var page in pageFiles)
        {
            var text = File.ReadAllText(page);
            Assert.Contains("@attribute [Authorize", text);
            foreach (var policy in ExtractPolicyNames(text))
            {
                Assert.Contains(policy, knownPermissions);
            }
        }
    }

    private static IEnumerable<string> ExtractPolicyNames(string text)
    {
        const string marker = "Policy = PermissionNames.";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var start = index + marker.Length;
            var end = start;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            var property = text[start..end];
            var field = typeof(PermissionNames).GetFields().FirstOrDefault(field => field.Name == property);
            if (field?.GetValue(null) is string permission) yield return permission;
            index = text.IndexOf(marker, end, StringComparison.Ordinal);
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

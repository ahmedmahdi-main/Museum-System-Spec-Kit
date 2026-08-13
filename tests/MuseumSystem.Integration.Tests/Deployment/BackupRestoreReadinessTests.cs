namespace MuseumSystem.Integration.Tests.Deployment;

public sealed class BackupRestoreReadinessTests
{
    [Fact]
    public void Quickstart_and_uat_results_include_backup_restore_drill()
    {
        var root = FindRepositoryRoot();
        var quickstart = File.ReadAllText(Path.Combine(root.FullName, "specs", "001-central-artifact-registry", "quickstart.md"));
        var uatPath = Path.Combine(root.FullName, "specs", "001-central-artifact-registry", "checklists", "uat-results.md");

        Assert.Contains("pg_dump", quickstart);
        Assert.Contains("pg_restore", quickstart);
        Assert.True(File.Exists(uatPath));
        Assert.Contains("Backup/Restore", File.ReadAllText(uatPath));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

namespace MuseumSystem.Integration.Tests.Quickstart;

public sealed class BuildDatabaseValidationTests
{
    [Fact]
    public void Quickstart_documents_build_test_and_migration_validation_commands()
    {
        var root = FindRepositoryRoot();
        var quickstart = File.ReadAllText(Path.Combine(root.FullName, "specs", "001-central-artifact-registry", "quickstart.md"));

        Assert.Contains("dotnet restore", quickstart);
        Assert.Contains("dotnet build", quickstart);
        Assert.Contains("dotnet test", quickstart);
        Assert.Contains("dotnet ef database update", quickstart);
        Assert.Contains("Windows Server", quickstart);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

namespace MuseumSystem.Integration.Tests.Foundation;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void Solution_contains_phase_one_projects()
    {
        var root = FindRepositoryRoot();
        var solution = Path.Combine(root.FullName, "Museum-System.sln");

        Assert.True(File.Exists(solution), $"Expected solution at {solution}");

        var solutionText = File.ReadAllText(solution).Replace('\\', '/');
        string[] expectedProjects =
        [
            "src/MuseumSystem.Web/MuseumSystem.Web.csproj",
            "src/MuseumSystem.Domain/MuseumSystem.Domain.csproj",
            "src/MuseumSystem.Application/MuseumSystem.Application.csproj",
            "src/MuseumSystem.Infrastructure/MuseumSystem.Infrastructure.csproj",
            "tests/MuseumSystem.Domain.Tests/MuseumSystem.Domain.Tests.csproj",
            "tests/MuseumSystem.Application.Tests/MuseumSystem.Application.Tests.csproj",
            "tests/MuseumSystem.Integration.Tests/MuseumSystem.Integration.Tests.csproj",
            "tests/MuseumSystem.Web.AcceptanceTests/MuseumSystem.Web.AcceptanceTests.csproj"
        ];

        foreach (var expectedProject in expectedProjects)
        {
            Assert.Contains(expectedProject, solutionText);
        }
    }

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

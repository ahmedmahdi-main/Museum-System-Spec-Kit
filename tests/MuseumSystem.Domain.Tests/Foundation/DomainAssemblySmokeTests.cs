using System.Reflection;

namespace MuseumSystem.Domain.Tests.Foundation;

public sealed class DomainAssemblySmokeTests
{
    [Fact]
    public void Domain_assembly_loads()
    {
        var assembly = Assembly.Load("MuseumSystem.Domain");

        Assert.Equal("MuseumSystem.Domain", assembly.GetName().Name);
    }
}

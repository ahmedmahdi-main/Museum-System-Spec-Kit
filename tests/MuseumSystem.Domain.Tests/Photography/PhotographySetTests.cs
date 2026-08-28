using System.Reflection;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class PhotographySetTests
{
    [Fact]
    public void Valid_create_succeeds_and_records_only_photography_set_context()
    {
        var artifactId = Guid.NewGuid();
        var set = PhotographySet.Create(
            artifactId,
            PhotographyPurpose.DuringMaintenance,
            new DateOnly(2026, 8, 25),
            " photographer-1 ",
            " creator-1 ");

        Assert.NotEqual(Guid.Empty, set.PhotographySetId);
        Assert.Equal(artifactId, set.ArtifactId);
        Assert.Equal(PhotographyPurpose.DuringMaintenance, set.Purpose);
        Assert.Equal(new DateOnly(2026, 8, 25), set.PhotographyDate);
        Assert.Equal("photographer-1", set.PhotographerUserId);
        Assert.Equal("creator-1", set.CreatedByUserId);
        Assert.Equal(0, set.ConcurrencyToken);
    }

    [Fact]
    public void Artifact_id_is_required()
    {
        Assert.Throws<ArgumentException>(() => PhotographySet.Create(
            Guid.Empty,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 25),
            "photographer-1"));
    }

    [Fact]
    public void Only_approved_photography_purposes_are_supported()
    {
        Assert.Equal([
            nameof(PhotographyPurpose.GeneralDocumentation),
            nameof(PhotographyPurpose.PreMaintenance),
            nameof(PhotographyPurpose.DuringMaintenance),
            nameof(PhotographyPurpose.PostMaintenance)
        ], Enum.GetNames<PhotographyPurpose>());

        Assert.Throws<ArgumentOutOfRangeException>(() => PhotographySet.Create(
            Guid.NewGuid(),
            (PhotographyPurpose)999,
            new DateOnly(2026, 8, 25),
            "photographer-1"));
    }

    [Fact]
    public void Photography_date_is_required()
    {
        Assert.Throws<ArgumentException>(() => PhotographySet.Create(
            Guid.NewGuid(),
            PhotographyPurpose.GeneralDocumentation,
            default,
            "photographer-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Photographer_user_id_is_required(string photographerUserId)
    {
        Assert.Throws<ArgumentException>(() => PhotographySet.Create(
            Guid.NewGuid(),
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 25),
            photographerUserId));
    }

    [Fact]
    public void Created_by_whitespace_is_normalized_to_null()
    {
        var set = PhotographySet.Create(
            Guid.NewGuid(),
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 25),
            "photographer-1",
            "   ");

        Assert.Null(set.CreatedByUserId);
    }

    [Fact]
    public void Artifact_and_set_context_are_immutable_after_creation()
    {
        var mutableMembers = typeof(PhotographySet)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(mutableMembers);
        Assert.DoesNotContain(typeof(PhotographySet).GetMethods(BindingFlags.Instance | BindingFlags.Public), method => method.Name.StartsWith("Update", StringComparison.Ordinal));
    }

    [Fact]
    public void Set_does_not_snapshot_or_own_artifact_registry_or_custody_state()
    {
        var memberNames = typeof(PhotographySet)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();

        Assert.DoesNotContain(memberNames, name => name.Contains("MuseumNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("ArtifactName", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Category", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Custody", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Movement", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Location", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("Documentation", StringComparison.OrdinalIgnoreCase));
    }
}

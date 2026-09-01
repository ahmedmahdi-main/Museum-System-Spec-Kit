using System.Reflection;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class PrimaryImageRulesTests
{
    [Fact]
    public void Available_image_belonging_to_the_artifact_is_primary_eligible()
    {
        var artifactId = Guid.NewGuid();
        var image = CreateImage(artifactId);

        Assert.True(PhotographyRules.IsPrimaryImageEligible(image, artifactId));
    }

    [Fact]
    public void Image_belonging_to_a_different_artifact_is_not_primary_eligible()
    {
        var image = CreateImage(Guid.NewGuid());

        Assert.False(PhotographyRules.IsPrimaryImageEligible(image, Guid.NewGuid()));
    }

    [Fact]
    public void Delete_pending_image_is_not_primary_eligible()
    {
        var artifactId = Guid.NewGuid();
        var image = CreateImage(artifactId);
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);

        Assert.False(PhotographyRules.IsPrimaryImageEligible(image, artifactId));
    }

    [Fact]
    public void Deleted_image_is_not_primary_eligible()
    {
        var artifactId = Guid.NewGuid();
        var image = CreateImage(artifactId);
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DateTimeOffset.UtcNow);

        Assert.False(PhotographyRules.IsPrimaryImageEligible(image, artifactId));
    }

    [Fact]
    public void Photography_state_is_created_with_no_primary_image_and_initial_concurrency_token()
    {
        var artifactId = Guid.NewGuid();

        var state = ArtifactPhotographyState.Create(artifactId);

        Assert.Equal(artifactId, state.ArtifactId);
        Assert.Null(state.PrimaryImageId);
        Assert.Equal(0, state.ConcurrencyToken);
        Assert.Null(state.UpdatedAt);
    }

    [Fact]
    public void Set_primary_image_assigns_the_authoritative_reference_and_advances_concurrency()
    {
        var state = ArtifactPhotographyState.Create(Guid.NewGuid());
        var imageId = Guid.NewGuid();

        state.SetPrimaryImage(imageId, "manager-1");

        Assert.Equal(imageId, state.PrimaryImageId);
        Assert.Equal(1, state.ConcurrencyToken);
        Assert.Equal("manager-1", state.UpdatedByUserId);
        Assert.NotNull(state.UpdatedAt);
    }

    [Fact]
    public void Setting_a_new_primary_image_replaces_the_previous_one_with_a_single_authoritative_value()
    {
        var state = ArtifactPhotographyState.Create(Guid.NewGuid());
        var imageA = Guid.NewGuid();
        var imageB = Guid.NewGuid();

        state.SetPrimaryImage(imageA, "manager-1");
        state.SetPrimaryImage(imageB, "manager-2");

        Assert.Equal(imageB, state.PrimaryImageId);
        Assert.Equal(2, state.ConcurrencyToken);
    }

    [Fact]
    public void Clear_primary_image_removes_the_reference_and_advances_concurrency()
    {
        var state = ArtifactPhotographyState.Create(Guid.NewGuid());
        state.SetPrimaryImage(Guid.NewGuid(), "manager-1");

        state.ClearPrimaryImage("manager-2");

        Assert.Null(state.PrimaryImageId);
        Assert.Equal(2, state.ConcurrencyToken);
        Assert.Equal("manager-2", state.UpdatedByUserId);
    }

    [Fact]
    public void Clearing_the_primary_image_never_auto_selects_a_replacement()
    {
        var state = ArtifactPhotographyState.Create(Guid.NewGuid());
        state.SetPrimaryImage(Guid.NewGuid(), "manager-1");

        state.ClearPrimaryImage("manager-1");

        Assert.Null(state.PrimaryImageId);

        var clearMethod = typeof(ArtifactPhotographyState).GetMethod(nameof(ArtifactPhotographyState.ClearPrimaryImage));
        Assert.NotNull(clearMethod);
        Assert.DoesNotContain(clearMethod!.GetParameters(), parameter =>
            typeof(System.Collections.IEnumerable).IsAssignableFrom(parameter.ParameterType) && parameter.ParameterType != typeof(string));
    }

    [Fact]
    public void Setting_an_empty_primary_image_id_is_rejected()
    {
        var state = ArtifactPhotographyState.Create(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => state.SetPrimaryImage(Guid.Empty));
        Assert.Null(state.PrimaryImageId);
        Assert.Equal(0, state.ConcurrencyToken);
    }

    [Fact]
    public void Artifact_image_does_not_own_an_independent_primary_authority_property()
    {
        var forbiddenNames = new[] { "IsPrimary", "Primary", "PrimaryImage" };
        var memberNames = typeof(ArtifactImage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name);

        Assert.Empty(memberNames.Intersect(forbiddenNames, StringComparer.Ordinal));
    }

    private static ArtifactImage CreateImage(Guid artifactId) =>
        ArtifactImage.Create(
            artifactId,
            Guid.NewGuid(),
            ImageStorageObjectKey.Create($"artifact-images/originals/{Guid.NewGuid():N}.jpg"),
            "image.jpg",
            "image/jpeg",
            1024,
            800,
            600,
            "photographer-1",
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
}

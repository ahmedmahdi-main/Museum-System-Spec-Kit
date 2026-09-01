using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PrimaryImageConcurrencyTests(PostgresPhotographyTestFixture fixture)
{
    [Fact]
    public async Task Competing_set_primary_writes_leave_exactly_one_authoritative_winner()
    {
        var (artifactId, imageAId, imageBId) = await SeedArtifactWithTwoImagesAsync("RR", primaryImageId: null);

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        var second = await secondContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);

        second.SetPrimaryImage(imageBId, "manager-b");
        await secondContext.SaveChangesAsync();

        first.SetPrimaryImage(imageAId, "manager-a");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firstContext.SaveChangesAsync());

        await using var reload = fixture.CreateContext();
        var authoritative = await reload.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        Assert.Equal(imageBId, authoritative.PrimaryImageId);
        Assert.Equal(1, authoritative.ConcurrencyToken);
    }

    [Fact]
    public async Task Set_primary_beats_a_stale_clear_primary_and_the_new_primary_wins()
    {
        var (artifactId, imageAId, imageBId) = await SeedArtifactWithTwoImagesAsync("SC", primaryImageId: null);
        await SetInitialPrimaryAsync(artifactId, imageAId);

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var settingContext = await firstContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        var clearingContext = await secondContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);

        settingContext.SetPrimaryImage(imageBId, "manager-set");
        await firstContext.SaveChangesAsync();

        clearingContext.ClearPrimaryImage("manager-clear");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());

        await using var reload = fixture.CreateContext();
        var authoritative = await reload.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        Assert.Equal(imageBId, authoritative.PrimaryImageId);
    }

    [Fact]
    public async Task Clear_primary_beats_a_stale_set_primary_and_the_artifact_is_left_without_a_primary()
    {
        var (artifactId, imageAId, imageBId) = await SeedArtifactWithTwoImagesAsync("CS", primaryImageId: null);
        await SetInitialPrimaryAsync(artifactId, imageAId);

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var clearingContext = await firstContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        var settingContext = await secondContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);

        clearingContext.ClearPrimaryImage("manager-clear");
        await firstContext.SaveChangesAsync();

        settingContext.SetPrimaryImage(imageBId, "manager-set");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());

        await using var reload = fixture.CreateContext();
        var authoritative = await reload.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        Assert.Null(authoritative.PrimaryImageId);
    }

    private async Task<(Guid ArtifactId, Guid ImageAId, Guid ImageBId)> SeedArtifactWithTwoImagesAsync(string prefix, Guid? primaryImageId)
    {
        await using var seed = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, prefix);
        var set = await PhotographyPersistenceTestData.SeedSetAsync(seed, artifact.ArtifactId);
        var imageA = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifact.ArtifactId, set.PhotographySetId, $"original/{prefix}-primary-race-1");
        var imageB = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifact.ArtifactId, set.PhotographySetId, $"original/{prefix}-primary-race-2");

        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        if (primaryImageId is not null)
        {
            state.SetPrimaryImage(primaryImageId.Value, "seed-manager");
        }

        seed.ArtifactPhotographyStates.Add(state);
        await seed.SaveChangesAsync();

        return (artifact.ArtifactId, imageA.ArtifactImageId, imageB.ArtifactImageId);
    }

    private async Task SetInitialPrimaryAsync(Guid artifactId, Guid imageId)
    {
        await using var context = fixture.CreateContext();
        var photographyState = await context.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        photographyState.SetPrimaryImage(imageId, "seed-manager");
        await context.SaveChangesAsync();
    }
}

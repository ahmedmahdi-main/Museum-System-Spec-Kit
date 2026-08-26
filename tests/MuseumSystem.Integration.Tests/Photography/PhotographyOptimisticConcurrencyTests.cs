using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyOptimisticConcurrencyTests(PostgresPhotographyTestFixture fixture)
{
    [Fact]
    public async Task Photography_set_rejects_stale_competing_write()
    {
        Guid setId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "CS");
            var set = await PhotographyPersistenceTestData.SeedSetAsync(seed, artifact.ArtifactId);
            setId = set.PhotographySetId;
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.PhotographySets.SingleAsync(set => set.PhotographySetId == setId);
        var second = await secondContext.PhotographySets.SingleAsync(set => set.PhotographySetId == setId);

        secondContext.Entry(second).Property(nameof(PhotographySet.ConcurrencyToken)).CurrentValue = second.ConcurrencyToken + 1;
        secondContext.Entry(second).Property(nameof(PhotographySet.ConcurrencyToken)).IsModified = true;
        await secondContext.SaveChangesAsync();

        firstContext.Entry(first).Property(nameof(PhotographySet.ConcurrencyToken)).CurrentValue = first.ConcurrencyToken + 1;
        firstContext.Entry(first).Property(nameof(PhotographySet.ConcurrencyToken)).IsModified = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firstContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Artifact_image_rejects_stale_competing_write()
    {
        Guid imageId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "CI");
            var set = await PhotographyPersistenceTestData.SeedSetAsync(seed, artifact.ArtifactId);
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifact.ArtifactId, set.PhotographySetId, "original/concurrency-image");
            imageId = image.ArtifactImageId;
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.ArtifactImages.SingleAsync(image => image.ArtifactImageId == imageId);
        var second = await secondContext.ArtifactImages.SingleAsync(image => image.ArtifactImageId == imageId);

        second.UpdateCaption("Second update");
        await secondContext.SaveChangesAsync();

        first.UpdateCaption("First stale update");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firstContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Photography_upload_operation_rejects_stale_competing_write()
    {
        Guid operationId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "CO");
            var operation = PhotographyUploadOperation.Start("actor", PhotographyUploadOperationKind.CreateSetUpload, "idem-concurrency", "fingerprint", artifact.ArtifactId);
            seed.PhotographyUploadOperations.Add(operation);
            await seed.SaveChangesAsync();
            operationId = operation.PhotographyUploadOperationId;
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.PhotographyUploadOperations.SingleAsync(operation => operation.PhotographyUploadOperationId == operationId);
        var second = await secondContext.PhotographyUploadOperations.SingleAsync(operation => operation.PhotographyUploadOperationId == operationId);

        second.MarkSeen();
        await secondContext.SaveChangesAsync();

        first.MarkSeen();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firstContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Artifact_photography_state_rejects_stale_competing_primary_write()
    {
        Guid artifactId;
        Guid firstImageId;
        Guid secondImageId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "CP");
            var set = await PhotographyPersistenceTestData.SeedSetAsync(seed, artifact.ArtifactId);
            var firstImage = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifact.ArtifactId, set.PhotographySetId, "original/primary-concurrency-1");
            var secondImage = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifact.ArtifactId, set.PhotographySetId, "original/primary-concurrency-2");
            seed.ArtifactPhotographyStates.Add(ArtifactPhotographyState.Create(artifact.ArtifactId));
            await seed.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            firstImageId = firstImage.ArtifactImageId;
            secondImageId = secondImage.ArtifactImageId;
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);
        var second = await secondContext.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);

        second.SetPrimaryImage(secondImageId, "second-manager");
        await secondContext.SaveChangesAsync();

        first.SetPrimaryImage(firstImageId, "first-manager");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firstContext.SaveChangesAsync());
    }
}

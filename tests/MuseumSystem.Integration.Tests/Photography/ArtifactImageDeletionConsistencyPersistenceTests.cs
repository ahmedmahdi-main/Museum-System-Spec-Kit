using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class ArtifactImageDeletionConsistencyPersistenceTests(PostgresPhotographyTestFixture fixture)
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 8, 25, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Audit_write_failure_during_finalization_rolls_back_the_deleted_transition_in_postgresql()
    {
        Guid imageId;
        int pendingToken;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "FP");
            var set = await PhotographyPersistenceTestData.SeedSetAsync(seed, artifact.ArtifactId);
            var image = await PhotographyPersistenceTestData.SeedImageAsync(
                seed, artifact.ArtifactId, set.PhotographySetId, $"original/finalization-rollback-{Guid.NewGuid():N}");
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
            pendingToken = image.ConcurrencyToken;
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
        }

        await using (var finalize = fixture.CreateContext())
        {
            var service = new ArtifactImageDeletionFinalizationService(finalize, new ThrowingAuditWriter());

            var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
                imageId, ArtifactImageDeletionMode.UploaderGracePeriod, pendingToken));

            Assert.Equal(ArtifactImageDeletionFinalizationOutcome.FinalizationPending, result.Outcome);
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.Equal(ArtifactImageStatus.DeletePending, reloaded.Status);
        Assert.Null(reloaded.DeletedByUserId);
        Assert.Null(reloaded.DeletedAt);
        Assert.Equal(pendingToken, reloaded.ConcurrencyToken);
        Assert.Equal(0, await reload.AuditEntries.CountAsync(entry => entry.EntityId == imageId.ToString()));
    }
}

internal sealed class ThrowingAuditWriter : IAuditWriter
{
    public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("Simulated audit write failure.");
}

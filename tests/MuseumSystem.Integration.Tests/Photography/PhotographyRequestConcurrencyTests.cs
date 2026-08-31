using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyRequestConcurrencyTests(PostgresPhotographyTestFixture fixture)
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = new(2026, 8, 24, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CancelledAt = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Complete_wins_and_stale_cancel_loses_with_db_concurrency_failure()
    {
        var (requestId, artifactId, setId) = await SeedPendingRequestWithValidFulfillmentAsync("QA");

        await using var completeContext = fixture.CreateContext();
        await using var cancelContext = fixture.CreateContext();
        var completingRequest = await completeContext.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == requestId);
        var cancellingRequest = await cancelContext.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == requestId);

        completingRequest.Complete(
            setId,
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            fulfillingSetHasAvailableImage: true,
            "photographer-1",
            CompletedAt);
        await completeContext.SaveChangesAsync();

        cancellingRequest.Cancel("requester-1", CancelledAt, actorHasManageAuthority: false);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => cancelContext.SaveChangesAsync());

        await using var verifyContext = fixture.CreateContext();
        var reloaded = await verifyContext.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == requestId);
        Assert.Equal(PhotographyRequestStatus.Completed, reloaded.Status);
        Assert.Equal(setId, reloaded.FulfillingPhotographySetId);
        Assert.Equal("photographer-1", reloaded.CompletedByUserId);
        Assert.Equal(CompletedAt, reloaded.CompletedAt);
        Assert.Null(reloaded.CancelledByUserId);
        Assert.Null(reloaded.CancelledAt);
        Assert.Equal(1, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task Cancel_wins_and_stale_complete_loses_with_db_concurrency_failure()
    {
        var (requestId, artifactId, setId) = await SeedPendingRequestWithValidFulfillmentAsync("QB");

        await using var cancelContext = fixture.CreateContext();
        await using var completeContext = fixture.CreateContext();
        var cancellingRequest = await cancelContext.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == requestId);
        var completingRequest = await completeContext.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == requestId);

        cancellingRequest.Cancel("manager-1", CancelledAt, actorHasManageAuthority: true);
        await cancelContext.SaveChangesAsync();

        completingRequest.Complete(
            setId,
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            fulfillingSetHasAvailableImage: true,
            "photographer-1",
            CompletedAt);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => completeContext.SaveChangesAsync());

        await using var verifyContext = fixture.CreateContext();
        var reloaded = await verifyContext.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == requestId);
        Assert.Equal(PhotographyRequestStatus.Cancelled, reloaded.Status);
        Assert.Equal("manager-1", reloaded.CancelledByUserId);
        Assert.Equal(CancelledAt, reloaded.CancelledAt);
        Assert.Null(reloaded.FulfillingPhotographySetId);
        Assert.Null(reloaded.CompletedByUserId);
        Assert.Null(reloaded.CompletedAt);
        Assert.Equal(1, reloaded.ConcurrencyToken);
    }

    private async Task<(Guid RequestId, Guid ArtifactId, Guid SetId)> SeedPendingRequestWithValidFulfillmentAsync(string prefix)
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, prefix);
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);
        await PhotographyPersistenceTestData.SeedImageAsync(context, artifact.ArtifactId, set.PhotographySetId, $"original/request-race-{prefix}-{Guid.NewGuid():N}");
        var request = PhotographyRequest.Create(
            artifact.ArtifactId,
            set.Purpose,
            "requester-1",
            RequestedAt);
        context.PhotographyRequests.Add(request);
        await context.SaveChangesAsync();

        return (request.PhotographyRequestId, artifact.ArtifactId, set.PhotographySetId);
    }
}

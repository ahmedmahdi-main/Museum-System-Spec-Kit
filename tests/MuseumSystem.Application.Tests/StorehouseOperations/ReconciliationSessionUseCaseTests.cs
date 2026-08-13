using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.StorehouseOperations;

public sealed class ReconciliationSessionUseCaseTests
{
    [Fact]
    public async Task Reconciliation_session_lifecycle_records_results_without_changing_artifacts()
    {
        await using var db = CreateDbContext();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var shelfA = Location.Create("Shelf A", LocationType.Storage);
        var shelfB = Location.Create("Shelf B", LocationType.Storage);
        var expected = Artifact.Create(category, 1, "Expected", shelfA);
        var conflict = Artifact.Create(category, 2, "Conflict", shelfB);
        db.ArtifactCategories.Add(category);
        db.Locations.AddRange(shelfA, shelfB);
        db.Artifacts.AddRange(expected, conflict);
        await db.SaveChangesAsync();

        var started = await new StartReconciliationSessionUseCase(db).StartReconciliationSession(new StartReconciliationSessionRequest(shelfA.LocationId));
        var recorded = await new RecordReconciliationItemsUseCase(db).RecordReconciliationItems(new RecordReconciliationItemsRequest(started.Value!.ReconciliationSessionId, [expected.MuseumNumberDisplay, conflict.MuseumNumberDisplay]));
        var reviewed = await new ReviewReconciliationResultsUseCase(db).ReviewReconciliationResults(started.Value.ReconciliationSessionId);

        Assert.True(started.Succeeded);
        Assert.True(recorded.Succeeded);
        Assert.Contains(recorded.Value!.Results, r => r.ResultType == ReconciliationResultType.Matched && r.ArtifactId == expected.ArtifactId);
        Assert.Contains(recorded.Value.Results, r => r.ResultType == ReconciliationResultType.Conflict && r.ArtifactId == conflict.ArtifactId);
        Assert.Equal(shelfB.LocationId, conflict.CurrentLocationId);
        Assert.True(reviewed.Succeeded);
        Assert.All(reviewed.Value!.Results.Where(r => r.ResultType == ReconciliationResultType.Conflict), r => Assert.True(r.IsConfirmed));
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

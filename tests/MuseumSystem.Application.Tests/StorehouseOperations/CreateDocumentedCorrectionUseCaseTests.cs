using MuseumSystem.Application.Common.Audit;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.StorehouseOperations;

public sealed class CreateDocumentedCorrectionUseCaseTests
{
    [Fact]
    public async Task Correction_does_not_substitute_for_return_when_artifact_is_out_of_storage()
    {
        await using var db = CreateDbContext();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Artifact", storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        var result = ReconciliationResult.Create(Guid.NewGuid(), artifact.ArtifactId, artifact.MuseumNumberDisplay, null, storage.LocationId, ReconciliationResultType.Conflict, "Observed in storage");
        result.ConfirmConflict();
        db.ArtifactCategories.Add(category);
        db.Locations.Add(storage);
        db.Artifacts.Add(artifact);
        db.ReconciliationResults.Add(result);
        await db.SaveChangesAsync();

        var correction = await new CreateDocumentedCorrectionUseCase(db, new FakeAuditWriter()).CreateDocumentedCorrection(new CreateDocumentedCorrectionRequest(
            result.ReconciliationResultId,
            DocumentedCorrectionType.LocationCorrection,
            storage.LocationId,
            null,
            null,
            "Confirmed conflict"));

        Assert.False(correction.Succeeded);
        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Null(artifact.CurrentLocationId);
        Assert.Empty(db.DocumentedCorrections);
        Assert.Empty(db.MovementRecords);
    }

    [Fact]
    public async Task Confirmed_location_correction_updates_current_state_without_rewriting_movement_history()
    {
        await using var db = CreateDbContext();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var shelfA = Location.Create("Shelf A", LocationType.Storage);
        var shelfB = Location.Create("Shelf B", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Artifact", shelfA);
        var result = ReconciliationResult.Create(Guid.NewGuid(), artifact.ArtifactId, artifact.MuseumNumberDisplay, shelfA.LocationId, shelfB.LocationId, ReconciliationResultType.Conflict, "Observed in another shelf");
        result.ConfirmConflict();
        db.ArtifactCategories.Add(category);
        db.Locations.AddRange(shelfA, shelfB);
        db.Artifacts.Add(artifact);
        db.ReconciliationResults.Add(result);
        await db.SaveChangesAsync();

        var correction = await new CreateDocumentedCorrectionUseCase(db, new FakeAuditWriter()).CreateDocumentedCorrection(new CreateDocumentedCorrectionRequest(
            result.ReconciliationResultId,
            DocumentedCorrectionType.LocationCorrection,
            shelfB.LocationId,
            null,
            null,
            "Confirmed by inventory officer"));

        Assert.True(correction.Succeeded);
        Assert.Equal(shelfB.LocationId, artifact.CurrentLocationId);
        Assert.Equal(shelfB.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Single(db.DocumentedCorrections);
        Assert.Empty(db.MovementRecords);
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default) => Task.FromResult("audit-1");
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

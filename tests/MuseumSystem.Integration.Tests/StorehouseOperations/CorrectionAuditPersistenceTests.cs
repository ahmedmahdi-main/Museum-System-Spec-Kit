using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.StorehouseOperations;

public sealed class CorrectionAuditPersistenceTests
{
    [Fact]
    public async Task Correction_and_audit_are_persisted_and_movement_history_remains()
    {
        await using var database = await TestDatabase.CreateAsync();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var shelfA = Location.Create("Shelf A", LocationType.Storage);
        var shelfB = Location.Create("Shelf B", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Artifact", shelfA);
        var session = ReconciliationSession.Start(shelfB);
        var result = ReconciliationResult.Create(session.ReconciliationSessionId, artifact.ArtifactId, artifact.MuseumNumberDisplay, shelfA.LocationId, shelfB.LocationId, ReconciliationResultType.Conflict, "Observed in another shelf");
        result.ConfirmConflict();
        var movement = MovementRecord.CreateDelivery(Guid.NewGuid(), artifact, MovementRecipientType.DocumentationDivision, "Documentation", "Review");
        database.Context.ArtifactCategories.Add(category);
        database.Context.Locations.AddRange(shelfA, shelfB);
        database.Context.Artifacts.Add(artifact);
        database.Context.ReconciliationSessions.Add(session);
        database.Context.ReconciliationResults.Add(result);
        database.Context.MovementRecords.Add(movement);
        await database.Context.SaveChangesAsync();

        var useCase = new CreateDocumentedCorrectionUseCase(database.Context, new AuditWriter(database.Context, new SystemAuditActorContext()));
        var correction = await useCase.CreateDocumentedCorrection(new CreateDocumentedCorrectionRequest(
            result.ReconciliationResultId,
            DocumentedCorrectionType.LocationCorrection,
            shelfB.LocationId,
            null,
            null,
            "Confirmed by inventory"));

        Assert.True(correction.Succeeded);
        Assert.Equal(shelfB.LocationId, artifact.CurrentLocationId);
        Assert.Equal(1, await database.Context.DocumentedCorrections.CountAsync());
        Assert.Equal(1, await database.Context.AuditEntries.CountAsync());
        Assert.Equal(1, await database.Context.MovementRecords.CountAsync());
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, MuseumDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public MuseumDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MuseumDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new MuseumDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

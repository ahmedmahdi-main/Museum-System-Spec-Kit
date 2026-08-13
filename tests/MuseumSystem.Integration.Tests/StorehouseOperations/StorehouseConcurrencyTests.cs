using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.StorehouseOperations;

public sealed class StorehouseConcurrencyTests
{
    [Fact]
    public async Task Artifact_state_changes_use_optimistic_concurrency()
    {
        await using var database = await TestDatabase.CreateAsync();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Artifact", storage);
        database.SeedContext.ArtifactCategories.Add(category);
        database.SeedContext.Locations.Add(storage);
        database.SeedContext.Artifacts.Add(artifact);
        await database.SeedContext.SaveChangesAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstCopy = await firstContext.Artifacts.SingleAsync(a => a.ArtifactId == artifact.ArtifactId);
        var secondCopy = await secondContext.Artifacts.SingleAsync(a => a.ArtifactId == artifact.ArtifactId);

        firstCopy.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Documentation");
        await firstContext.SaveChangesAsync();

        secondCopy.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<MuseumDbContext> _options;

        private TestDatabase(SqliteConnection connection, DbContextOptions<MuseumDbContext> options, MuseumDbContext seedContext)
        {
            _connection = connection;
            _options = options;
            SeedContext = seedContext;
        }

        public MuseumDbContext SeedContext { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MuseumDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new MuseumDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, options, context);
        }

        public MuseumDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            await SeedContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

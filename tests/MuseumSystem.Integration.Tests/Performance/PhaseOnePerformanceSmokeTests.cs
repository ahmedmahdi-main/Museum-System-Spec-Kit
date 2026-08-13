using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.ArtifactRegistry;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Performance;

public sealed class PhaseOnePerformanceSmokeTests
{
    [Fact]
    public async Task Search_returns_results_within_smoke_threshold()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context;
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        db.ArtifactCategories.Add(category);
        db.Locations.Add(storage);
        for (var i = 1; i <= 250; i++)
        {
            db.Artifacts.Add(Artifact.Create(category, i, $"Searchable artifact {i}", storage));
        }
        await db.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        var results = await new ArtifactReadUseCases(db).SearchArtifacts("ARC");
        sw.Stop();

        Assert.True(results.Count >= 250);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Search smoke test took {sw.Elapsed}.");
    }

    [Fact]
    public async Task Bulk_delivery_and_return_complete_within_smoke_threshold()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context;
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var returnStorage = Location.Create("Shelf B", LocationType.Storage);
        db.ArtifactCategories.Add(category);
        db.Locations.AddRange(storage, returnStorage);
        var artifacts = Enumerable.Range(1, 40).Select(i => Artifact.Create(category, i, $"Bulk artifact {i}", storage)).ToList();
        db.Artifacts.AddRange(artifacts);
        await db.SaveChangesAsync();
        var ids = artifacts.Select(a => a.ArtifactId).ToList();

        var sw = Stopwatch.StartNew();
        var delivery = await new DeliverArtifactsUseCase(db).DeliverArtifacts(new DeliverArtifactsRequest(ids, MovementRecipientType.DocumentationDivision, "Documentation", null, "Smoke test"));
        var returned = await new ReturnArtifactsUseCase(db).ReturnArtifacts(new ReturnArtifactsRequest(ids, returnStorage.LocationId));
        sw.Stop();

        Assert.True(delivery.Succeeded);
        Assert.True(returned.Succeeded);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Bulk movement smoke test took {sw.Elapsed}.");
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

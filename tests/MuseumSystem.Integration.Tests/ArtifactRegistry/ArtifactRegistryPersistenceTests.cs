using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.ArtifactRegistry;

public sealed class ArtifactRegistryPersistenceTests
{
    [Fact]
    public async Task Category_code_is_unique_in_database()
    {
        await using var database = await TestDatabase.CreateAsync();
        database.Context.ArtifactCategories.Add(ArtifactCategory.Create("A", "فخار"));
        database.Context.ArtifactCategories.Add(ArtifactCategory.Create("A", "فخار آخر"));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Item_number_is_unique_inside_category_in_database()
    {
        await using var database = await TestDatabase.CreateAsync();
        var category = ArtifactCategory.Create("A", "فخار");
        var location = Location.Create("رف 1", LocationType.Storage);
        database.Context.ArtifactCategories.Add(category);
        database.Context.Locations.Add(location);
        database.Context.Artifacts.Add(Artifact.Create(category, 1, "قطعة أولى", location));
        database.Context.Artifacts.Add(Artifact.Create(category, 1, "قطعة ثانية", location));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
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

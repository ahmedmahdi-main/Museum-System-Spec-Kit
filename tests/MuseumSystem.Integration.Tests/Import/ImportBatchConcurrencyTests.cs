using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Import;

public sealed class ImportBatchConcurrencyTests
{
    [Fact]
    public async Task Import_batch_uses_optimistic_concurrency_token()
    {
        await using var database = await TestDatabase.CreateAsync();
        var batch = ImportBatch.Create("import.xlsx");
        database.SeedContext.ImportBatches.Add(batch);
        await database.SeedContext.SaveChangesAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstCopy = await firstContext.ImportBatches.SingleAsync(b => b.ImportBatchId == batch.ImportBatchId);
        var secondCopy = await secondContext.ImportBatches.SingleAsync(b => b.ImportBatchId == batch.ImportBatchId);

        firstCopy.Cancel();
        await firstContext.SaveChangesAsync();

        secondCopy.Cancel();
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

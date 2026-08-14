using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.ArtifactRegistry;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Audit;

public sealed class SensitiveWriteAuditPersistenceTests
{
    [Fact]
    public async Task Sensitive_write_use_cases_persist_audit_entries_with_actor_and_change_summary()
    {
        await using var database = await TestDatabase.CreateAsync();
        var auditWriter = new AuditWriter(database.Context, new TestAuditActorContext("user-1"));
        var categoryUseCases = new CategoryUseCases(database.Context, auditWriter);
        var locationUseCases = new LocationUseCases(database.Context, auditWriter);
        var artifactWriteUseCases = new ArtifactWriteUseCases(database.Context, auditWriter);
        var deliverUseCase = new DeliverArtifactsUseCase(database.Context, auditWriter);
        var returnUseCase = new ReturnArtifactsUseCase(database.Context, auditWriter);
        var commitUseCase = new CommitImportBatchUseCase(database.Context, auditWriter);

        var retiredCategory = await categoryUseCases.CreateCategory(new CreateCategoryRequest("OLD", "Retired", null));
        await categoryUseCases.UpdateCategory(new UpdateCategoryRequest(retiredCategory.Value!.CategoryId, "OLD2", "Retired Updated", null));
        await categoryUseCases.DisableCategoryForNewUse(retiredCategory.Value.CategoryId);

        var activeCategory = ArtifactCategory.Create("ARC", "Archive");
        database.Context.ArtifactCategories.Add(activeCategory);
        await database.Context.SaveChangesAsync();

        var storage = await locationUseCases.CreateLocation(new CreateLocationRequest("Shelf A", LocationType.Storage));
        await locationUseCases.UpdateLocation(new UpdateLocationRequest(storage.Value!.LocationId, "Shelf A Updated", LocationType.Storage));
        var returnStorage = await locationUseCases.CreateLocation(new CreateLocationRequest("Shelf B", LocationType.Storage));

        var artifact = await artifactWriteUseCases.CreateArtifact(new CreateArtifactRequest(activeCategory.CategoryId, 1, "Artifact", storage.Value.LocationId));
        await artifactWriteUseCases.UpdateArtifactBasicInfo(new UpdateArtifactBasicInfoRequest(artifact.Value!.ArtifactId, "Updated artifact"));

        await deliverUseCase.DeliverArtifacts(new DeliverArtifactsRequest(
            [artifact.Value.ArtifactId],
            MovementRecipientType.DocumentationDivision,
            "Documentation",
            null,
            "Study"));
        await returnUseCase.ReturnArtifacts(new ReturnArtifactsRequest([artifact.Value.ArtifactId], returnStorage.Value!.LocationId));
        await locationUseCases.DisableLocationForNewUse(returnStorage.Value.LocationId);

        var batch = ImportBatch.Create("import.xlsx");
        batch.AddRow(ImportRow.Create(2, activeCategory.CategoryCode, "99", "Shelf A Updated", "Imported artifact"));
        database.Context.ImportBatches.Add(batch);
        await database.Context.SaveChangesAsync();
        var validation = await new ValidateImportBatchUseCase(database.Context).ValidateImportBatch(batch.ImportBatchId);
        var commit = await commitUseCase.CommitImportBatch(batch.ImportBatchId);

        Assert.True(validation.Succeeded);
        Assert.True(commit.Succeeded);

        var auditEntries = await database.Context.AuditEntries.ToListAsync();
        var actions = auditEntries.Select(entry => entry.ActionName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ArtifactCategory.Create", actions);
        Assert.Contains("ArtifactCategory.Update", actions);
        Assert.Contains("ArtifactCategory.DisableForNewUse", actions);
        Assert.Contains("Artifact.Create", actions);
        Assert.Contains("Artifact.UpdateBasicInfo", actions);
        Assert.Contains("Location.Create", actions);
        Assert.Contains("Location.Update", actions);
        Assert.Contains("Location.DisableForNewUse", actions);
        Assert.Contains("Movement.DeliverArtifacts", actions);
        Assert.Contains("Movement.ReturnArtifacts", actions);
        Assert.Contains("Import.CommitImportBatch", actions);
        Assert.All(auditEntries, entry =>
        {
            Assert.Equal("user-1", entry.ActorUserId);
            Assert.False(string.IsNullOrWhiteSpace(entry.EntityName));
            Assert.False(string.IsNullOrWhiteSpace(entry.EntityId));
            Assert.False(string.IsNullOrWhiteSpace(entry.Summary));
            Assert.True(entry.OccurredAt > DateTimeOffset.MinValue);
        });
        Assert.Contains(auditEntries, entry => entry.ChangeSummary is not null);
    }

    private sealed class TestAuditActorContext(string userId) : IAuditActorContext
    {
        public AuditActor CurrentActor => new(userId, "Test user", true);
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
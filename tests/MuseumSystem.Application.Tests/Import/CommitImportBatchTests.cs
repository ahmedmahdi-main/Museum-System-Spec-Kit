using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Import;

public sealed class CommitImportBatchTests
{
    [Fact]
    public async Task Commit_refuses_batch_before_validation()
    {
        await using var db = CreateDbContext();
        var batch = ImportBatch.Create("import.xlsx");
        batch.AddRow(ImportRow.Create(2, "ARC", "1", "Shelf A", "Artifact"));
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var result = await new CommitImportBatchUseCase(db).CommitImportBatch(batch.ImportBatchId);

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ImportBatch.NotReady");
    }

    [Fact]
    public async Task Commit_creates_accepted_artifacts_once_and_blocks_second_commit()
    {
        await using var db = CreateDbContext();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        db.ArtifactCategories.Add(category);
        db.Locations.Add(storage);
        var batch = ImportBatch.Create("import.xlsx");
        batch.AddRow(ImportRow.Create(2, "ARC", "1", "Shelf A", "Artifact"));
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var validation = await new ValidateImportBatchUseCase(db).ValidateImportBatch(batch.ImportBatchId);

        var firstCommit = await new CommitImportBatchUseCase(db).CommitImportBatch(batch.ImportBatchId);
        var secondCommit = await new CommitImportBatchUseCase(db).CommitImportBatch(batch.ImportBatchId);

        Assert.True(validation.Succeeded);
        Assert.True(firstCommit.Succeeded);
        Assert.Equal(1, await db.Artifacts.CountAsync());
        Assert.False(secondCommit.Succeeded);
        Assert.Contains(secondCommit.ValidationIssues, issue => issue.Code == "ImportBatch.AlreadyCommitted");
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

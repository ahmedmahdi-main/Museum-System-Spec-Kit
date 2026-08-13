using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Import;

public sealed class ValidateImportBatchTests
{
    [Fact]
    public async Task Validation_assigns_accepted_rejected_and_needs_review_statuses()
    {
        await using var db = CreateDbContext();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var inactiveCategory = ArtifactCategory.Create("OLD", "Old");
        inactiveCategory.DisableForNewUse();
        var storage = Location.Create("Shelf A", LocationType.Storage);
        db.ArtifactCategories.AddRange(category, inactiveCategory);
        db.Locations.Add(storage);
        db.Artifacts.Add(Artifact.Create(category, 99, "Existing", storage));
        var batch = ImportBatch.Create("import.xlsx");
        batch.AddRow(ImportRow.Create(2, "ARC", "1", "Shelf A", "Accepted"));
        batch.AddRow(ImportRow.Create(3, "ARC", "1", "Shelf A", "Duplicate in file"));
        batch.AddRow(ImportRow.Create(4, "ARC", "99", "Shelf A", "Duplicate in registry"));
        batch.AddRow(ImportRow.Create(5, "ARC", "2", "Unknown", "Unknown location"));
        batch.AddRow(ImportRow.Create(6, "OLD", "3", "Shelf A", "Needs review"));
        batch.AddRow(ImportRow.Create(7, null, null, null, null));
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var result = await new ValidateImportBatchUseCase(db).ValidateImportBatch(batch.ImportBatchId);

        Assert.True(result.Succeeded);
        Assert.Equal(ImportBatchStatus.ValidatedWithErrors, result.Value!.Status);
        Assert.Contains(result.Value.Rows, row => row.RowNumber == 2 && row.Status == ImportRowStatus.Accepted);
        Assert.Contains(result.Value.Rows, row => row.RowNumber == 3 && row.Status == ImportRowStatus.Rejected && row.Issues.Contains("مكرر"));
        Assert.Contains(result.Value.Rows, row => row.RowNumber == 4 && row.Status == ImportRowStatus.Rejected && row.Issues.Contains("مكرر"));
        Assert.Contains(result.Value.Rows, row => row.RowNumber == 5 && row.Status == ImportRowStatus.Rejected && row.Issues.Contains("غير معروف"));
        Assert.Contains(result.Value.Rows, row => row.RowNumber == 6 && row.Status == ImportRowStatus.NeedsReview);
        Assert.Contains(result.Value.Rows, row => row.RowNumber == 7 && row.Status == ImportRowStatus.Rejected && row.Issues.Contains("مطلوب"));
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

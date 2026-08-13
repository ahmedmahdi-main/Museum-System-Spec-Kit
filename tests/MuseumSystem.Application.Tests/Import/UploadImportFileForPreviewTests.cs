using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Application.Modules.Import.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Import;

public sealed class UploadImportFileForPreviewTests
{
    [Fact]
    public async Task Preview_saves_import_rows_without_mutating_artifacts_or_locations()
    {
        await using var db = CreateDbContext();
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        db.ArtifactCategories.Add(category);
        db.Locations.Add(storage);
        db.Artifacts.Add(Artifact.Create(category, 1, "Existing artifact", storage));
        await db.SaveChangesAsync();
        var artifactCount = await db.Artifacts.CountAsync();
        var locationCount = await db.Locations.CountAsync();
        var reader = new FakeExcelImportReader([
            new ImportSpreadsheetRow(2, "ARC", "2", "Shelf A", "Imported artifact")
        ]);

        var result = await new UploadImportFileForPreviewUseCase(db, reader)
            .UploadImportFileForPreview(new UploadImportFileForPreviewRequest("import.xlsx", new MemoryStream([1, 2, 3])));

        Assert.True(result.Succeeded);
        Assert.Equal(artifactCount, await db.Artifacts.CountAsync());
        Assert.Equal(locationCount, await db.Locations.CountAsync());
        Assert.Equal(1, await db.ImportBatches.CountAsync());
        Assert.Equal(1, await db.ImportRows.CountAsync());
    }

    private sealed class FakeExcelImportReader(IReadOnlyList<ImportSpreadsheetRow> rows) : IExcelImportReader
    {
        public Task<IReadOnlyList<ImportSpreadsheetRow>> ReadRowsAsync(Stream content, CancellationToken cancellationToken = default) => Task.FromResult(rows);
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

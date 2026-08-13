using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Import.Contracts;
using MuseumSystem.Domain.Modules.Import;

namespace MuseumSystem.Application.Modules.Import;

public sealed class UploadImportFileForPreviewUseCase(IMuseumDbContext dbContext, IExcelImportReader reader)
{
    public async Task<UseCaseResult<ImportBatchDto>> UploadImportFileForPreview(UploadImportFileForPreviewRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return UseCaseResult<ImportBatchDto>.Failure(new ValidationIssue("Import.FileType", "ارفع ملف Excel بصيغة .xlsx فقط.", nameof(request.FileName)));
        }

        var spreadsheetRows = await reader.ReadRowsAsync(request.Content, cancellationToken);
        var batch = ImportBatch.Create(request.FileName);
        foreach (var spreadsheetRow in spreadsheetRows)
        {
            batch.AddRow(ImportRow.Create(
                spreadsheetRow.RowNumber,
                spreadsheetRow.CategoryCode,
                spreadsheetRow.ItemNumber,
                spreadsheetRow.LocationName,
                spreadsheetRow.BasicDescription));
        }

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
        return UseCaseResult<ImportBatchDto>.Success(ImportDtoMapper.ToDto(batch), "تم رفع الملف للمعاينة.");
    }
}

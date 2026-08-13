using MuseumSystem.Application.Modules.Import.Contracts;
using MuseumSystem.Domain.Modules.Import;

namespace MuseumSystem.Application.Modules.Import;

internal static class ImportDtoMapper
{
    public static ImportBatchDto ToDto(ImportBatch batch) => new(
        batch.ImportBatchId,
        batch.FileName,
        batch.Status,
        batch.TotalRows,
        batch.AcceptedRows,
        batch.RejectedRows,
        batch.Rows.OrderBy(row => row.RowNumber).Select(ToDto).ToList());

    public static ImportRowDto ToDto(ImportRow row) => new(
        row.ImportRowId,
        row.RowNumber,
        row.CategoryValue,
        row.ItemNumberValue,
        row.LocationValue,
        row.DescriptionValue,
        row.Status,
        row.Issues,
        row.ProposedCategoryId,
        row.ProposedLocationId,
        row.ProposedArtifactId);
}

namespace MuseumSystem.Application.Modules.Import;

public sealed record ImportSpreadsheetRow(
    int RowNumber,
    string? CategoryCode,
    string? ItemNumber,
    string? LocationName,
    string? BasicDescription);

public interface IExcelImportReader
{
    Task<IReadOnlyList<ImportSpreadsheetRow>> ReadRowsAsync(Stream content, CancellationToken cancellationToken = default);
}

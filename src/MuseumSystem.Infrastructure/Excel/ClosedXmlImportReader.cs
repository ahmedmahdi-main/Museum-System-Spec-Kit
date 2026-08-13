using ClosedXML.Excel;
using MuseumSystem.Application.Modules.Import;

namespace MuseumSystem.Infrastructure.Excel;

public sealed class ClosedXmlImportReader : IExcelImportReader
{
    public Task<IReadOnlyList<ImportSpreadsheetRow>> ReadRowsAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(content);
        var worksheet = workbook.Worksheets.First();
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return Task.FromResult<IReadOnlyList<ImportSpreadsheetRow>>([]);
        }

        var headerRow = usedRange.FirstRow();
        var headerMap = headerRow.CellsUsed()
            .Select(cell => new { Header = NormalizeHeader(cell.GetString()), Column = cell.Address.ColumnNumber })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .GroupBy(item => item.Header)
            .ToDictionary(group => group.Key, group => group.First().Column);

        var rows = new List<ImportSpreadsheetRow>();
        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new ImportSpreadsheetRow(
                row.RowNumber(),
                GetCell(row, headerMap, "categorycode", "category", "رمزالفئة", "الفئة"),
                GetCell(row, headerMap, "itemnumber", "item", "رقمالقطعة", "الرقم"),
                GetCell(row, headerMap, "locationname", "location", "storage", "الموقع", "موقعالخزن"),
                GetCell(row, headerMap, "basicdescription", "description", "الوصف", "الوصفالاساسي"));

            if (HasAnyValue(item))
            {
                rows.Add(item);
            }
        }

        return Task.FromResult<IReadOnlyList<ImportSpreadsheetRow>>(rows);
    }

    private static string? GetCell(IXLRangeRow row, IReadOnlyDictionary<string, int> headerMap, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (headerMap.TryGetValue(NormalizeHeader(key), out var column))
            {
                var value = row.Cell(column).GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }

        return null;
    }

    private static bool HasAnyValue(ImportSpreadsheetRow row) =>
        !string.IsNullOrWhiteSpace(row.CategoryCode) ||
        !string.IsNullOrWhiteSpace(row.ItemNumber) ||
        !string.IsNullOrWhiteSpace(row.LocationName) ||
        !string.IsNullOrWhiteSpace(row.BasicDescription);

    private static string NormalizeHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

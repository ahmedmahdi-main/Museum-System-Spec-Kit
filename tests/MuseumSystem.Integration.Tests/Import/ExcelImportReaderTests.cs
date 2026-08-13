using ClosedXML.Excel;
using MuseumSystem.Infrastructure.Excel;

namespace MuseumSystem.Integration.Tests.Import;

public sealed class ExcelImportReaderTests
{
    [Fact]
    public async Task Closedxml_reader_parses_xlsx_rows_by_headers()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Artifacts");
            sheet.Cell(1, 1).Value = "CategoryCode";
            sheet.Cell(1, 2).Value = "ItemNumber";
            sheet.Cell(1, 3).Value = "LocationName";
            sheet.Cell(1, 4).Value = "BasicDescription";
            sheet.Cell(2, 1).Value = "ARC";
            sheet.Cell(2, 2).Value = "12";
            sheet.Cell(2, 3).Value = "Shelf A";
            sheet.Cell(2, 4).Value = "Imported artifact";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;

        var rows = await new ClosedXmlImportReader().ReadRowsAsync(stream);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("ARC", row.CategoryCode);
        Assert.Equal("12", row.ItemNumber);
        Assert.Equal("Shelf A", row.LocationName);
        Assert.Equal("Imported artifact", row.BasicDescription);
    }
}

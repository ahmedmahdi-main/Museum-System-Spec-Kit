namespace MuseumSystem.Web.AcceptanceTests.Import;

public sealed class ExcelImportFlowTests
{
    [Fact]
    public void Excel_import_page_exists()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Imports", "ExcelImport.razor");

        Assert.True(File.Exists(path), $"Expected page at {path}");
    }

    [Fact]
    public void Excel_import_page_shows_upload_preview_validation_commit_flow_and_row_errors()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Imports", "ExcelImport.razor"));

        Assert.Contains("رفع", page);
        Assert.Contains("معاينة", page);
        Assert.Contains("تحقق", page);
        Assert.Contains("اعتماد", page);
        Assert.Contains("أخطاء الصف", page);
        Assert.Contains("UploadImportFileForPreviewUseCase", page);
        Assert.Contains("CommitImportBatchUseCase", page);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

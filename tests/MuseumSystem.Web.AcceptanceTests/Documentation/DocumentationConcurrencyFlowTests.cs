namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationConcurrencyFlowTests
{
    [Fact]
    public void Documentation_pages_surface_concurrency_conflicts_as_reload_review_warnings()
    {
        var pages = DocumentationPagesRoot();
        var index = Read(pages, "Index.razor");
        var edit = Read(pages, "EditRecord.razor");
        var correction = Read(pages, "CorrectRecord.razor");
        var templateEditor = Read(pages, "TemplateVersionEditor.razor");
        var combined = string.Concat(index, edit, correction, templateEditor);

        Assert.True(CountOccurrences(combined, "ConcurrencyConflict") >= 4);
        Assert.True(CountOccurrences(combined, "warning-message") >= 4);
        Assert.True(CountOccurrences(combined, "تغيرت البيانات بواسطة مستخدم آخر") >= 4);
        Assert.True(CountOccurrences(combined, "إعادة تحميل أحدث نسخة") >= 4);
        Assert.True(CountOccurrences(combined, "راجع") >= 4);
    }

    [Fact]
    public void Reload_latest_affordances_call_current_page_reload_methods()
    {
        var pages = DocumentationPagesRoot();
        var index = Read(pages, "Index.razor");
        var edit = Read(pages, "EditRecord.razor");
        var correction = Read(pages, "CorrectRecord.razor");
        var templateEditor = Read(pages, "TemplateVersionEditor.razor");

        Assert.Contains("ReloadLatestWorkspaceAsync", index);
        Assert.Contains("await RefreshWorkspaceAsync(clearMessage: true);", index);
        Assert.Contains("ReloadLatestRecordAsync", edit);
        Assert.Contains("await ReloadAsync();", edit);
        Assert.Contains("ReloadLatestRecordAsync", correction);
        Assert.Contains("await OnParametersSetAsync();", correction);
        Assert.Contains("loadedRecordId != RecordId", correction);
        Assert.Contains("ReloadLatestVersionAsync", templateEditor);
        Assert.Contains("await ReloadAsync();", templateEditor);
    }

    [Fact]
    public void Concurrency_conflicts_are_not_treated_as_successful_saves()
    {
        var pages = DocumentationPagesRoot();
        var edit = Read(pages, "EditRecord.razor");
        var correction = Read(pages, "CorrectRecord.razor");
        var templateEditor = Read(pages, "TemplateVersionEditor.razor");
        var index = Read(pages, "Index.razor");

        Assert.Contains("isConflict = result.ConcurrencyConflict;", edit);
        Assert.Contains("if (result.Succeeded)", edit);
        Assert.Contains("isConflict = result.ConcurrencyConflict;", correction);
        Assert.Contains("if (result.Succeeded) await OnParametersSetAsync();", correction);
        Assert.Contains("isConflict = result.ConcurrencyConflict;", templateEditor);
        Assert.Contains("if (result.Succeeded)", templateEditor);
        Assert.Contains("var conflict = result.ConcurrencyConflict;", index);
        Assert.Contains("if (!result.Succeeded)", index);
    }

    private static DirectoryInfo DocumentationPagesRoot() =>
        new(Path.Combine(RepositoryRoot().FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation"));

    private static string Read(DirectoryInfo root, string fileName) =>
        File.ReadAllText(Path.Combine(root.FullName, fileName));

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static DirectoryInfo RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

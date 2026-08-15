using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationPrimaryFlowTests
{
    [Fact]
    public void Primary_documentation_pages_and_dynamic_form_are_present()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var componentPath = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Documentation", "DynamicDocumentationForm.razor");

        AssertPage(Path.Combine(pagesRoot, "Index.razor"), "@page \"/documentation\"", PermissionNames.DocumentationView);
        AssertPage(Path.Combine(pagesRoot, "EditRecord.razor"), "@page \"/documentation/records/{RecordId:guid}/edit\"", PermissionNames.DocumentationView);
        Assert.True(File.Exists(componentPath), $"{componentPath} should exist.");
    }

    [Fact]
    public void Primary_flow_sources_include_search_summary_create_save_resume_and_complete()
    {
        var root = FindRepositoryRoot();
        var pagesRoot = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var index = File.ReadAllText(Path.Combine(pagesRoot, "Index.razor"));
        var edit = File.ReadAllText(Path.Combine(pagesRoot, "EditRecord.razor"));
        var form = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Documentation", "DynamicDocumentationForm.razor"));
        var nav = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("SearchDocumentationArtifact", index);
        Assert.Contains("CreateDocumentationRecord", index);
        Assert.Contains("Resume Draft", index);
        Assert.Contains("DocumentationArtifactSummaryDto", index);
        Assert.Contains("SaveDocumentationDraft", edit);
        Assert.Contains("CompleteDocumentationRecord", edit);
        Assert.Contains("DynamicDocumentationForm", edit);
        Assert.Contains("DocumentationFieldType.Text", form);
        Assert.Contains("DocumentationFieldType.MultilineText", form);
        Assert.Contains("DocumentationFieldType.Number", form);
        Assert.Contains("DocumentationFieldType.Date", form);
        Assert.Contains("DocumentationFieldType.Boolean", form);
        Assert.Contains("DocumentationFieldType.SingleSelect", form);
        Assert.Contains("DocumentationFieldType.MultiSelect", form);
        Assert.Contains(nameof(PermissionNames.DocumentationView), nav);
    }


    [Fact]
    public void Dynamic_form_and_edit_page_wire_read_only_state_for_all_supported_field_types()
    {
        var root = FindRepositoryRoot();
        var form = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Documentation", "DynamicDocumentationForm.razor"));
        var edit = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation", "EditRecord.razor"));

        Assert.Contains("[Parameter] public bool IsReadOnly", form);
        Assert.True(CountOccurrences(form, "disabled=\"@IsReadOnly\"") >= 7);
        Assert.Contains("IsReadOnly=\"IsFormReadOnly\"", edit);
        Assert.Contains("record.Record.Status != DocumentationRecordStatus.Draft", edit);
        Assert.Contains("!record.Actions.CanSaveDraft", edit);
    }

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
    private static void AssertPage(string path, string route, string policy)
    {
        Assert.True(File.Exists(path), $"{path} should exist.");
        var text = File.ReadAllText(path);
        Assert.Contains(route, text);
        Assert.Contains($"Policy = PermissionNames.{FieldNameFor(policy)}", text);
    }

    private static string FieldNameFor(string permission) =>
        typeof(PermissionNames).GetFields().Single(field => (string)field.GetValue(null)! == permission).Name;

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationQuickstartTests
{
    [Fact]
    public void Template_setup_flow_has_pages_editor_seven_field_types_and_activation()
    {
        var root = RepositoryRoot();
        var pages = DocumentationPagesRoot(root);
        var templates = Read(pages, "Templates.razor");
        var editor = Read(pages, "TemplateVersionEditor.razor");

        Assert.Contains("@page \"/documentation/templates\"", templates);
        Assert.Contains("@page \"/documentation/templates/{VersionId:guid}/edit\"", editor);
        Assert.Contains("CreateDocumentationTemplateUseCase", templates);
        Assert.Contains("CreateTemplateVersionDraftUseCase", templates);
        Assert.Contains("SaveTemplateVersionDraftUseCase", editor);
        Assert.Contains("ActivateTemplateVersionUseCase", templates + editor);
        AssertSupportedFieldTypes(editor);
    }

    [Fact]
    public void Documentation_flow_has_museum_number_search_create_save_resume_and_complete_structure()
    {
        var pages = DocumentationPagesRoot(RepositoryRoot());
        var index = Read(pages, "Index.razor");
        var edit = Read(pages, "EditRecord.razor");

        Assert.Contains("museumNumber", index);
        Assert.Contains("SearchDocumentationArtifactUseCase", index);
        Assert.Contains("CreateDocumentationRecordUseCase", index);
        Assert.Contains("SaveDocumentationDraftUseCase", edit);
        Assert.Contains("CanResumeDraft", index);
        Assert.Contains("EditHref(workspace.ExistingRecord.DocumentationRecordId)", index);
        Assert.Contains("CompleteDocumentationRecordUseCase", edit);
        Assert.Contains("DynamicDocumentationForm", edit);
    }

    [Fact]
    public void Custody_is_informational_only_and_does_not_gate_create_save_complete_or_correction()
    {
        var root = RepositoryRoot();
        var application = DocumentationApplicationRoot(root);
        var pages = DocumentationPagesRoot(root);
        var create = Read(application, "CreateDocumentationRecordUseCase.cs");
        var save = Read(application, "SaveDocumentationDraftUseCase.cs");
        var complete = Read(application, "CompleteDocumentationRecordUseCase.cs");
        var correction = Read(application, "CorrectCompletedDocumentationUseCase.cs");
        var pageSources = string.Concat(Directory.GetFiles(pages.FullName, "*.razor").Select(File.ReadAllText));

        Assert.DoesNotContain("CustodyRequired", create);
        Assert.DoesNotContain("CustodyRequired", save);
        Assert.DoesNotContain("CustodyRequired", complete);
        Assert.DoesNotContain("DocumentationAvailabilityService", correction);
        Assert.DoesNotContain("DeliverArtifacts", pageSources);
        Assert.DoesNotContain("ReturnArtifacts", pageSources);
        Assert.DoesNotContain("إرجاع إلى المخزن", pageSources);
    }

    [Fact]
    public void Template_evolution_keeps_records_bound_to_template_version_and_supports_lifecycle()
    {
        var root = RepositoryRoot();
        var application = DocumentationApplicationRoot(root);
        var recordDtos = Read(application, "Contracts", "DocumentationRecordDtos.cs");
        var createRecord = Read(application, "CreateDocumentationRecordUseCase.cs");
        var templates = Read(DocumentationPagesRoot(root), "Templates.razor");
        var details = Read(DocumentationPagesRoot(root), "TemplateVersionDetails.razor");

        Assert.Contains("DocumentationTemplateVersionId", recordDtos + createRecord);
        Assert.Contains("resolution.Version.DocumentationTemplateVersionId", createRecord);
        Assert.Contains("RetireTemplateVersionUseCase", templates);
        Assert.Contains("ActivateTemplateVersionUseCase", templates);
        Assert.Contains("IsReadOnly", details);
        Assert.Contains("للقراءة فقط", details);
    }

    [Fact]
    public void Revision_history_routes_cover_correction_history_and_revision_details()
    {
        var pages = DocumentationPagesRoot(RepositoryRoot());
        var correction = Read(pages, "CorrectRecord.razor");
        var history = Read(pages, "History.razor");
        var details = Read(pages, "RevisionDetails.razor");
        var edit = Read(pages, "EditRecord.razor");

        Assert.Contains("@page \"/documentation/records/{RecordId:guid}/correct\"", correction);
        Assert.Contains("@page \"/documentation/records/{RecordId:guid}/history\"", history);
        Assert.Contains("@page \"/documentation/records/{RecordId:guid}/history/{RevisionNumber:int}\"", details);
        Assert.Contains("CorrectCompletedDocumentationUseCase", correction);
        Assert.Contains("GetDocumentationHistoryUseCase", history);
        Assert.Contains("GetDocumentationRevisionDetailsUseCase", details);
        Assert.Contains("/correct", edit);
        Assert.Contains("/history", edit);
    }

    [Fact]
    public void Stale_save_reload_review_affordances_exist()
    {
        var pages = DocumentationPagesRoot(RepositoryRoot());
        var combined = string.Concat(new[]
        {
            Read(pages, "Index.razor"),
            Read(pages, "EditRecord.razor"),
            Read(pages, "CorrectRecord.razor"),
            Read(pages, "TemplateVersionEditor.razor")
        });

        Assert.Contains("ConcurrencyConflict", combined);
        Assert.Contains("تغيرت البيانات بواسطة مستخدم آخر", combined);
        Assert.Contains("إعادة تحميل أحدث نسخة", combined);
    }

    [Fact]
    public void Authorization_relies_on_phase_e_permission_matrix()
    {
        var root = RepositoryRoot();
        var matrix = File.ReadAllText(Path.Combine(root.FullName, "tests", "MuseumSystem.Web.AcceptanceTests", "Security", "DocumentationPermissionMatrixTests.cs"));
        var nav = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor"));

        Assert.Contains("DocumentationPermissionMatrixTests", matrix);
        Assert.Contains("PermissionNames.DocumentationView", matrix + nav);
        Assert.Contains("PermissionNames.DocumentationTemplatesManage", matrix);
        Assert.Contains("AuthorizeRouteView", matrix);
    }

    private static void AssertSupportedFieldTypes(string source)
    {
        Assert.Contains("DocumentationFieldType.Text", source);
        Assert.Contains("DocumentationFieldType.MultilineText", source);
        Assert.Contains("DocumentationFieldType.Number", source);
        Assert.Contains("DocumentationFieldType.Date", source);
        Assert.Contains("DocumentationFieldType.Boolean", source);
        Assert.Contains("DocumentationFieldType.SingleSelect", source);
        Assert.Contains("DocumentationFieldType.MultiSelect", source);
    }

    private static DirectoryInfo DocumentationPagesRoot(DirectoryInfo root) =>
        new(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation"));

    private static DirectoryInfo DocumentationApplicationRoot(DirectoryInfo root) =>
        new(Path.Combine(root.FullName, "src", "MuseumSystem.Application", "Modules", "Documentation"));

    private static string Read(DirectoryInfo root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root.FullName, .. parts]));

    private static DirectoryInfo RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

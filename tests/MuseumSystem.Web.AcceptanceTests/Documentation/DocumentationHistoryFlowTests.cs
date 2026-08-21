using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationHistoryFlowTests
{
    [Fact]
    public void Correction_history_and_details_pages_use_expected_policies_use_cases_and_actions()
    {
        var pages = Path.Combine(FindRepositoryRoot().FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var correction = File.ReadAllText(Path.Combine(pages, "CorrectRecord.razor"));
        var history = File.ReadAllText(Path.Combine(pages, "History.razor"));
        var details = File.ReadAllText(Path.Combine(pages, "RevisionDetails.razor"));
        var edit = File.ReadAllText(Path.Combine(pages, "EditRecord.razor"));

        Assert.Contains("CorrectCompletedDocumentationUseCase", correction);
        Assert.Contains(nameof(PermissionNames.DocumentationView), correction);
        Assert.Contains(nameof(PermissionNames.DocumentationEdit), correction);
        Assert.Contains("سبب التصحيح", correction);
        Assert.Contains("maxlength=\"1000\"", correction);
        Assert.DoesNotContain("DocumentationAvailabilityService", correction);
        Assert.DoesNotContain("العهدة", correction);
        Assert.Contains("GetDocumentationHistoryUseCase", history);
        Assert.Contains(nameof(PermissionNames.DocumentationHistoryView), history);
        Assert.Contains("RevisionNumber", history);
        Assert.Contains("GetDocumentationRevisionDetailsUseCase", details);
        Assert.Contains(nameof(PermissionNames.DocumentationHistoryView), details);
        Assert.Contains("PreviousValue", details);
        Assert.Contains("NewValue", details);
        Assert.Contains("/correct", edit);
        Assert.Contains("/history", edit);
        Assert.DoesNotContain("Reopen", edit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Routed_pages_clear_previous_data_and_error_state_before_loading()
    {
        var pages = Path.Combine(FindRepositoryRoot().FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation");
        var correction = File.ReadAllText(Path.Combine(pages, "CorrectRecord.razor"));
        var history = File.ReadAllText(Path.Combine(pages, "History.razor"));
        var details = File.ReadAllText(Path.Combine(pages, "RevisionDetails.razor"));

        AssertResetBeforeLoad(correction, "record = null;", "GetRecordUseCase.GetDocumentationRecordForEdit");
        AssertResetBeforeLoad(correction, "values = [];", "GetRecordUseCase.GetDocumentationRecordForEdit");
        AssertResetBeforeLoad(correction, "message = null;", "GetRecordUseCase.GetDocumentationRecordForEdit");
        AssertResetBeforeLoad(correction, "isConflict = false;", "GetRecordUseCase.GetDocumentationRecordForEdit");
        Assert.Contains("loadedRecordId != RecordId", correction);
        AssertResetBeforeLoad(history, "items = [];", "HistoryUseCase.GetDocumentationHistory");
        AssertResetBeforeLoad(history, "message = null;", "HistoryUseCase.GetDocumentationHistory");
        AssertResetBeforeLoad(details, "details = null;", "DetailsUseCase.GetDocumentationRevisionDetails");
        AssertResetBeforeLoad(details, "message = null;", "DetailsUseCase.GetDocumentationRevisionDetails");
    }

    private static void AssertResetBeforeLoad(string source, string reset, string load)
    {
        var resetIndex = source.IndexOf(reset, StringComparison.Ordinal);
        var loadIndex = source.IndexOf(load, StringComparison.Ordinal);
        Assert.True(resetIndex >= 0, $"Expected reset '{reset}'.");
        Assert.True(loadIndex >= 0, $"Expected load '{load}'.");
        Assert.True(resetIndex < loadIndex, $"Expected '{reset}' before '{load}'.");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

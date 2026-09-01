using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyRequestFlowTests
{
    [Fact]
    public void Requests_page_is_arabic_rtl_authenticated_and_register_based()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Requests.razor");

        Assert.Contains("@page \"/photography/requests\"", page);
        Assert.Contains("@attribute [Authorize]", page);
        Assert.Contains("@rendermode InteractiveServer", page);
        Assert.Contains("<PageTitle>طلبات التصوير</PageTitle>", page);
        Assert.Contains("طلبات التصوير", page);
        Assert.Contains("سجل تشغيلي", page);
        Assert.Contains("register-toolbar", page);
        Assert.Contains("data-table", page);
        Assert.Contains("artifact-state", page);
        Assert.Contains("ref ref-lead", page);
        Assert.Contains("PhotographyRequestPanel", page);
        Assert.DoesNotContain("kanban", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dashboard", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<h1>Photography", page);
    }

    [Fact]
    public void Requests_page_creates_request_from_existing_artifact_without_client_actor_inputs()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Requests.razor");

        Assert.Contains("@inject ArtifactReadUseCases ArtifactReadUseCases", page);
        Assert.Contains("@inject CreatePhotographyRequestUseCase CreateUseCase", page);
        Assert.Contains("ArtifactReadUseCases.SearchArtifacts", page);
        Assert.Contains("SelectArtifact", page);
        Assert.Contains("selectedArtifact", page);
        Assert.Contains("CreatePhotographyRequestCommand", page);
        Assert.Contains("selectedArtifact!.ArtifactId", page);
        Assert.Contains("selectedPurpose", page);
        Assert.Contains("PhotographyPurpose.GeneralDocumentation", page);
        Assert.Contains("PhotographyPurpose.PreMaintenance", page);
        Assert.Contains("PhotographyPurpose.DuringMaintenance", page);
        Assert.Contains("PhotographyPurpose.PostMaintenance", page);
        Assert.Contains("LoadRequestsAsync", page);
        Assert.DoesNotContain("DateTimeOffset.UtcNow", page);
        Assert.DoesNotContain("clock.GetUtcNow", page);
    }

    [Fact]
    public void Requests_page_uses_only_existing_photography_permissions_for_capabilities()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Requests.razor");

        Assert.Contains(nameof(PermissionNames.PhotographyRequest), page);
        Assert.Contains(nameof(PermissionNames.PhotographyManage), page);
        Assert.Contains(nameof(PermissionNames.PhotographyUpload), page);
        Assert.DoesNotContain("MuseumRoleNames", page);
        Assert.DoesNotContain("Photography.Approve", page);
        Assert.DoesNotContain("Photography.Admin", page);
    }

    [Fact]
    public void Request_panel_cancels_pending_own_or_managed_request_with_authoritative_token_and_confirmation()
    {
        var root = FindRepositoryRoot();
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyRequestPanel.razor");

        Assert.Contains("@inject CancelPhotographyRequestUseCase CancelUseCase", panel);
        Assert.Contains("CanManage || IsOwnRequest", panel);
        Assert.Contains("Request.Request.Status == PhotographyRequestStatus.Pending", panel);
        Assert.Contains("confirmCancellation", panel);
        Assert.Contains("أؤكد إلغاء طلب التصوير نهائيا.", panel);
        Assert.Contains("CancelPhotographyRequestCommand", panel);
        Assert.Contains("Request.Request.PhotographyRequestId", panel);
        Assert.Contains("Request.Request.ConcurrencyToken", panel);
        Assert.Contains("CancelUseCase.CancelPhotographyRequest", panel);
        Assert.DoesNotContain("رمز المراجعة", panel);
        Assert.DoesNotContain("MuseumRoleNames", panel);
        Assert.DoesNotContain("ExpectedConcurrencyToken = 0", panel);
    }

    [Fact]
    public void Request_panel_completes_pending_request_with_upload_permission_and_eligible_set_selector()
    {
        var root = FindRepositoryRoot();
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyRequestPanel.razor");

        Assert.Contains("@inject CompletePhotographyRequestUseCase CompleteUseCase", panel);
        Assert.Contains("@inject PhotographyRequestQueries RequestQueries", panel);
        Assert.Contains("CanUpload", panel);
        Assert.Contains("PhotographyRequestStatus.Pending", panel);
        Assert.Contains("ListEligibleFulfillingSetsForRequest", panel);
        Assert.Contains("PhotographyRequestFulfillingSetSummaryDto", panel);
        Assert.Contains("<InputSelect @bind-Value=\"selectedFulfillingSetId\"", panel);
        Assert.Contains("CompletePhotographyRequestCommand", panel);
        Assert.Contains("selectedFulfillingSetId.Value", panel);
        Assert.Contains("Request.Request.ConcurrencyToken", panel);
        Assert.Contains("لا توجد مجموعة تصوير مؤهلة لإكمال هذا الطلب.", panel);
        Assert.Contains("href=\"/photography/upload\"", panel);
        Assert.DoesNotContain("<InputText @bind-Value=\"selectedFulfillingSetId\"", panel);
        Assert.DoesNotContain("FulfillingPhotographySetId\" class=\"form-control\"", panel);
    }

    [Fact]
    public void Request_workflow_does_not_leak_later_image_management_or_other_module_ownership()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Requests.razor");
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyRequestPanel.razor");
        var combined = page + panel;

        Assert.DoesNotContain("ViewArtifactImagesUseCase", combined);
        Assert.DoesNotContain("ImageStreamEndpoint", combined);
        Assert.DoesNotContain("PrimaryImage", combined);
        Assert.DoesNotContain("SetPrimary", combined);
        Assert.DoesNotContain("DeleteArtifactImage", combined);
        Assert.DoesNotContain("Minio", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DocumentationDivision", combined);
        Assert.DoesNotContain("ConservationLab", combined);
        Assert.DoesNotContain("MovementRecipientType", combined);
    }

    [Fact]
    public void Navigation_exposes_single_request_link_to_request_manage_or_upload_actors()
    {
        var root = FindRepositoryRoot();
        var nav = Read(root, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor");

        Assert.Contains(nameof(PermissionNames.PhotographyUpload), nav);
        Assert.Contains(nameof(PermissionNames.PhotographyRequest), nav);
        Assert.Contains(nameof(PermissionNames.PhotographyManage), nav);
        Assert.Contains("href=\"photography/upload\"", nav);
        Assert.Contains("رفع صور القطعة", nav);
        Assert.Contains("href=\"photography/requests\"", nav);
        Assert.Contains("طلبات التصوير", nav);
        Assert.Equal(1, CountOccurrences(nav, "href=\"photography/requests\""));
        Assert.DoesNotContain("photography/gallery", nav, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("photography/delete", nav, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Read(DirectoryInfo root, params string[] segments) =>
        File.ReadAllText(Path.Combine([root.FullName, .. segments]));

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

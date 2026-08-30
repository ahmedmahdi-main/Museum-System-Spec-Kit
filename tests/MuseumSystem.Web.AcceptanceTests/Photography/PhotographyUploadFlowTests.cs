using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyUploadFlowTests
{
    [Fact]
    public void Upload_page_is_arabic_rtl_and_authorized_for_photography_upload()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");

        Assert.Contains("@page \"/photography/upload\"", page);
        Assert.Contains($"Policy = PermissionNames.{nameof(PermissionNames.PhotographyUpload)}", page);
        Assert.Contains("@rendermode InteractiveServer", page);
        Assert.Contains("<PageTitle>رفع صور القطعة</PageTitle>", page);
        Assert.Contains("رفع صور القطعة", page);
        Assert.Contains("بحث القطعة", page);
        Assert.Contains("اختر القطعة", page);
        Assert.Contains("معلومات القطعة", page);
        Assert.Contains("الغرض", page);
        Assert.Contains("تاريخ التصوير", page);
        Assert.Contains("المصور", page);
        Assert.Contains("اختيار الصور", page);
        Assert.Contains("ابدأ الرفع", page);
        Assert.DoesNotContain("Upload Workflow", page);
        Assert.DoesNotContain("<h1>Photography", page);
    }

    [Fact]
    public void Upload_page_wires_artifact_search_selection_files_and_idempotent_submit()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");

        Assert.Contains("@inject ArtifactReadUseCases ArtifactReadUseCases", page);
        Assert.Contains("@inject CreatePhotographySetWithImagesUseCase CreateUseCase", page);
        Assert.Contains("ArtifactReadUseCases.SearchArtifacts", page);
        Assert.Contains("SelectArtifact", page);
        Assert.Contains("selectedArtifact", page);
        Assert.Contains("<InputFile", page);
        Assert.Contains("multiple", page);
        Assert.Contains("accept=\".jpg,.jpeg,.png,image/jpeg,image/png\"", page);
        Assert.Contains("PhotographyUploadFileInput", page);
        Assert.Contains("CreatePhotographySetWithImagesCommand", page);
        Assert.Contains("currentAttemptIdempotencyKey", page);
        Assert.Contains("isUploading", page);
        Assert.Contains("disabled=\"@(!CanUpload)\"", page);
        Assert.DoesNotContain("@currentAttemptIdempotencyKey", page);
        Assert.DoesNotContain("placeholder=\"", page.Substring(page.IndexOf("<InputFile", StringComparison.Ordinal)));
    }

    [Fact]
    public void Upload_page_handles_empty_invalid_partial_success_and_local_previews_without_storage_urls()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");

        Assert.Contains("لم تُحدّد أي صور بعد.", page);
        Assert.Contains("اختر قطعة أولاً ثم حدد صور JPEG أو PNG.", page);
        Assert.Contains("لا توجد صور صالحة للرفع.", page);
        Assert.Contains("بعض الملفات لم تُقبل", page);
        Assert.Contains("RequestImageFileAsync", page);
        Assert.Contains("data:image/jpeg;base64", page);
        Assert.Contains("localPreviewDataUrls", page);
        Assert.Contains("<PhotographyUploadResults", page);
        Assert.DoesNotContain("Minio", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BucketName", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presigned", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PhotographyRequest", page);
        Assert.DoesNotContain("SetPrimary", page);
        Assert.DoesNotContain("DeleteArtifactImage", page);
        Assert.DoesNotContain("MovementRecipientType", page);
    }

    [Fact]
    public void Upload_results_component_distinguishes_success_rejected_and_failed_with_staff_labels()
    {
        var root = FindRepositoryRoot();
        var component = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyUploadResults.razor");

        Assert.Contains("PhotographyUploadOperationResultDto", component);
        Assert.Contains("PhotographyUploadFileOutcomeStatus.Succeeded", component);
        Assert.Contains("PhotographyUploadFileOutcomeStatus.Rejected", component);
        Assert.Contains("PhotographyUploadFileOutcomeStatus.Failed", component);
        Assert.Contains("ناجح", component);
        Assert.Contains("مرفوض", component);
        Assert.Contains("فشل الرفع", component);
        Assert.Contains("badge-active", component);
        Assert.Contains("badge-draft", component);
        Assert.Contains("badge-retired", component);
        Assert.Contains("OriginalFilename", component);
        Assert.Contains("StaffFacingMessage", component);
        Assert.Contains("localPreviewDataUrls", component);
        Assert.DoesNotContain("Minio", component, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bucket", component, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", component, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", component, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Navigation_exposes_only_upload_entry_when_user_has_upload_permission()
    {
        var root = FindRepositoryRoot();
        var nav = Read(root, "src", "MuseumSystem.Web", "Components", "Layout", "NavMenu.razor");

        Assert.Contains($"Policy=\"@PermissionNames.{nameof(PermissionNames.PhotographyUpload)}\"", nav);
        Assert.Contains("href=\"photography/upload\"", nav);
        Assert.Contains("رفع صور القطع", nav);
        Assert.DoesNotContain("photography/requests", nav, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("photography/gallery", nav, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("photography/delete", nav, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Application_di_registers_only_upload_use_case_needed_by_page()
    {
        var root = FindRepositoryRoot();
        var dependencyInjection = Read(root, "src", "MuseumSystem.Application", "DependencyInjection.cs");

        Assert.Contains("services.AddScoped<CreatePhotographySetWithImagesUseCase>();", dependencyInjection);
        Assert.DoesNotContain("AddScoped<AppendImagesToPhotographySetUseCase>", dependencyInjection);
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

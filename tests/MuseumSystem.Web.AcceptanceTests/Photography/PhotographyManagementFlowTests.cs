using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyManagementFlowTests
{
    [Fact]
    public void Gallery_renders_management_panel_only_for_photography_manage_without_changing_view_authorization()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains($"Policy = PermissionNames.{nameof(PermissionNames.PhotographyView)}", page);
        Assert.Contains($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.PhotographyManage)}\">", page);
        Assert.Contains("<PhotographyImageManagementPanel ArtifactId=\"ArtifactId\"", page);
        Assert.Contains("SelectedImage=\"selectedImage\"", page);
        Assert.Contains("OnImageChanged=\"RefreshAfterManagementAsync\"", page);
        Assert.DoesNotContain("disabled=\"@(!", page);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyDelete), page);
        Assert.DoesNotContain("DeleteArtifactImage", page);
    }

    [Fact]
    public void Management_panel_calls_application_use_cases_with_expected_tokens_only()
    {
        var root = FindRepositoryRoot();
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyImageManagementPanel.razor");

        Assert.Contains("@inject UpdateArtifactImageMetadataUseCase MetadataUseCase", panel);
        Assert.Contains("@inject SetPrimaryArtifactImageUseCase PrimaryImageUseCase", panel);
        Assert.Contains("@inject ArtifactPhotographyStateService StateService", panel);
        Assert.Contains("StateService.GetSnapshot(ArtifactId)", panel);
        Assert.Contains("new UpdateArtifactImageMetadataCommand(", panel);
        Assert.Contains("SelectedImage.ArtifactImageId", panel);
        Assert.Contains("editableCaption", panel);
        Assert.Contains("SelectedImage.ConcurrencyToken", panel);
        Assert.Contains("new SetPrimaryArtifactImageCommand(", panel);
        Assert.Contains("ArtifactId", panel);
        Assert.Contains("primaryState.ConcurrencyToken", panel);
        Assert.DoesNotContain("UpdatedBy", panel);
        Assert.DoesNotContain("ChangedAt", panel);
        Assert.DoesNotContain("AuditTimestamp", panel);
        Assert.DoesNotContain("IArtifactImageStorage", panel);
        Assert.DoesNotContain("BucketName", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", panel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Management_panel_is_arabic_accessible_and_hides_concurrency_token_from_markup()
    {
        var root = FindRepositoryRoot();
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyImageManagementPanel.razor");

        Assert.Contains("إدارة بيانات الصورة المختارة", panel);
        Assert.Contains("وصف الصورة", panel);
        Assert.Contains("maxlength=\"1000\"", panel);
        Assert.Contains("حفظ الوصف", panel);
        Assert.Contains("تعيين كصورة رئيسية", panel);
        Assert.Contains("الصورة الرئيسية الحالية", panel);
        Assert.Contains("role=\"@messageRole\"", panel);
        Assert.Contains("aria-live=\"@messageLiveMode\"", panel);
        Assert.DoesNotContain("data-concurrency-token", panel);
        Assert.DoesNotContain("type=\"hidden\"", panel);
        Assert.DoesNotContain("رمز المراجعة", panel);
        Assert.DoesNotContain("<InputText @bind-Value=\"SelectedImage.ArtifactImageId\"", panel);
    }

    [Fact]
    public void Management_panel_surfaces_reload_review_conflicts_without_retrying_or_deleting()
    {
        var root = FindRepositoryRoot();
        var panel = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyImageManagementPanel.razor");

        Assert.Contains("result.ConcurrencyConflict", panel);
        Assert.Contains("تم تغيير وصف الصورة بواسطة مستخدم آخر. تم تحميل البيانات الأحدث؛ راجعها قبل المحاولة مرة أخرى.", panel);
        Assert.Contains("تم تغيير الصورة الرئيسية بواسطة مستخدم آخر. تم تحميل الحالة الأحدث؛ راجعها قبل المحاولة مرة أخرى.", panel);
        Assert.Contains("await LoadPrimaryStateAsync();", panel);
        Assert.Contains("await OnImageChanged.InvokeAsync(SelectedImage.ArtifactImageId);", panel);
        Assert.DoesNotContain("retry", panel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClearPrimary", panel);
        Assert.DoesNotContain("Delete", panel);
    }

    [Fact]
    public void Gallery_dto_exposes_internal_concurrency_token_for_management_without_storage_data()
    {
        var root = FindRepositoryRoot();
        var mapper = Read(root, "src", "MuseumSystem.Application", "Modules", "Photography", "PhotographyGalleryMapper.cs");

        Assert.Contains("int ConcurrencyToken", mapper);
        Assert.Contains("image.ConcurrencyToken", mapper);
        Assert.DoesNotContain("BucketName", mapper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", mapper, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presigned", mapper, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Artifact_search_gates_batch_primary_image_projection_by_photography_view()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Search.razor");

        Assert.Contains($"Policy = PermissionNames.{nameof(PermissionNames.ArtifactsView)}", page);
        Assert.Contains("@inject PrimaryImageProjectionQueries PrimaryImageProjectionQueries", page);
        Assert.Contains("@inject IAuthorizationService AuthorizationService", page);
        Assert.Contains("@inject AuthenticationStateProvider AuthenticationStateProvider", page);
        Assert.Contains("AuthorizationService.AuthorizeAsync(state.User, PermissionNames.PhotographyView)", page);
        Assert.Contains("if (!canViewPhotography || results.Count == 0)", page);
        Assert.Contains("primaryImagesByArtifactId.Clear();", page);
        Assert.Contains("GetPrimaryImagesForArtifacts(", page);
        Assert.Contains("new PrimaryImagesForArtifactsQuery(results.Select(artifact => artifact.ArtifactId).ToArray())", page);
    }

    [Fact]
    public void Artifact_search_primary_image_column_uses_safe_application_endpoint_and_arabic_empty_states()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Search.razor");

        Assert.Contains("@if (canViewPhotography)", page);
        Assert.Contains("<th>الصورة الرئيسية</th>", page);
        Assert.Contains("primaryImagesByArtifactId.TryGetValue", page);
        Assert.Contains("/photography/artifacts/@artifact.ArtifactId", page);
        Assert.Contains("/photography/images/{image.Thumbnail!.ArtifactImageId}/thumbnail", page);
        Assert.Contains("الصورة الرئيسية للقطعة", page);
        Assert.Contains("الصورة الرئيسية مسجلة دون معاينة متاحة.", page);
        Assert.Contains("لا توجد صورة رئيسية", page);
        Assert.Contains("تعذر تحميل الصورة الرئيسية.", page);
        Assert.DoesNotContain("IArtifactImageStorage", page);
        Assert.DoesNotContain("BucketName", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presigned", page, StringComparison.OrdinalIgnoreCase);
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

using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyDeletionFlowTests
{
    [Fact]
    public void Gallery_remains_view_authorized_and_hosts_deletion_outside_manage_gate()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        var deletionIndex = page.IndexOf("<PhotographyImageDeletionDialog SelectedImage=\"selectedImage\"", StringComparison.Ordinal);
        var manageGateIndex = page.IndexOf($"<AuthorizeView Policy=\"@PermissionNames.{nameof(PermissionNames.PhotographyManage)}\">", StringComparison.Ordinal);

        Assert.Contains("@page \"/photography/artifacts/{ArtifactId:guid}\"", page);
        Assert.Contains($"Policy = PermissionNames.{nameof(PermissionNames.PhotographyView)}", page);
        Assert.Contains("<PhotographyImageDeletionDialog SelectedImage=\"selectedImage\"", page);
        Assert.Contains("OnImageDeleted=\"RefreshAfterDeletionAsync\"", page);
        Assert.True(deletionIndex >= 0 && manageGateIndex >= 0 && deletionIndex < manageGateIndex,
            "Deletion must be available in the selected-image workflow before the Manage-only panel gate.");
    }

    [Fact]
    public void Deletion_component_uses_permission_policies_and_current_identity_without_role_checks()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("@inject IAuthorizationService AuthorizationService", component);
        Assert.Contains("@inject AuthenticationStateProvider AuthenticationStateProvider", component);
        Assert.Contains("ClaimTypes.NameIdentifier", component);
        Assert.Contains($"PermissionNames.{nameof(PermissionNames.PhotographyUpload)}", component);
        Assert.Contains($"PermissionNames.{nameof(PermissionNames.PhotographyDelete)}", component);
        Assert.Contains("CanShowGraceAction", component);
        Assert.Contains("SelectedImage.UploadedByUserId", component);
        Assert.DoesNotContain("IsInRole", component);
        Assert.DoesNotContain("MuseumRoleNames", component);
        Assert.DoesNotContain("RoleNames", component);
        Assert.DoesNotContain(nameof(PermissionNames.PhotographyManage), component);
    }

    [Fact]
    public void Grace_path_is_upload_and_uploader_match_only_without_client_clock_authority()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("canUpload", component);
        Assert.Contains("string.Equals(currentUserId.Trim(), SelectedImage.UploadedByUserId.Trim(), StringComparison.Ordinal)", component);
        Assert.Contains("حذف ضمن مهلة التصحيح", component);
        AssertNoClientClock(component);
        Assert.DoesNotContain("TotalMinutes", component);
        Assert.DoesNotContain("UploadedAt.Add", component);
    }

    [Fact]
    public void Privileged_path_is_delete_permission_only_and_reason_based()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("canDelete", component);
        Assert.Contains($"PermissionNames.{nameof(PermissionNames.PhotographyDelete)}", component);
        Assert.Contains("حذف بصلاحية الحذف", component);
        Assert.Contains("سبب الحذف", component);
        Assert.Contains("string.IsNullOrWhiteSpace(deletionReason)", component);
        Assert.Contains("maxlength=\"1000\"", component);
        Assert.Contains("required", component);
        Assert.DoesNotContain("canUpload && canDelete", component);
    }

    [Fact]
    public void Deletion_commands_are_constructed_from_selected_image_and_no_actor_or_time_input()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);
        var graceMethod = Slice(component, "private async Task ConfirmGraceDeletionAsync", "private async Task ConfirmPrivilegedDeletionAsync");
        var privilegedMethod = Slice(component, "private async Task ConfirmPrivilegedDeletionAsync", "private async Task HandleFailureAsync");

        Assert.Contains("new DeleteArtifactImageByUploaderGraceCommand(", graceMethod);
        Assert.Contains("SelectedImage.ArtifactImageId", graceMethod);
        Assert.Contains("SelectedImage.ConcurrencyToken", graceMethod);
        Assert.DoesNotContain("deletionReason", graceMethod);
        Assert.DoesNotContain("currentUserId", graceMethod);
        AssertNoClientClock(graceMethod);

        Assert.Contains("new DeleteArtifactImagePrivilegedCommand(", privilegedMethod);
        Assert.Contains("SelectedImage.ArtifactImageId", privilegedMethod);
        Assert.Contains("deletionReason", privilegedMethod);
        Assert.Contains("SelectedImage.ConcurrencyToken", privilegedMethod);
        Assert.DoesNotContain("currentUserId", privilegedMethod);
        AssertNoClientClock(privilegedMethod);
    }

    [Fact]
    public void Dialog_is_arabic_rtl_accessible_and_warns_permanent_non_reversible_deletion()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("حذف الصورة", component);
        Assert.Contains("تأكيد حذف الصورة", component);
        Assert.Contains("تأكيد الحذف المفوض", component);
        Assert.Contains("إلغاء", component);
        Assert.Contains("سبب الحذف", component);
        Assert.Contains("نهائياً", component);
        Assert.Contains("لا يمكن التراجع", component);
        Assert.Contains("role=\"dialog\"", component);
        Assert.Contains("aria-modal=\"true\"", component);
        Assert.Contains("aria-labelledby", component);
        Assert.Contains("aria-describedby", component);
        Assert.Contains("aria-live", component);
        Assert.DoesNotContain("<div @onclick", component);
    }

    [Fact]
    public void Reason_state_is_privileged_only_validated_and_cleared_on_cancel_success_or_image_switch()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);
        var graceMethod = Slice(component, "private async Task ConfirmGraceDeletionAsync", "private async Task ConfirmPrivilegedDeletionAsync");

        Assert.Contains("DeletionDialogMode.Privileged", component);
        Assert.Contains("DeletionDialogMode.UploaderGrace", component);
        Assert.Contains("deletionReason = null;", component);
        Assert.Contains("\"ArtifactImage.DeletionReasonRequired\" => \"سبب الحذف مطلوب.\"", component);
        Assert.Contains("\"ArtifactImage.DeletionReasonTooLong\" => \"سبب الحذف لا يجب أن يتجاوز 1000 حرف.\"", component);
        Assert.DoesNotContain("سبب الحذف", graceMethod);
    }

    [Fact]
    public void Unauthorized_view_manage_or_upload_only_users_do_not_get_the_wrong_controls()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("ShouldRenderDeletionPanel", component);
        Assert.Contains("CanShowGraceAction || canDelete", component);
        Assert.Contains("canUpload", component);
        Assert.Contains("canDelete", component);
        Assert.DoesNotContain("PhotographyManage", component);
        Assert.DoesNotContain("disabled=\"@(!canUpload", component);
        Assert.DoesNotContain("disabled=\"@(!canDelete", component);
    }

    [Fact]
    public void Gallery_refreshes_authoritative_data_after_success_without_auto_primary_replacement()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");
        var deletionHandler = Slice(page, "private async Task RefreshAfterDeletionAsync", "private async Task RefreshAfterDeletionStateChangeAsync");
        var refreshHandler = Slice(page, "private async Task RefreshAfterDeletionStateChangeAsync", "private Task ShowDeletionWarningAsync");

        Assert.Contains("await LoadGalleryAsync();", deletionHandler);
        Assert.Contains("SetDeletionStatus(\"تم حذف الصورة نهائياً.\")", deletionHandler);
        Assert.Contains("await LoadGalleryAsync();", refreshHandler);
        Assert.Contains("SelectPreferredImage(selectedImage?.ArtifactImageId)", page);
        Assert.Contains("ArtifactImageGalleryState.NoImages", page);
        Assert.DoesNotContain("SetPrimaryArtifactImageUseCase", deletionHandler + refreshHandler);
        Assert.DoesNotContain("SetPrimaryArtifactImageCommand", deletionHandler + refreshHandler);
        Assert.DoesNotContain(".Remove(", deletionHandler);
    }

    [Fact]
    public void Conflict_expired_permission_uploader_and_state_failures_have_staff_safe_arabic_mapping()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("result.ConcurrencyConflict", component);
        Assert.Contains("تم تغيير حالة الصورة بواسطة مستخدم آخر", component);
        Assert.Contains("\"ArtifactImage.GracePeriodExpired\" => \"انتهت مهلة التصحيح الخاصة بهذه الصورة.\"", component);
        Assert.Contains("\"ArtifactImage.UploaderMismatch\" => \"لا يمكن حذف صورة رفعها مستخدم آخر.\"", component);
        Assert.Contains("\"Photography.PermissionDenied\"", component);
        Assert.Contains("\"ArtifactImage.NotFound\" => \"لم تعد الصورة موجودة. تم تحديث البيانات المعروضة.\"", component);
        Assert.Contains("\"ArtifactImage.DeleteInvalidState\" => \"لا يمكن حذف الصورة في حالتها الحالية. تم تحديث البيانات المعروضة.\"", component);
        Assert.Contains("activeMode = null;", component);
        Assert.DoesNotContain("DeleteArtifactImagePrivileged", Slice(component, "ArtifactImage.GracePeriodExpired", "_ =>"));
    }

    [Fact]
    public void Recovery_and_finalization_pending_are_warnings_not_staff_recovery_ui()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);
        var combined = component
            + Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains("\"ArtifactImage.DeletionRecoveryRequired\"", component);
        Assert.Contains("\"ArtifactImage.DeletionFinalizationPending\"", component);
        Assert.Contains("ArtifactImage.DeletionRecoveryRequired", component);
        Assert.Contains("RequiresAuthoritativeRefresh", component);
        Assert.DoesNotContain("Photography.Recovery", combined);
        Assert.DoesNotContain("StorageOperationRecovery", combined);
        Assert.DoesNotContain("FailureSummary", combined);
        Assert.DoesNotContain("زر استرداد", combined);
    }

    [Fact]
    public void Deletion_web_code_does_not_expose_storage_internals_raw_tokens_or_uploader_ids()
    {
        var root = FindRepositoryRoot();
        var combined = DeletionComponent(root)
            + Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        AssertNoStorageInternals(combined);
        Assert.DoesNotContain("data-concurrency-token", combined);
        Assert.DoesNotContain("type=\"hidden\"", combined);
        Assert.DoesNotContain("رمز التزامن", combined);
        Assert.DoesNotContain("@SelectedImage.ConcurrencyToken", combined);
        Assert.DoesNotContain("@SelectedImage.UploadedByUserId", combined);
    }

    [Fact]
    public void Destructive_buttons_are_semantic_styled_and_busy_guarded()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);

        Assert.Contains("<button type=\"button\"", component);
        Assert.Contains("type=\"submit\" class=\"btn-danger compact\" disabled=\"@isDeleting\"", component);
        Assert.Contains("class=\"btn-danger compact\"", component);
        Assert.Contains("isDeleting", component);
        Assert.Contains("جار الحذف...", component);
        Assert.Contains("if (SelectedImage is null || activeMode != DeletionDialogMode.UploaderGrace || isDeleting)", component);
        Assert.Contains("if (SelectedImage is null || activeMode != DeletionDialogMode.Privileged || isDeleting)", component);
        Assert.DoesNotContain("@onclick=\"ConfirmPrivilegedDeletionAsync\"", component);
    }

    private static string DeletionComponent(DirectoryInfo root) =>
        Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyImageDeletionDialog.razor");

    private static void AssertNoClientClock(string source)
    {
        Assert.DoesNotContain("DateTimeOffset.Now", source);
        Assert.DoesNotContain("DateTimeOffset.UtcNow", source);
        Assert.DoesNotContain("DateTime.Now", source);
        Assert.DoesNotContain("DateTime.UtcNow", source);
    }

    private static void AssertNoStorageInternals(string source)
    {
        Assert.DoesNotContain("IArtifactImageStorage", source);
        Assert.DoesNotContain("Minio", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OriginalObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bucket", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Endpoint", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presigned", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider failure", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string Slice(string source, string startNeedle, string endNeedle)
    {
        var start = source.IndexOf(startNeedle, StringComparison.Ordinal);
        var end = source.IndexOf(endNeedle, start + startNeedle.Length, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Missing start marker {startNeedle}.");
        Assert.True(end > start, $"Missing end marker {endNeedle}.");

        return source[start..end];
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

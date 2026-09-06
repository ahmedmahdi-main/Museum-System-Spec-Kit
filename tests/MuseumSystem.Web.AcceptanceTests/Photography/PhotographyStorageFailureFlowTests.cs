namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyStorageFailureFlowTests
{
    [Fact]
    public void Upload_page_does_not_expose_storage_failure_internals()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");

        Assert.Contains("@inject CreatePhotographySetWithImagesUseCase CreateUseCase", page);
        Assert.Contains("var result = await CreateUseCase.CreatePhotographySetWithImages(command);", page);
        Assert.Contains("uploadResult = result.Value;", page);
        Assert.Contains("message = ResultMessage(result);", page);
        Assert.DoesNotContain("uploadResult = result;", page);
        AssertNoRawStorageInternals(page);
        AssertNoProviderExceptionHandling(page);
        Assert.DoesNotContain("StorageOperationRecovery", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Upload_results_render_only_staff_safe_failure_contract()
    {
        var root = FindRepositoryRoot();
        var component = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyUploadResults.razor");

        Assert.Contains("<td>@file.StaffFacingMessage</td>", component);
        Assert.Contains("رسالة للفريق", component);
        Assert.Contains("PhotographyUploadFileResultDto", Read(root, "src", "MuseumSystem.Application", "Modules", "Photography", "Contracts", "PhotographyDtos.cs"));
        Assert.DoesNotContain("@file.Code", component, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FailureCode", component, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file.Failure", component, StringComparison.OrdinalIgnoreCase);
        AssertNoRawStorageInternals(component);
        AssertNoProviderEndpointCoupling(component);
    }

    [Fact]
    public void Recovery_needed_upload_uses_controlled_staff_labels_without_recovery_actions()
    {
        var root = FindRepositoryRoot();
        var component = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyUploadResults.razor");
        var uploadPage = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");
        var combined = component + Environment.NewLine + uploadPage;

        Assert.Contains("PhotographyUploadOperationStatus.RecoveryNeeded => \"تحتاج هذه العملية إلى متابعة تخزين داخلية.\"", component);
        Assert.Contains("PhotographyUploadFileOutcomeStatus.RecoveryNeeded => \"يتطلب متابعة\"", component);
        Assert.DoesNotContain("StorageOperationRecoveryId", combined);
        Assert.DoesNotContain("StorageOperationRecoveryUseCase", combined);
        Assert.DoesNotContain("RecoveryRetry", combined);
        Assert.DoesNotContain("RetryStorageRecovery", combined);
        Assert.DoesNotContain("RetryAsync", combined);
        Assert.DoesNotContain("FailureSummary", combined);
        Assert.DoesNotContain("ObjectKey", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Upload_failure_flow_does_not_handle_provider_exceptions_or_operational_summaries()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor");
        var resultMessage = Slice(page, "private static string ResultMessage", "private static string FormatBytes");

        Assert.Contains("return rejectedOrFailed == 0", resultMessage);
        Assert.Contains("\"تم حفظ كل الصور المقبولة.\"", resultMessage);
        Assert.Contains("\"بعض الملفات لم تُقبل. تم حفظ", resultMessage);
        Assert.Contains("return string.Join(\" \", result.ValidationIssues.Select(issue => issue.Message));", resultMessage);
        Assert.DoesNotContain("OperationalSummary", resultMessage);
        Assert.DoesNotContain("FailureSummary", resultMessage);
        Assert.DoesNotContain("ArtifactImageStorageResultKind", page);
        Assert.DoesNotContain("RetryableFailure", page);
        Assert.DoesNotContain("UnauthorizedOrMisconfigured", page);
        Assert.DoesNotContain("exception.Message", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("catch (Exception", page);
        AssertNoProviderExceptionHandling(page);
    }

    [Fact]
    public void Deletion_recovery_states_use_controlled_arabic_messages_and_authoritative_refresh()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);
        var refreshCodes = Slice(component, "private static bool RequiresAuthoritativeRefresh", "private static string FailureMessage");
        var closeReload = Slice(component, "private async Task CloseReloadAndReportAsync", "private void CloseAfterSuccess");

        Assert.Contains("\"ArtifactImage.DeletionRecoveryRequired\"", refreshCodes);
        Assert.Contains("\"ArtifactImage.DeletionFinalizationPending\"", refreshCodes);
        Assert.Contains("\"ArtifactImage.DeletionRecoveryRequired\" => \"تعذر إكمال حذف الصورة نهائياً. تم حفظ العملية للمعالجة الداخلية دون عرض تفاصيل تقنية.\"", component);
        Assert.Contains("\"ArtifactImage.DeletionFinalizationPending\" => \"بدأت معالجة حذف الصورة لكن السجل النهائي لم يكتمل بعد. البيانات المعروضة محدثة.\"", component);
        Assert.Contains("activeMode = null;", closeReload);
        Assert.Contains("deletionReason = null;", closeReload);
        Assert.Contains("await OnImageDeletionRefreshRequired.InvokeAsync(imageId);", closeReload);
        Assert.Contains("await OnMessageRequested.InvokeAsync(value);", closeReload);
    }

    [Fact]
    public void Deletion_failure_flow_exposes_no_recovery_or_storage_internals()
    {
        var root = FindRepositoryRoot();
        var component = DeletionComponent(root);
        var gallery = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");
        var combined = component + Environment.NewLine + gallery;

        Assert.Contains("OnImageDeletionRefreshRequired", combined);
        Assert.Contains("ShowDeletionWarningAsync", gallery);
        AssertNoRawStorageInternals(combined);
        Assert.DoesNotContain("StorageOperationRecoveryId", combined);
        Assert.DoesNotContain("RecoveryRetry", combined);
        Assert.DoesNotContain("stack trace", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gallery_and_stream_failure_boundary_remains_opaque()
    {
        var root = FindRepositoryRoot();
        var gallery = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");
        var stream = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "ImageStreamEndpoint.cs");
        var combined = gallery + Environment.NewLine + stream;

        Assert.Contains("@inject ViewArtifactImagesUseCase ViewImagesUseCase", gallery);
        Assert.Contains("ViewImagesUseCase.ViewArtifactImages(new ViewArtifactImagesQuery(ArtifactId))", gallery);
        Assert.Contains("public const string Route = \"/photography/images/{artifactImageId:guid}/{rendition}\";", stream);
        Assert.Contains("ViewArtifactImagesUseCase useCase", stream);
        Assert.Contains("new ReadArtifactImageRenditionQuery(artifactImageId, requestedRendition)", stream);
        Assert.Contains("statusCode: StatusCodes.Status503ServiceUnavailable", stream);
        Assert.Contains("detail: \"The requested museum image is temporarily unavailable.\"", stream);
        Assert.Contains("ImageSource(PhotographyImageAccessReferenceDto access)", gallery);
        Assert.Contains("access.ArtifactImageId", gallery);
        Assert.Contains("access.Rendition", gallery);
        Assert.DoesNotContain("CreateShortLivedReadAccessAsync", combined);
        AssertNoRawStorageInternals(combined, allowEndpointName: true);
        AssertNoProviderEndpointCoupling(combined);
    }

    [Fact]
    public void Photography_staff_workflows_have_no_sixth_recovery_permission_or_retry_ui()
    {
        var root = FindRepositoryRoot();
        var combined = string.Join(Environment.NewLine, new[]
        {
            Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Upload.razor"),
            Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyUploadResults.razor"),
            DeletionComponent(root),
            Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor"),
            Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "ImageStreamEndpoint.cs")
        });

        Assert.DoesNotContain("Photography.Recovery", combined);
        Assert.DoesNotContain("Photography.StorageRecovery", combined);
        Assert.DoesNotContain("StorageRecoveryRetry", combined);
        Assert.DoesNotContain("RetryStorageRecovery", combined);
        Assert.DoesNotContain("StorageOperationRecoveryUseCase", combined);
        Assert.DoesNotContain("Task.Delay", combined);
        Assert.DoesNotContain(".DeleteObjectAsync", combined);
        Assert.DoesNotContain(".DeleteImageObjectsAsync", combined);
        Assert.DoesNotContain(".StatAsync", combined);
        Assert.DoesNotContain("RetryAsync", combined);
    }

    private static string DeletionComponent(DirectoryInfo root) =>
        Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyImageDeletionDialog.razor");

    private static void AssertNoRawStorageInternals(string source, bool allowEndpointName = false)
    {
        Assert.DoesNotContain("Minio", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BucketName", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OriginalObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DerivativeObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccessKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OperationalSummary", source);
        Assert.DoesNotContain("FailureSummary", source);
        Assert.DoesNotContain("IArtifactImageStorage", source);
        Assert.DoesNotContain("StorageOperationRecovery", source);
        Assert.DoesNotContain("exception.Message", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception.ToString", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", source, StringComparison.OrdinalIgnoreCase);
        if (!allowEndpointName)
        {
            Assert.DoesNotContain("Endpoint", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoProviderExceptionHandling(string source)
    {
        Assert.DoesNotContain("MinioException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BucketNotFoundException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthorizationException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessDeniedException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidEndpointException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidBucketNameException", source, StringComparison.Ordinal);
    }

    private static void AssertNoProviderEndpointCoupling(string source)
    {
        Assert.DoesNotContain("WithEndpoint", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Endpoint =", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Endpoint=", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
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

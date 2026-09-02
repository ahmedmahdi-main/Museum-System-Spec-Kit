using MuseumSystem.Application.Modules.IdentityAccess;

namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyGalleryFlowTests
{
    [Fact]
    public void Gallery_page_is_artifact_scoped_arabic_rtl_and_authorized_for_photography_view()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains("@page \"/photography/artifacts/{ArtifactId:guid}\"", page);
        Assert.Contains($"Policy = PermissionNames.{nameof(PermissionNames.PhotographyView)}", page);
        Assert.Contains("@rendermode InteractiveServer", page);
        Assert.Contains("<PageTitle>صور القطعة</PageTitle>", page);
        Assert.Contains("صور القطعة", page);
        Assert.Contains("[Parameter] public Guid ArtifactId { get; set; }", page);
        Assert.DoesNotContain("<h1>Photography", page);
        Assert.DoesNotContain("kanban", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("masonry", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gallery_page_shows_artifact_identity_and_operational_state_before_images()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        var stateIndex = page.IndexOf("class=\"artifact-state\"", StringComparison.Ordinal);
        var galleryLayoutIndex = page.IndexOf("photography-gallery-layout", StringComparison.Ordinal);

        Assert.Contains("gallery.Artifact.MuseumNumber", page);
        Assert.Contains("gallery.Artifact.CurrentStatus", page);
        Assert.Contains("gallery.Artifact.CurrentLocationName", page);
        Assert.Contains("artifact-state", page);
        Assert.True(stateIndex >= 0 && galleryLayoutIndex >= 0 && stateIndex < galleryLayoutIndex,
            "Artifact identity/state must render before the image layout.");
    }

    [Fact]
    public void Gallery_page_uses_view_use_case_and_application_endpoints_only()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains("@inject ViewArtifactImagesUseCase ViewImagesUseCase", page);
        Assert.Contains("ViewImagesUseCase.ViewArtifactImages(new ViewArtifactImagesQuery(ArtifactId))", page);
        Assert.Contains("/photography/images/{access.ArtifactImageId}/{access.Rendition.ToString().ToLowerInvariant()}", page);
        Assert.DoesNotContain("IArtifactImageStorage", page);
        Assert.DoesNotContain("Minio", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BucketName", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presigned", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gallery_page_distinguishes_no_images_from_unavailable_renditions_in_arabic()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains("ArtifactImageGalleryState.NoImages", page);
        Assert.Contains("لا توجد صور متاحة لهذه القطعة.", page);
        Assert.Contains("PhotographyImageRenditionAvailability.Available", page);
        Assert.Contains("الصورة غير متاحة مؤقتا.", page);
        Assert.Contains("المعاينة ليست متاحة الآن لهذه الصورة.", page);
        Assert.DoesNotContain("PhotographyImageRendition.Original", page);
        Assert.DoesNotContain("Rendition.Original", page);
    }

    [Fact]
    public void Gallery_page_supports_selecting_a_preview_as_temporary_ui_state_only()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains("private PhotographyGalleryImageDto? selectedImage;", page);
        Assert.Contains("private void SelectImage(PhotographyGalleryImageDto image) => selectedImage = image;", page);
        Assert.Contains("aria-pressed=\"@(selectedImage?.ArtifactImageId == image.ArtifactImageId)\"", page);
        Assert.DoesNotContain("PrimaryImageProjectionQueries", page);
    }

    [Fact]
    public void Gallery_toolbar_remains_view_only_with_no_management_controls()
    {
        var root = FindRepositoryRoot();
        var toolbar = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyGalleryToolbar.razor");

        Assert.Contains("TotalImageCount", toolbar);
        Assert.Contains("HasSelectedImage", toolbar);
        Assert.Contains("OnRefresh", toolbar);
        Assert.DoesNotContain("SetPrimary", toolbar);
        Assert.DoesNotContain("DeleteArtifactImage", toolbar);
        Assert.DoesNotContain("UpdateArtifactImageMetadata", toolbar);
        Assert.DoesNotContain("Upload", toolbar, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manage", toolbar, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Photography.Delete", toolbar);
        Assert.DoesNotContain("disabled=\"@(!", toolbar);
    }

    [Fact]
    public void Gallery_and_toolbar_stay_division_neutral_and_show_all_photography_purposes()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");
        var toolbar = Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyGalleryToolbar.razor");
        var combined = page + toolbar;

        Assert.Contains("PhotographyPurpose.GeneralDocumentation", page);
        Assert.Contains("PhotographyPurpose.PreMaintenance", page);
        Assert.Contains("PhotographyPurpose.DuringMaintenance", page);
        Assert.Contains("PhotographyPurpose.PostMaintenance", page);
        Assert.Contains("توثيق عام", page);
        Assert.DoesNotContain("تصوير عام", page);
        Assert.DoesNotContain("MuseumRoleNames", combined);
        Assert.DoesNotContain("PhotographerRole", combined);
        Assert.DoesNotContain("DocumentationDivision", combined);
        Assert.DoesNotContain("ConservationLab", combined);
        Assert.DoesNotContain("Laboratory", combined);
        Assert.DoesNotContain("Storehouse", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Details_page_keeps_artifacts_view_and_additionally_gates_photography_by_photography_view()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Details.razor");

        Assert.Contains($"Policy = PermissionNames.{nameof(PermissionNames.ArtifactsView)}", page);
        Assert.Contains("@inject ViewArtifactImagesUseCase ViewImagesUseCase", page);
        Assert.Contains("@inject IAuthorizationService AuthorizationService", page);
        Assert.Contains("@inject AuthenticationStateProvider AuthenticationStateProvider", page);
        Assert.Contains("AuthorizationService.AuthorizeAsync(state.User, PermissionNames.PhotographyView)", page);
        Assert.Contains("private bool canViewPhotography;", page);
        Assert.Contains("@if (canViewPhotography)", page);
        Assert.Contains("href=\"@PhotographyGalleryHref\"", page);
        Assert.Contains("$\"/photography/artifacts/{ArtifactId}\"", page);
    }

    [Fact]
    public void Details_page_does_not_load_photography_data_before_authorization_check()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Details.razor");

        var authorizeIndex = page.IndexOf("canViewPhotography = (await AuthorizationService.AuthorizeAsync", StringComparison.Ordinal);
        var guardIndex = page.IndexOf("if (!canViewPhotography)", StringComparison.Ordinal);
        var loadIndex = page.IndexOf("ViewImagesUseCase.ViewArtifactImages(new ViewArtifactImagesQuery(ArtifactId))", StringComparison.Ordinal);

        Assert.True(authorizeIndex >= 0, "Details.razor must check Photography.View authorization.");
        Assert.True(guardIndex >= 0, "Details.razor must guard on canViewPhotography before loading images.");
        Assert.True(loadIndex >= 0, "Details.razor must invoke ViewArtifactImagesUseCase for the compact panel.");
        Assert.True(authorizeIndex < guardIndex && guardIndex < loadIndex,
            "Authorization must be checked, then guarded, strictly before the viewing use case is invoked.");
    }

    [Fact]
    public void Details_page_does_not_reveal_image_existence_when_unauthorized_and_avoids_management_or_primary_concepts()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Details.razor");

        Assert.Contains("photographyGallery = null;", page);
        Assert.Contains("الصورة مسجلة ولكن الملف غير متاح حاليًا.", page);
        Assert.DoesNotContain("SetPrimary", page);
        Assert.DoesNotContain("PrimaryImage", page);
        Assert.DoesNotContain("DeleteArtifactImage", page);
        Assert.DoesNotContain("UpdateArtifactImageMetadata", page);
        Assert.DoesNotContain("الصورة الرئيسية", page);
        Assert.DoesNotContain("IArtifactImageStorage", page);
        Assert.DoesNotContain("BucketName", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", page, StringComparison.OrdinalIgnoreCase);
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

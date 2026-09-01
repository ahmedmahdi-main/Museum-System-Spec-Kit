namespace MuseumSystem.Web.AcceptanceTests.Photography;

public sealed class PhotographyStorageBoundaryTests
{
    [Fact]
    public void Staff_facing_gallery_and_toolbar_never_reference_raw_storage_provider_internals()
    {
        var root = FindRepositoryRoot();
        var source = string.Join(Environment.NewLine, new[]
        {
            Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor"),
            Read(root, "src", "MuseumSystem.Web", "Components", "Photography", "PhotographyGalleryToolbar.razor"),
        });

        AssertNoStorageInternals(source);
    }

    [Fact]
    public void Artifact_details_integration_never_references_raw_storage_provider_internals()
    {
        var root = FindRepositoryRoot();
        var source = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Details.razor");

        AssertNoStorageInternals(source);
    }

    [Fact]
    public void Image_stream_endpoint_delegates_to_the_use_case_and_does_not_expose_provider_urls_or_credentials()
    {
        var root = FindRepositoryRoot();
        var source = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "ImageStreamEndpoint.cs");

        Assert.Contains("ViewArtifactImagesUseCase", source);
        Assert.Contains("RequireAuthorization(PermissionNames.PhotographyView)", source);
        AssertNoStorageInternals(source);
    }

    [Fact]
    public void Safe_gallery_dto_contracts_expose_only_opaque_application_image_identity()
    {
        var root = FindRepositoryRoot();
        var source = Read(root, "src", "MuseumSystem.Application", "Modules", "Photography", "PhotographyGalleryMapper.cs");

        Assert.Contains("ArtifactImageId", source);
        AssertNoStorageInternals(source);
    }

    [Fact]
    public void Opaque_application_image_identity_is_not_treated_as_a_forbidden_storage_internal()
    {
        var root = FindRepositoryRoot();
        var page = Read(root, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "Gallery.razor");

        Assert.Contains("ArtifactImageId", page);
        Assert.Contains("/photography/images/", page);
    }

    private static void AssertNoStorageInternals(string source)
    {
        Assert.DoesNotContain("BucketName", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OriginalObjectKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Minio", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presigned", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CreateShortLivedReadAccessAsync", source);
        Assert.DoesNotContain("AccessKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OperationalSummary", source);
        Assert.DoesNotContain("IArtifactImageStorage", source);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"(?i)endpoint\s*=", source);
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

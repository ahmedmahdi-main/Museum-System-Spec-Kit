using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class PhotographyUploadFingerprintServiceTests
{
    [Fact]
    public void Same_logical_input_produces_same_request_fingerprint()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput();

        var first = service.ComputeRequestFingerprint(input);
        var second = service.ComputeRequestFingerprint(input);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Material_input_change_produces_different_request_fingerprint()
    {
        var service = new PhotographyUploadFingerprintService();
        var original = CreateInput();
        var changed = original with { Purpose = PhotographyPurpose.PostMaintenance };

        Assert.NotEqual(
            service.ComputeRequestFingerprint(original),
            service.ComputeRequestFingerprint(changed));
    }

    [Fact]
    public void Materially_different_photographer_identities_are_not_case_collapsed()
    {
        var service = new PhotographyUploadFingerprintService();
        var first = CreateInput() with { PhotographerUserId = "PhotographerA" };
        var second = CreateInput() with { PhotographerUserId = "photographera" };

        Assert.NotEqual(
            service.ComputeRequestFingerprint(first),
            service.ComputeRequestFingerprint(second));
    }

    [Fact]
    public void Filename_only_identity_is_insufficient_for_file_fingerprint()
    {
        var service = new PhotographyUploadFingerprintService();
        var first = CreateFileInput() with { OriginalFilename = "same-name.jpg", ContentHash = "hash-a" };
        var second = CreateFileInput() with { OriginalFilename = "same-name.jpg", ContentHash = "hash-b" };

        Assert.NotEqual(
            service.ComputeFileFingerprint(first),
            service.ComputeFileFingerprint(second));
    }

    [Fact]
    public void Materially_different_content_hash_values_are_not_case_collapsed()
    {
        var service = new PhotographyUploadFingerprintService();
        var first = CreateFileInput(contentHash: "ABCDEF123456");
        var second = CreateFileInput(contentHash: "abcdef123456");

        Assert.NotEqual(
            service.ComputeFileFingerprint(first),
            service.ComputeFileFingerprint(second));
    }

    [Fact]
    public void Media_descriptor_casing_is_canonicalized()
    {
        var service = new PhotographyUploadFingerprintService();
        var first = CreateFileInput() with
        {
            DetectedContentType = "Image/JPEG",
            NormalizedFormat = "JPEG",
            NormalizedExtension = ".JPG"
        };
        var second = CreateFileInput() with
        {
            DetectedContentType = "image/jpeg",
            NormalizedFormat = "jpeg",
            NormalizedExtension = ".jpg"
        };

        Assert.Equal(
            service.ComputeFileFingerprint(first),
            service.ComputeFileFingerprint(second));
    }

    [Fact]
    public void Display_filename_change_does_not_change_file_fingerprint()
    {
        var service = new PhotographyUploadFingerprintService();
        var first = CreateFileInput() with { OriginalFilename = "front.jpg" };
        var second = CreateFileInput() with { OriginalFilename = "renamed-by-browser.jpg" };

        Assert.Equal(
            service.ComputeFileFingerprint(first),
            service.ComputeFileFingerprint(second));
    }

    [Fact]
    public void File_ordinal_affects_file_and_request_fingerprints()
    {
        var service = new PhotographyUploadFingerprintService();
        var firstFile = CreateFileInput(clientFileOrdinal: 0);
        var secondFile = CreateFileInput(clientFileOrdinal: 1);

        Assert.NotEqual(
            service.ComputeFileFingerprint(firstFile),
            service.ComputeFileFingerprint(secondFile));

        var firstRequest = CreateInput(files: [firstFile]);
        var secondRequest = CreateInput(files: [secondFile]);

        Assert.NotEqual(
            service.ComputeRequestFingerprint(firstRequest),
            service.ComputeRequestFingerprint(secondRequest));
    }

    [Fact]
    public void Provider_storage_location_is_not_part_of_request_fingerprint()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput();

        var fingerprintBeforeProviderChoice = service.ComputeRequestFingerprint(input);
        var fingerprintAfterProviderChoice = service.ComputeRequestFingerprint(input);

        Assert.Equal(fingerprintBeforeProviderChoice, fingerprintAfterProviderChoice);
    }

    [Fact]
    public void Request_fingerprint_is_deterministic_regardless_of_file_collection_order_when_ordinals_match()
    {
        var service = new PhotographyUploadFingerprintService();
        var firstFile = CreateFileInput(clientFileOrdinal: 0, contentHash: "hash-a");
        var secondFile = CreateFileInput(clientFileOrdinal: 1, contentHash: "hash-b");

        var forward = CreateInput(files: [firstFile, secondFile]);
        var reversed = CreateInput(files: [secondFile, firstFile]);

        Assert.Equal(
            service.ComputeRequestFingerprint(forward),
            service.ComputeRequestFingerprint(reversed));
    }

    [Fact]
    public void Undefined_operation_kind_is_rejected()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput() with { OperationKind = (PhotographyUploadOperationKind)999 };

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ComputeRequestFingerprint(input));
    }

    [Fact]
    public void Create_set_upload_requires_purpose_date_and_photographer()
    {
        var service = new PhotographyUploadFingerprintService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ComputeRequestFingerprint(CreateInput() with { Purpose = null }));
        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(CreateInput() with { PhotographyDate = null }));
        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(CreateInput() with { PhotographyDate = default(DateOnly) }));
        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(CreateInput() with { PhotographerUserId = " " }));
    }

    [Fact]
    public void Create_set_upload_rejects_existing_photography_set_id()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput() with { PhotographySetId = Guid.NewGuid() };

        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(input));
    }

    [Fact]
    public void Append_upload_requires_existing_photography_set_id()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = new PhotographyUploadFingerprintInput(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            PhotographyUploadOperationKind.AppendToSetUpload,
            null,
            null,
            null,
            null,
            [CreateFileInput()]);

        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(input));
    }

    [Fact]
    public void Append_upload_includes_supplied_authoritative_set_context_deterministically()
    {
        var service = new PhotographyUploadFingerprintService();
        var withoutSetContext = CreateAppendInput();
        var withSetContext = withoutSetContext with
        {
            Purpose = PhotographyPurpose.GeneralDocumentation,
            PhotographyDate = new DateOnly(2026, 8, 25),
            PhotographerUserId = "PhotographerA"
        };

        Assert.NotEqual(
            service.ComputeRequestFingerprint(withoutSetContext),
            service.ComputeRequestFingerprint(withSetContext));
        Assert.Equal(
            service.ComputeRequestFingerprint(withSetContext),
            service.ComputeRequestFingerprint(withSetContext));
    }

    [Fact]
    public void Append_upload_rejects_undefined_supplied_set_context_values()
    {
        var service = new PhotographyUploadFingerprintService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ComputeRequestFingerprint(CreateAppendInput() with { Purpose = (PhotographyPurpose)999 }));
        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(CreateAppendInput() with { PhotographyDate = default(DateOnly) }));
        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(CreateAppendInput() with { PhotographerUserId = " " }));
    }

    [Fact]
    public void Request_fingerprint_rejects_empty_artifact_id()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput() with { ArtifactId = Guid.Empty };

        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(input));
    }

    [Fact]
    public void Request_fingerprint_rejects_empty_file_list()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput(files: []);

        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(input));
    }

    [Fact]
    public void File_fingerprint_rejects_negative_ordinal()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateFileInput(clientFileOrdinal: -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ComputeFileFingerprint(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void File_fingerprint_rejects_non_positive_file_size(long fileSizeBytes)
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateFileInput() with { FileSizeBytes = fileSizeBytes };

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ComputeFileFingerprint(input));
    }

    [Fact]
    public void Request_fingerprint_rejects_duplicate_ordinals()
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateInput(files:
        [
            CreateFileInput(clientFileOrdinal: 1, contentHash: "hash-a"),
            CreateFileInput(clientFileOrdinal: 1, contentHash: "hash-b")
        ]);

        Assert.Throws<ArgumentException>(() => service.ComputeRequestFingerprint(input));
    }

    [Theory]
    [InlineData("", "image/jpeg", "jpeg", ".jpg")]
    [InlineData("hash-a", "", "jpeg", ".jpg")]
    [InlineData("hash-a", "image/jpeg", "", ".jpg")]
    [InlineData("hash-a", "image/jpeg", "jpeg", "")]
    public void File_fingerprint_rejects_empty_content_hash_or_media_descriptors(
        string contentHash,
        string detectedContentType,
        string normalizedFormat,
        string normalizedExtension)
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateFileInput() with
        {
            ContentHash = contentHash,
            DetectedContentType = detectedContentType,
            NormalizedFormat = normalizedFormat,
            NormalizedExtension = normalizedExtension
        };

        Assert.Throws<ArgumentException>(() => service.ComputeFileFingerprint(input));
    }

    [Theory]
    [InlineData(0, 3000)]
    [InlineData(4000, 0)]
    [InlineData(-1, 3000)]
    [InlineData(4000, -1)]
    public void File_fingerprint_rejects_non_positive_dimensions(int pixelWidth, int pixelHeight)
    {
        var service = new PhotographyUploadFingerprintService();
        var input = CreateFileInput() with { PixelWidth = pixelWidth, PixelHeight = pixelHeight };

        Assert.Throws<ArgumentOutOfRangeException>(() => service.ComputeFileFingerprint(input));
    }

    private static PhotographyUploadFingerprintInput CreateInput(IReadOnlyList<PhotographyUploadFingerprintFileInput>? files = null) =>
        new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            PhotographyUploadOperationKind.CreateSetUpload,
            null,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 25),
            "photographer-1",
            files ?? [CreateFileInput()]);

    private static PhotographyUploadFingerprintInput CreateAppendInput(IReadOnlyList<PhotographyUploadFingerprintFileInput>? files = null) =>
        new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            PhotographyUploadOperationKind.AppendToSetUpload,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            null,
            null,
            null,
            files ?? [CreateFileInput()]);

    private static PhotographyUploadFingerprintFileInput CreateFileInput(int clientFileOrdinal = 0, string contentHash = "sha256-content-a") =>
        new(
            clientFileOrdinal,
            1_024_000,
            contentHash,
            "image/jpeg",
            4000,
            3000,
            "jpeg",
            ".jpg",
            "artifact.jpg");
}

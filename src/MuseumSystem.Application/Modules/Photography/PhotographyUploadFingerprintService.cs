using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyUploadFingerprintService
{
    public string ComputeRequestFingerprint(PhotographyUploadFingerprintInput input)
    {
        ValidateRequestInput(input);

        var builder = new StringBuilder();
        AppendField(builder, "artifactId", input.ArtifactId.ToString("D"));
        AppendField(builder, "operationKind", input.OperationKind.ToString());
        AppendField(builder, "photographySetId", input.PhotographySetId?.ToString("D"));
        AppendField(builder, "purpose", input.Purpose?.ToString());
        AppendField(builder, "photographyDate", input.PhotographyDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendField(builder, "photographerUserId", input.PhotographerUserId is null ? null : CanonicalizeIdentity(input.PhotographerUserId));

        foreach (var file in input.Files.OrderBy(file => file.ClientFileOrdinal))
        {
            AppendField(builder, "file", ComputeFileFingerprint(file));
        }

        return Hash(builder.ToString());
    }

    public string ComputeFileFingerprint(PhotographyUploadFingerprintFileInput input)
    {
        ValidateFileInput(input);

        var builder = new StringBuilder();
        AppendField(builder, "ordinal", input.ClientFileOrdinal.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "size", input.FileSizeBytes.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "contentHash", CanonicalizeContentHash(input.ContentHash));
        AppendField(builder, "detectedContentType", CanonicalizeMediaDescriptor(input.DetectedContentType));
        AppendField(builder, "pixelWidth", input.PixelWidth.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "pixelHeight", input.PixelHeight.ToString(CultureInfo.InvariantCulture));
        AppendField(builder, "normalizedFormat", CanonicalizeMediaDescriptor(input.NormalizedFormat));
        AppendField(builder, "normalizedExtension", CanonicalizeMediaDescriptor(input.NormalizedExtension));

        return Hash(builder.ToString());
    }

    private static void ValidateRequestInput(PhotographyUploadFingerprintInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ArtifactId == Guid.Empty)
        {
            throw new ArgumentException("Artifact is required.", nameof(input));
        }

        if (input.OperationKind is not (PhotographyUploadOperationKind.CreateSetUpload or PhotographyUploadOperationKind.AppendToSetUpload))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Unsupported photography upload operation kind.");
        }

        if (input.Files is null || input.Files.Count == 0)
        {
            throw new ArgumentException("At least one file is required.", nameof(input));
        }

        var ordinals = new HashSet<int>();
        foreach (var file in input.Files)
        {
            ValidateFileInput(file);
            if (!ordinals.Add(file.ClientFileOrdinal))
            {
                throw new ArgumentException("File ordinals must be unique within the request.", nameof(input));
            }
        }

        if (input.OperationKind == PhotographyUploadOperationKind.CreateSetUpload)
        {
            if (input.PhotographySetId.HasValue)
            {
                throw new ArgumentException("Create-set upload fingerprints must not include an existing photography set.", nameof(input));
            }

            ValidatePurpose(input.Purpose);
            if (!input.PhotographyDate.HasValue || input.PhotographyDate.Value == default)
            {
                throw new ArgumentException("Photography date is required for create-set upload fingerprints.", nameof(input));
            }

            RequireIdentity(input.PhotographerUserId, "Photographer identity is required for create-set upload fingerprints.");
            return;
        }

        if (!input.PhotographySetId.HasValue || input.PhotographySetId.Value == Guid.Empty)
        {
            throw new ArgumentException("Append upload fingerprints require an existing photography set.", nameof(input));
        }

        if (input.Purpose.HasValue)
        {
            ValidatePurpose(input.Purpose);
        }

        if (input.PhotographyDate.HasValue && input.PhotographyDate.Value == default)
        {
            throw new ArgumentException("Supplied append photography date cannot be the default value.", nameof(input));
        }

        if (input.PhotographerUserId is not null)
        {
            RequireIdentity(input.PhotographerUserId, "Supplied append photographer identity cannot be empty.");
        }
    }

    private static void ValidateFileInput(PhotographyUploadFingerprintFileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ClientFileOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "File ordinal cannot be negative.");
        }

        if (input.FileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "File size must be greater than zero.");
        }

        if (input.PixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Pixel width must be greater than zero.");
        }

        if (input.PixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Pixel height must be greater than zero.");
        }

        RequireText(input.ContentHash, "Content hash is required.");
        RequireText(input.DetectedContentType, "Detected content type is required.");
        RequireText(input.NormalizedFormat, "Normalized format is required.");
        RequireText(input.NormalizedExtension, "Normalized extension is required.");
    }

    private static void ValidatePurpose(PhotographyPurpose? purpose)
    {
        if (purpose is not (PhotographyPurpose.GeneralDocumentation
            or PhotographyPurpose.PreMaintenance
            or PhotographyPurpose.DuringMaintenance
            or PhotographyPurpose.PostMaintenance))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), "Unsupported photography purpose.");
        }
    }

    private static void AppendField(StringBuilder builder, string name, string? value)
    {
        builder
            .Append(name)
            .Append('=')
            .Append(value is null ? "<null>" : Convert.ToBase64String(Encoding.UTF8.GetBytes(value)))
            .Append('\n');
    }

    private static string CanonicalizeIdentity(string? value) =>
        RequireIdentity(value, "Identity is required.");

    private static string CanonicalizeContentHash(string value) =>
        RequireText(value, "Content hash is required.");

    private static string CanonicalizeMediaDescriptor(string value) =>
        RequireText(value, "Media descriptor is required.").ToLowerInvariant();

    private static string RequireIdentity(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string RequireText(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed record PhotographyUploadFingerprintInput(
    Guid ArtifactId,
    PhotographyUploadOperationKind OperationKind,
    Guid? PhotographySetId,
    PhotographyPurpose? Purpose,
    DateOnly? PhotographyDate,
    string? PhotographerUserId,
    IReadOnlyList<PhotographyUploadFingerprintFileInput> Files);

public sealed record PhotographyUploadFingerprintFileInput(
    int ClientFileOrdinal,
    long FileSizeBytes,
    string ContentHash,
    string DetectedContentType,
    int PixelWidth,
    int PixelHeight,
    string NormalizedFormat,
    string NormalizedExtension,
    string? OriginalFilename = null);

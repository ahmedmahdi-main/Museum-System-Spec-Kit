using System.Globalization;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyObjectKeyFactory
{
    public ImageStorageObjectKey CreateOriginalKey(PhotographyObjectKeyInput input) =>
        ImageStorageObjectKey.Create(BuildKey(input, "originals", "original", input.NormalizedExtension));

    public ImageStorageObjectKey CreateDerivativeKey(PhotographyObjectKeyInput input, ImageDerivativeKind derivativeKind, string normalizedExtension) =>
        ImageStorageObjectKey.Create(BuildKey(input, "derivatives", derivativeKind.ToString().ToLowerInvariant(), normalizedExtension));

    private static string BuildKey(PhotographyObjectKeyInput input, string group, string role, string extension)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.PhotographyUploadOperationId == Guid.Empty)
        {
            throw new ArgumentException("Upload operation identity is required.", nameof(input));
        }

        if (input.ClientFileOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "File ordinal cannot be negative.");
        }

        var fingerprint = RequireSafeToken(input.FileFingerprint, nameof(input.FileFingerprint));
        var safeExtension = NormalizeExtension(extension);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"artifact-images/{input.PhotographyUploadOperationId:N}/{input.ClientFileOrdinal:000000}/{group}/{role}-{fingerprint[..Math.Min(24, fingerprint.Length)]}{safeExtension}");
    }

    private static string NormalizeExtension(string extension)
    {
        var value = RequireSafeToken(extension, nameof(extension)).Trim().ToLowerInvariant();
        if (!value.StartsWith(".", StringComparison.Ordinal))
        {
            value = $".{value}";
        }

        if (value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '.')))
        {
            throw new ArgumentException("Object key extension contains unsupported characters.", nameof(extension));
        }

        return value;
    }

    private static string RequireSafeToken(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ArgumentException("Object key token contains unsupported characters.", paramName);
        }

        return trimmed;
    }
}

public sealed record PhotographyObjectKeyInput(
    Guid PhotographyUploadOperationId,
    int ClientFileOrdinal,
    string FileFingerprint,
    string NormalizedExtension);

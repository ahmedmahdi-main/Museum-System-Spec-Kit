using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography.Imaging;

public interface IArtifactImageProcessor
{
    ValueTask<ArtifactImageValidationResult> ValidateAsync(
        Stream imageContent,
        string originalFilename,
        long lengthBytes,
        CancellationToken cancellationToken = default);

    ValueTask<ArtifactImageDerivativeGenerationResult> GenerateDerivativesAsync(
        Stream originalContent,
        ArtifactImageMediaDescriptor sourceImage,
        CancellationToken cancellationToken = default);
}

public enum ArtifactImageFormat
{
    Jpeg = 1,
    Png = 2
}

public enum ArtifactImageProcessingFailureKind
{
    Retryable = 1,
    Permanent = 2
}

public sealed record ArtifactImageMediaDescriptor(
    ArtifactImageFormat Format,
    string ContentType,
    string NormalizedExtension,
    int PixelWidth,
    int PixelHeight,
    long LengthBytes);

public sealed record ArtifactImageRejection(string Code, string StaffFacingMessage);

public sealed record ArtifactImageProcessingFailure(
    ArtifactImageProcessingFailureKind Kind,
    string Code,
    string StaffFacingMessage);

public sealed record ArtifactImageValidationResult(
    bool IsValid,
    ArtifactImageMediaDescriptor? Media,
    ArtifactImageRejection? Rejection,
    ArtifactImageProcessingFailure? Failure)
{
    public static ArtifactImageValidationResult Valid(ArtifactImageMediaDescriptor media) =>
        new(true, media ?? throw new ArgumentNullException(nameof(media)), null, null);

    public static ArtifactImageValidationResult Rejected(string code, string staffFacingMessage) =>
        new(false, null, new ArtifactImageRejection(code, staffFacingMessage), null);

    public static ArtifactImageValidationResult Failed(ArtifactImageProcessingFailureKind kind, string code, string staffFacingMessage) =>
        new(false, null, null, new ArtifactImageProcessingFailure(kind, code, staffFacingMessage));
}

public sealed record ArtifactImageDerivativeContent(
    ImageDerivativeKind Kind,
    Stream Content,
    string ContentType,
    string NormalizedExtension,
    long LengthBytes,
    int PixelWidth,
    int PixelHeight);

public sealed record ArtifactImageDerivativeGenerationResult(
    bool Succeeded,
    IReadOnlyList<ArtifactImageDerivativeContent> Derivatives,
    ArtifactImageProcessingFailure? Failure)
{
    public static ArtifactImageDerivativeGenerationResult Success(IReadOnlyList<ArtifactImageDerivativeContent> derivatives) =>
        new(true, derivatives ?? throw new ArgumentNullException(nameof(derivatives)), null);

    public static ArtifactImageDerivativeGenerationResult Failed(ArtifactImageProcessingFailureKind kind, string code, string staffFacingMessage) =>
        new(false, [], new ArtifactImageProcessingFailure(kind, code, staffFacingMessage));
}

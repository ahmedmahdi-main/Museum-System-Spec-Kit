using Microsoft.Extensions.Options;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Domain.Modules.Photography;
using SkiaSharp;

namespace MuseumSystem.Infrastructure.Photography.Imaging;

public sealed class ArtifactImageProcessor(IOptions<ArtifactImageProcessingOptions> options) : IArtifactImageProcessor
{
    private readonly ArtifactImageProcessingOptions options = options.Value;

    public async ValueTask<ArtifactImageValidationResult> ValidateAsync(
        Stream imageContent,
        string originalFilename,
        long lengthBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageContent);

        if (lengthBytes <= 0)
        {
            return ArtifactImageValidationResult.Rejected("Image.Empty", "Image file is empty.");
        }

        if (lengthBytes > options.MaximumOriginalBytes)
        {
            return ArtifactImageValidationResult.Rejected("Image.TooLarge", "Image file exceeds the configured maximum size.");
        }

        try
        {
            var bytes = await ReadBytesAsync(imageContent, options.MaximumOriginalBytes, cancellationToken);
            if (bytes.LongLength > options.MaximumOriginalBytes)
            {
                return ArtifactImageValidationResult.Rejected("Image.TooLarge", "Image file exceeds the configured maximum size.");
            }

            using var stream = new MemoryStream(bytes, writable: false);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                return ArtifactImageValidationResult.Rejected("Image.Invalid", "Image file is not a valid JPEG or PNG.");
            }

            var format = codec.EncodedFormat switch
            {
                SKEncodedImageFormat.Jpeg => ArtifactImageFormat.Jpeg,
                SKEncodedImageFormat.Png => ArtifactImageFormat.Png,
                _ => (ArtifactImageFormat?)null
            };

            if (format is null)
            {
                return ArtifactImageValidationResult.Rejected("Image.UnsupportedFormat", "Only JPEG and PNG image files are supported.");
            }

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
            {
                return ArtifactImageValidationResult.Rejected("Image.InvalidDimensions", "Image dimensions could not be read.");
            }

            return ArtifactImageValidationResult.Valid(new ArtifactImageMediaDescriptor(
                format.Value,
                format.Value == ArtifactImageFormat.Png ? "image/png" : "image/jpeg",
                format.Value == ArtifactImageFormat.Png ? ".png" : ".jpg",
                info.Width,
                info.Height,
                bytes.LongLength));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ArtifactImageValidationResult.Failed(
                ArtifactImageProcessingFailureKind.Permanent,
                "Image.ValidationFailed",
                "Image file could not be inspected.");
        }
    }

    public async ValueTask<ArtifactImageDerivativeGenerationResult> GenerateDerivativesAsync(
        Stream originalContent,
        ArtifactImageMediaDescriptor sourceImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(originalContent);
        ArgumentNullException.ThrowIfNull(sourceImage);

        try
        {
            var bytes = await ReadBytesAsync(originalContent, options.MaximumOriginalBytes, cancellationToken);
            using var source = SKBitmap.Decode(bytes);
            if (source is null || source.Width <= 0 || source.Height <= 0)
            {
                return ArtifactImageDerivativeGenerationResult.Failed(
                    ArtifactImageProcessingFailureKind.Permanent,
                    "Image.DerivativeSourceInvalid",
                    "Image derivatives could not be generated.");
            }

            var derivatives = new[]
            {
                CreateDerivative(source, ImageDerivativeKind.Thumbnail, options.Thumbnail),
                CreateDerivative(source, ImageDerivativeKind.Preview, options.Preview)
            };

            return ArtifactImageDerivativeGenerationResult.Success(derivatives);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ArtifactImageDerivativeGenerationResult.Failed(
                ArtifactImageProcessingFailureKind.Permanent,
                "Image.DerivativeGenerationFailed",
                "Image derivatives could not be generated.");
        }
    }

    private static ArtifactImageDerivativeContent CreateDerivative(SKBitmap source, ImageDerivativeKind kind, DerivativeOptions derivativeOptions)
    {
        var (width, height) = FitWithinBounds(source.Width, source.Height, derivativeOptions.MaxWidth, derivativeOptions.MaxHeight);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(source, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, derivativeOptions.JpegQuality);
        var bytes = data.ToArray();
        var stream = new MemoryStream(bytes, writable: false);
        stream.Position = 0;

        return new ArtifactImageDerivativeContent(kind, stream, "image/jpeg", ".jpg", bytes.LongLength, width, height);
    }

    private static (int Width, int Height) FitWithinBounds(int width, int height, int maxWidth, int maxHeight)
    {
        var scale = Math.Min(1d, Math.Min((double)maxWidth / width, (double)maxHeight / height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static async Task<byte[]> ReadBytesAsync(Stream content, long maximumBytes, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return copy.ToArray();
            }

            await copy.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (copy.Length > maximumBytes)
            {
                return copy.ToArray();
            }
        }
    }
}

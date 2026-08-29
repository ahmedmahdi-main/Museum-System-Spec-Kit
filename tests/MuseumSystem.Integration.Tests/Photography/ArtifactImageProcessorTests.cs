using Microsoft.Extensions.Options;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Photography.Imaging;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class ArtifactImageProcessorTests
{
    [Theory]
    [InlineData("front.jpeg", ArtifactImageFormat.Jpeg, "image/jpeg", ".jpg")]
    [InlineData("front.jpg", ArtifactImageFormat.Jpeg, "image/jpeg", ".jpg")]
    [InlineData("front.png", ArtifactImageFormat.Png, "image/png", ".png")]
    [InlineData("spoofed.png", ArtifactImageFormat.Jpeg, "image/jpeg", ".jpg")]
    public async Task Validate_detects_supported_format_from_content_not_extension(string filename, ArtifactImageFormat expectedFormat, string expectedContentType, string expectedExtension)
    {
        var bytes = expectedFormat == ArtifactImageFormat.Png
            ? PhotographyIntegrationTestImages.Png(640, 480)
            : PhotographyIntegrationTestImages.Jpeg(640, 480);
        var processor = CreateProcessor();
        await using var stream = PhotographyIntegrationTestImages.Stream(bytes);

        var result = await processor.ValidateAsync(stream, filename, bytes.LongLength);

        Assert.True(result.IsValid);
        Assert.Null(result.Rejection);
        Assert.NotNull(result.Media);
        Assert.Equal(expectedFormat, result.Media!.Format);
        Assert.Equal(expectedContentType, result.Media.ContentType);
        Assert.Equal(expectedExtension, result.Media.NormalizedExtension);
        Assert.Equal(640, result.Media.PixelWidth);
        Assert.Equal(480, result.Media.PixelHeight);
        Assert.Equal(bytes.LongLength, result.Media.LengthBytes);
    }

    [Theory]
    [InlineData("fake.jpg")]
    [InlineData("fake.png")]
    public async Task Validate_rejects_invalid_content_even_when_filename_extension_is_allowed(string filename)
    {
        var bytes = "not really an image"u8.ToArray();
        var processor = CreateProcessor();
        await using var stream = PhotographyIntegrationTestImages.Stream(bytes);

        var result = await processor.ValidateAsync(stream, filename, bytes.LongLength);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Rejection);
        Assert.Equal("Image.Invalid", result.Rejection!.Code);
        Assert.Null(result.Media);
    }

    [Fact]
    public async Task Validate_rejects_unsupported_decodable_formats_with_structured_rejection()
    {
        var bytes = PhotographyIntegrationTestImages.Gif();
        var processor = CreateProcessor();
        await using var stream = PhotographyIntegrationTestImages.Stream(bytes);

        var result = await processor.ValidateAsync(stream, "scan.jpg", bytes.LongLength);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Rejection);
        Assert.Equal("Image.UnsupportedFormat", result.Rejection!.Code);
        Assert.Null(result.Media);
    }

    [Fact]
    public async Task Validate_enforces_configured_maximum_original_size_before_accepting_media()
    {
        var bytes = PhotographyIntegrationTestImages.Jpeg(100, 80);
        var processor = CreateProcessor(maximumOriginalBytes: bytes.LongLength - 1);
        await using var stream = PhotographyIntegrationTestImages.Stream(bytes);

        var result = await processor.ValidateAsync(stream, "too-large.jpg", bytes.LongLength);

        Assert.False(result.IsValid);
        Assert.Equal("Image.TooLarge", result.Rejection?.Code);
    }

    [Fact]
    public async Task GenerateDerivatives_creates_bounded_independent_jpeg_streams_and_preserves_aspect_ratio()
    {
        var bytes = PhotographyIntegrationTestImages.Jpeg(2400, 1200);
        var processor = CreateProcessor(
            maximumOriginalBytes: 10 * 1024 * 1024,
            thumbnail: new DerivativeOptions(320, 320, 82),
            preview: new DerivativeOptions(1600, 1600, 86));
        await using var validationStream = PhotographyIntegrationTestImages.Stream(bytes);
        var validation = await processor.ValidateAsync(validationStream, "wide.jpg", bytes.LongLength);
        await using var derivativeStream = PhotographyIntegrationTestImages.Stream(bytes);

        var result = await processor.GenerateDerivativesAsync(derivativeStream, validation.Media!);

        Assert.True(result.Succeeded);
        Assert.Null(result.Failure);
        Assert.Collection(result.Derivatives.OrderBy(derivative => derivative.Kind),
            thumbnail => AssertDerivative(thumbnail, ImageDerivativeKind.Thumbnail, 320, 160),
            preview => AssertDerivative(preview, ImageDerivativeKind.Preview, 1600, 800));
        Assert.All(result.Derivatives, derivative =>
        {
            Assert.NotSame(derivativeStream, derivative.Content);
            Assert.True(derivative.Content.CanRead);
            Assert.Equal(0, derivative.Content.Position);
            Assert.True(derivative.LengthBytes > 0);
        });
    }

    [Fact]
    public async Task GenerateDerivatives_does_not_upscale_small_originals()
    {
        var bytes = PhotographyIntegrationTestImages.Png(120, 90);
        var processor = CreateProcessor();
        await using var validationStream = PhotographyIntegrationTestImages.Stream(bytes);
        var validation = await processor.ValidateAsync(validationStream, "small.png", bytes.LongLength);
        await using var derivativeStream = PhotographyIntegrationTestImages.Stream(bytes);

        var result = await processor.GenerateDerivativesAsync(derivativeStream, validation.Media!);

        Assert.True(result.Succeeded);
        Assert.All(result.Derivatives, derivative =>
        {
            Assert.Equal(120, derivative.PixelWidth);
            Assert.Equal(90, derivative.PixelHeight);
        });
    }

    [Fact]
    public async Task Validation_and_derivative_generation_do_not_mutate_original_binary()
    {
        var bytes = PhotographyIntegrationTestImages.Jpeg(640, 480);
        var originalCopy = bytes.ToArray();
        var processor = CreateProcessor();
        await using var stream = new MemoryStream(bytes, writable: true);

        var validation = await processor.ValidateAsync(stream, "immutable.jpg", bytes.LongLength);
        stream.Position = 0;
        var derivatives = await processor.GenerateDerivativesAsync(stream, validation.Media!);

        Assert.True(derivatives.Succeeded);
        Assert.Equal(originalCopy, bytes);
    }

    private static void AssertDerivative(ArtifactImageDerivativeContent derivative, ImageDerivativeKind kind, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(kind, derivative.Kind);
        Assert.Equal("image/jpeg", derivative.ContentType);
        Assert.Equal(".jpg", derivative.NormalizedExtension);
        Assert.Equal(expectedWidth, derivative.PixelWidth);
        Assert.Equal(expectedHeight, derivative.PixelHeight);
    }

    private static ArtifactImageProcessor CreateProcessor(
        long maximumOriginalBytes = 20 * 1024 * 1024,
        DerivativeOptions? thumbnail = null,
        DerivativeOptions? preview = null) =>
        new(Options.Create(new ArtifactImageProcessingOptions
        {
            MaximumOriginalBytes = maximumOriginalBytes,
            Thumbnail = thumbnail ?? new DerivativeOptions(320, 320, 82),
            Preview = preview ?? new DerivativeOptions(1600, 1600, 86)
        }));
}

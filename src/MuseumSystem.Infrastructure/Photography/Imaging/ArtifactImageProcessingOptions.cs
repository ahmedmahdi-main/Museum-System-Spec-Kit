using Microsoft.Extensions.Options;

namespace MuseumSystem.Infrastructure.Photography.Imaging;

public sealed class ArtifactImageProcessingOptions
{
    public long MaximumOriginalBytes { get; set; } = 20 * 1024 * 1024;
    public DerivativeOptions Thumbnail { get; set; } = new(320, 320, 82);
    public DerivativeOptions Preview { get; set; } = new(1600, 1600, 86);
}

public sealed record DerivativeOptions(int MaxWidth, int MaxHeight, int JpegQuality);

public sealed class ArtifactImageProcessingOptionsValidator : IValidateOptions<ArtifactImageProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, ArtifactImageProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (options.MaximumOriginalBytes <= 0)
        {
            failures.Add($"{nameof(options.MaximumOriginalBytes)} must be greater than zero.");
        }

        ValidateDerivative(options.Thumbnail, nameof(options.Thumbnail), failures);
        ValidateDerivative(options.Preview, nameof(options.Preview), failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateDerivative(DerivativeOptions options, string name, List<string> failures)
    {
        if (options.MaxWidth <= 0)
        {
            failures.Add($"{name}.{nameof(options.MaxWidth)} must be greater than zero.");
        }

        if (options.MaxHeight <= 0)
        {
            failures.Add($"{name}.{nameof(options.MaxHeight)} must be greater than zero.");
        }

        if (options.JpegQuality is < 1 or > 100)
        {
            failures.Add($"{name}.{nameof(options.JpegQuality)} must be between 1 and 100.");
        }
    }
}

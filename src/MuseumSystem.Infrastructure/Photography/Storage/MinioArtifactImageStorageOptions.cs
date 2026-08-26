using Microsoft.Extensions.Options;

namespace MuseumSystem.Infrastructure.Photography.Storage;

public sealed class MinioArtifactImageStorageOptions
{
    public const string SectionName = "Photography:Storage";

    public string Provider { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string? Region { get; init; }
    public bool UseTls { get; init; } = true;
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 30;
}

public sealed class MinioArtifactImageStorageOptionsValidator : IValidateOptions<MinioArtifactImageStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, MinioArtifactImageStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.Equals(options.Provider, "Minio", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        Require(options.Endpoint, nameof(options.Endpoint), failures);
        Require(options.BucketName, nameof(options.BucketName), failures);
        Require(options.AccessKey, nameof(options.AccessKey), failures);
        Require(options.SecretKey, nameof(options.SecretKey), failures);

        if (options.RequestTimeoutSeconds <= 0)
        {
            failures.Add($"{nameof(options.RequestTimeoutSeconds)} must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(string? value, string name, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{name} is required when Photography storage provider is Minio.");
        }
    }
}

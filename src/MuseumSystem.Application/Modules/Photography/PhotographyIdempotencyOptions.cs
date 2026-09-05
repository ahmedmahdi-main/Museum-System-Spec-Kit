using Microsoft.Extensions.Options;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyIdempotencyOptions
{
    public const string SectionName = "Photography:Idempotency";

    public int RetentionDays { get; init; } = 14;
}

public sealed class PhotographyIdempotencyOptionsValidator : IValidateOptions<PhotographyIdempotencyOptions>
{
    public ValidateOptionsResult Validate(string? name, PhotographyIdempotencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.RetentionDays > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{nameof(options.RetentionDays)} must be greater than zero.");
    }
}

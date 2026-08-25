namespace MuseumSystem.Domain.Modules.Photography;

public static class PhotographyRules
{
    public static readonly TimeSpan UploaderGracePeriod = TimeSpan.FromMinutes(60);

    public static bool DoesImageBelongToSet(PhotographySet set, ArtifactImage image)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(image);

        return image.PhotographySetId == set.PhotographySetId && image.ArtifactId == set.ArtifactId;
    }

    public static bool IsPrimaryImageEligible(ArtifactImage image, Guid artifactId)
    {
        ArgumentNullException.ThrowIfNull(image);

        return image.ArtifactId == artifactId && image.Status == ArtifactImageStatus.Available;
    }

    public static bool IsDeletionReasonRequired(ArtifactImageDeletionMode mode) =>
        PhotographyEnumValidation.RequireDefined(mode, nameof(mode)) == ArtifactImageDeletionMode.Privileged;

    public static bool HasRequiredDeletionReason(ArtifactImageDeletionMode mode, string? deletionReason) =>
        !IsDeletionReasonRequired(mode) || !string.IsNullOrWhiteSpace(deletionReason);

    public static bool IsWithinUploaderGracePeriod(DateTimeOffset uploadedAtUtc, DateTimeOffset serverNowUtc)
    {
        if (serverNowUtc < uploadedAtUtc)
        {
            return false;
        }

        return serverNowUtc - uploadedAtUtc <= UploaderGracePeriod;
    }

    public static bool CanUseUploaderGraceDeletion(string uploadedByUserId, string currentUserId, DateTimeOffset uploadedAtUtc, DateTimeOffset serverNowUtc, bool currentUserHasUploadPermission) =>
        currentUserHasUploadPermission
        && string.Equals(NormalizeRequired(uploadedByUserId, nameof(uploadedByUserId)), NormalizeRequired(currentUserId, nameof(currentUserId)), StringComparison.Ordinal)
        && IsWithinUploaderGracePeriod(uploadedAtUtc, serverNowUtc);

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

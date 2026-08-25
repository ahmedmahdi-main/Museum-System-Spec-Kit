namespace MuseumSystem.Domain.Modules.Photography;

public enum PhotographyPurpose
{
    GeneralDocumentation = 1,
    PreMaintenance = 2,
    DuringMaintenance = 3,
    PostMaintenance = 4
}

public enum ArtifactImageStatus
{
    Available = 1,
    DeletePending = 2,
    Deleted = 3
}

public enum ImageDerivativeKind
{
    Thumbnail = 1,
    Preview = 2
}

public enum ArtifactImageDeletionMode
{
    UploaderGracePeriod = 1,
    Privileged = 2
}

public enum PhotographyUploadOperationKind
{
    CreateSetUpload = 1,
    AppendToSetUpload = 2
}

public enum PhotographyUploadOperationStatus
{
    InProgress = 1,
    Completed = 2,
    CompletedWithFailures = 3,
    Failed = 4,
    RecoveryNeeded = 5
}

public enum PhotographyUploadFileOutcomeStatus
{
    Succeeded = 1,
    Rejected = 2,
    Failed = 3,
    CleanupPending = 4,
    RecoveryNeeded = 5
}

public enum StorageOperationRecoveryType
{
    UploadCleanup = 1,
    DeleteCleanup = 2,
    DerivativeCleanup = 3,
    MissingObject = 4,
    DerivativeGeneration = 5
}

public enum StorageOperationRecoveryStatus
{
    Pending = 1,
    Retrying = 2,
    Resolved = 3,
    FailedNeedsAttention = 4
}

public sealed record ImageStorageObjectKey
{
    private ImageStorageObjectKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ImageStorageObjectKey Create(string value) => new(RequireText(value, nameof(value)));

    public override string ToString() => Value;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

public sealed record DeletionReason
{
    private DeletionReason(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static DeletionReason Create(string value) => new(RequireText(value, nameof(value)));

    public override string ToString() => Value;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

internal static class PhotographyEnumValidation
{
    public static PhotographyPurpose RequireDefined(PhotographyPurpose purpose, string paramName)
    {
        return purpose is PhotographyPurpose.GeneralDocumentation
            or PhotographyPurpose.PreMaintenance
            or PhotographyPurpose.DuringMaintenance
            or PhotographyPurpose.PostMaintenance
            ? purpose
            : throw new ArgumentOutOfRangeException(paramName, "Unsupported photography purpose.");
    }

    public static ArtifactImageDeletionMode RequireDefined(ArtifactImageDeletionMode mode, string paramName)
    {
        return mode is ArtifactImageDeletionMode.UploaderGracePeriod
            or ArtifactImageDeletionMode.Privileged
            ? mode
            : throw new ArgumentOutOfRangeException(paramName, "Unsupported image deletion mode.");
    }

    public static PhotographyUploadOperationKind RequireDefined(PhotographyUploadOperationKind operationKind, string paramName)
    {
        return operationKind is PhotographyUploadOperationKind.CreateSetUpload
            or PhotographyUploadOperationKind.AppendToSetUpload
            ? operationKind
            : throw new ArgumentOutOfRangeException(paramName, "Unsupported photography upload operation kind.");
    }

    public static PhotographyUploadFileOutcomeStatus RequireDefined(PhotographyUploadFileOutcomeStatus status, string paramName)
    {
        return status is PhotographyUploadFileOutcomeStatus.Succeeded
            or PhotographyUploadFileOutcomeStatus.Rejected
            or PhotographyUploadFileOutcomeStatus.Failed
            or PhotographyUploadFileOutcomeStatus.CleanupPending
            or PhotographyUploadFileOutcomeStatus.RecoveryNeeded
            ? status
            : throw new ArgumentOutOfRangeException(paramName, "Unsupported upload file outcome status.");
    }
}

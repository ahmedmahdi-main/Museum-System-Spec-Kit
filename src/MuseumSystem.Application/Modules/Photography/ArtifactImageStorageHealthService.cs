using MuseumSystem.Application.Modules.Photography.Storage;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class ArtifactImageStorageHealthService
{
    public ArtifactImageStorageAssessment Assess(
        ArtifactImageStorageResultKind kind,
        ArtifactImageStorageOperationContext context = ArtifactImageStorageOperationContext.General) =>
        kind switch
        {
            ArtifactImageStorageResultKind.Success => new(
                kind,
                ArtifactImageStorageCondition.Available,
                IsSuccessful: true,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: false,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: false,
                CanonicalStaffFacingMessage: "Image storage operation completed.",
                OperationalSummary: null),
            ArtifactImageStorageResultKind.NotFound => new(
                kind,
                ArtifactImageStorageCondition.Missing,
                IsSuccessful: false,
                IsMissing: true,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: false,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: false,
                CanonicalStaffFacingMessage: "Stored image object was not found.",
                OperationalSummary: "Object storage reported the object was missing."),
            ArtifactImageStorageResultKind.AlreadyExists => new(
                kind,
                ArtifactImageStorageCondition.Conflict,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: true,
                IsFailureRetryable: false,
                IsStorageUnavailable: false,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: context == ArtifactImageStorageOperationContext.Write,
                CanonicalStaffFacingMessage: "Stored image object already exists.",
                OperationalSummary: "Object storage reported an object identity conflict."),
            ArtifactImageStorageResultKind.RetryableFailure => new(
                kind,
                ArtifactImageStorageCondition.TemporaryUnavailable,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: true,
                IsStorageUnavailable: true,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: context == ArtifactImageStorageOperationContext.Write,
                CanonicalStaffFacingMessage: "Image storage is temporarily unavailable. Please try again.",
                OperationalSummary: "Object storage reported a transient availability failure."),
            ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured => new(
                kind,
                ArtifactImageStorageCondition.ConfigurationUnavailable,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: true,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: context == ArtifactImageStorageOperationContext.Write,
                CanonicalStaffFacingMessage: "Image storage is currently unavailable.",
                OperationalSummary: "Object storage reported an authorization or configuration failure."),
            ArtifactImageStorageResultKind.PermanentFailure => new(
                kind,
                ArtifactImageStorageCondition.PermanentProviderFailure,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: true,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: context == ArtifactImageStorageOperationContext.Write,
                CanonicalStaffFacingMessage: "Image storage is currently unavailable.",
                OperationalSummary: "Object storage reported a permanent provider failure."),
            ArtifactImageStorageResultKind.NotSupported => new(
                kind,
                ArtifactImageStorageCondition.Unsupported,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: false,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: false,
                CanonicalStaffFacingMessage: "Image storage capability is not supported.",
                OperationalSummary: "Object storage capability is not supported."),
            ArtifactImageStorageResultKind.PartialFailure => new(
                kind,
                ArtifactImageStorageCondition.PartialConsistencyFailure,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: true,
                RequiresRecovery: true,
                RequiresAuthoritativeWriteVerification: context == ArtifactImageStorageOperationContext.Write,
                CanonicalStaffFacingMessage: "Image storage operation could not be completed safely. Recovery is required.",
                OperationalSummary: "Object storage reported a partial consistency failure."),
            _ => new(
                kind,
                ArtifactImageStorageCondition.ProviderFailure,
                IsSuccessful: false,
                IsMissing: false,
                IsConflict: false,
                IsFailureRetryable: false,
                IsStorageUnavailable: true,
                RequiresRecovery: false,
                RequiresAuthoritativeWriteVerification: context == ArtifactImageStorageOperationContext.Write,
                CanonicalStaffFacingMessage: "Image storage is currently unavailable.",
                OperationalSummary: "Object storage reported an unclassified provider failure.")
        };

    public ArtifactImageStorageAssessment Assess(
        ArtifactImageStorageFailure? failure,
        ArtifactImageStorageOperationContext context = ArtifactImageStorageOperationContext.General) =>
        Assess(failure?.Kind ?? ArtifactImageStorageResultKind.PermanentFailure, context);
}

public enum ArtifactImageStorageOperationContext
{
    General = 1,
    Write = 2
}

public enum ArtifactImageStorageCondition
{
    Available = 1,
    Missing = 2,
    Conflict = 3,
    TemporaryUnavailable = 4,
    ConfigurationUnavailable = 5,
    PermanentProviderFailure = 6,
    Unsupported = 7,
    PartialConsistencyFailure = 8,
    ProviderFailure = 9
}

public sealed record ArtifactImageStorageAssessment(
    ArtifactImageStorageResultKind ResultKind,
    ArtifactImageStorageCondition Condition,
    bool IsSuccessful,
    bool IsMissing,
    bool IsConflict,
    bool IsFailureRetryable,
    bool IsStorageUnavailable,
    bool RequiresRecovery,
    bool RequiresAuthoritativeWriteVerification,
    string CanonicalStaffFacingMessage,
    string? OperationalSummary);

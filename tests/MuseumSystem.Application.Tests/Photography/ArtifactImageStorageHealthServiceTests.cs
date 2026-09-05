using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class ArtifactImageStorageHealthServiceTests
{
    private readonly ArtifactImageStorageHealthService service = new();

    public static TheoryData<ArtifactImageStorageResultKind, ArtifactImageStorageCondition, bool, bool, bool, bool, bool, bool, string> ClassificationCases() =>
        new()
        {
            { ArtifactImageStorageResultKind.Success, ArtifactImageStorageCondition.Available, true, false, false, false, false, false, "Image storage operation completed." },
            { ArtifactImageStorageResultKind.NotFound, ArtifactImageStorageCondition.Missing, false, true, false, false, false, false, "Stored image object was not found." },
            { ArtifactImageStorageResultKind.AlreadyExists, ArtifactImageStorageCondition.Conflict, false, false, true, false, false, false, "Stored image object already exists." },
            { ArtifactImageStorageResultKind.RetryableFailure, ArtifactImageStorageCondition.TemporaryUnavailable, false, false, false, true, true, false, "Image storage is temporarily unavailable. Please try again." },
            { ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured, ArtifactImageStorageCondition.ConfigurationUnavailable, false, false, false, false, true, false, "Image storage is currently unavailable." },
            { ArtifactImageStorageResultKind.PermanentFailure, ArtifactImageStorageCondition.PermanentProviderFailure, false, false, false, false, true, false, "Image storage is currently unavailable." },
            { ArtifactImageStorageResultKind.NotSupported, ArtifactImageStorageCondition.Unsupported, false, false, false, false, false, false, "Image storage capability is not supported." },
            { ArtifactImageStorageResultKind.PartialFailure, ArtifactImageStorageCondition.PartialConsistencyFailure, false, false, false, false, true, true, "Image storage operation could not be completed safely. Recovery is required." }
        };

    [Theory]
    [MemberData(nameof(ClassificationCases))]
    public void Classifies_all_storage_result_kinds_without_provider_details(
        ArtifactImageStorageResultKind kind,
        ArtifactImageStorageCondition expectedCondition,
        bool expectedSuccess,
        bool expectedMissing,
        bool expectedConflict,
        bool expectedRetryable,
        bool expectedUnavailable,
        bool expectedRecovery,
        string expectedStaffMessage)
    {
        var assessment = service.Assess(kind);

        Assert.Equal(kind, assessment.ResultKind);
        Assert.Equal(expectedCondition, assessment.Condition);
        Assert.Equal(expectedSuccess, assessment.IsSuccessful);
        Assert.Equal(expectedMissing, assessment.IsMissing);
        Assert.Equal(expectedConflict, assessment.IsConflict);
        Assert.Equal(expectedRetryable, assessment.IsFailureRetryable);
        Assert.Equal(expectedUnavailable, assessment.IsStorageUnavailable);
        Assert.Equal(expectedRecovery, assessment.RequiresRecovery);
        Assert.False(assessment.RequiresAuthoritativeWriteVerification);
        Assert.Equal(expectedStaffMessage, assessment.CanonicalStaffFacingMessage);
        AssertNoProviderLeak(assessment.CanonicalStaffFacingMessage);
        if (assessment.OperationalSummary is not null)
        {
            AssertNoProviderLeak(assessment.OperationalSummary);
        }
    }

    [Theory]
    [InlineData(ArtifactImageStorageResultKind.AlreadyExists)]
    [InlineData(ArtifactImageStorageResultKind.RetryableFailure)]
    [InlineData(ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured)]
    [InlineData(ArtifactImageStorageResultKind.PermanentFailure)]
    [InlineData(ArtifactImageStorageResultKind.PartialFailure)]
    public void Failed_writes_that_may_have_changed_storage_require_authoritative_stat(ArtifactImageStorageResultKind kind)
    {
        var writeAssessment = service.Assess(kind, ArtifactImageStorageOperationContext.Write);

        Assert.True(writeAssessment.RequiresAuthoritativeWriteVerification);
        Assert.False(service.Assess(kind).RequiresAuthoritativeWriteVerification);
    }

    [Theory]
    [InlineData(ArtifactImageStorageResultKind.NotFound)]
    [InlineData(ArtifactImageStorageResultKind.NotSupported)]
    public void Definitive_failed_writes_do_not_require_authoritative_stat(ArtifactImageStorageResultKind kind)
    {
        var writeAssessment = service.Assess(kind, ArtifactImageStorageOperationContext.Write);

        Assert.False(writeAssessment.RequiresAuthoritativeWriteVerification);
    }

    [Fact]
    public void Failure_assessment_uses_canonical_messages_instead_of_provider_text()
    {
        var failure = new ArtifactImageStorageFailure(
            ArtifactImageStorageResultKind.RetryableFailure,
            "Provider.Raw",
            "MinIO bucket artifact-images endpoint http://127.0.0.1:9000 credentials secret exception path C:\\storage\\image.jpg",
            "minio://artifact-images/objects/key.jpg credentials secret exception path C:\\storage\\image.jpg");

        var assessment = service.Assess(failure, ArtifactImageStorageOperationContext.Write);

        Assert.Equal("Image storage is temporarily unavailable. Please try again.", assessment.CanonicalStaffFacingMessage);
        Assert.Equal("Object storage reported a transient availability failure.", assessment.OperationalSummary);
        Assert.True(assessment.IsFailureRetryable);
        Assert.True(assessment.RequiresAuthoritativeWriteVerification);
        AssertNoProviderLeak(assessment.CanonicalStaffFacingMessage);
        AssertNoProviderLeak(assessment.OperationalSummary!);
    }

    private static void AssertNoProviderLeak(string value)
    {
        Assert.DoesNotContain("MinIO", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifact-images", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objects/key", value, StringComparison.OrdinalIgnoreCase);
    }
}

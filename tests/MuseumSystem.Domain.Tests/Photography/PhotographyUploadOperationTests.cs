using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class PhotographyUploadOperationTests
{
    [Fact]
    public void First_success_does_not_prematurely_finalize_multi_file_upload()
    {
        var operation = StartCreateSetOperation();
        var outcome = SuccessfulOutcome(operation, 0);

        operation.AddFileOutcome(outcome);

        Assert.Equal(PhotographyUploadOperationStatus.InProgress, operation.Status);
        Assert.Null(operation.CompletedAt);
        Assert.Equal(outcome, Assert.Single(operation.FileOutcomes));
    }

    [Fact]
    public void Success_and_rejection_finalize_as_completed_with_failures_only_after_explicit_batch_finalization()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            1,
            "bad.txt",
            "file-fingerprint-1",
            "Unsupported file type."));

        Assert.Equal(PhotographyUploadOperationStatus.InProgress, operation.Status);
        Assert.Null(operation.CompletedAt);

        operation.AttachPhotographySet(Guid.NewGuid());
        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, operation.Status);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void All_succeeded_batch_finalizes_as_completed()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AddFileOutcome(SuccessfulOutcome(operation, 1));
        operation.AttachPhotographySet(Guid.NewGuid());
        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.Completed, operation.Status);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void All_rejected_or_failed_batch_finalizes_as_failed()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            0,
            "bad.txt",
            "file-fingerprint-0",
            "Unsupported file type."));
        operation.AddFileOutcome(PhotographyUploadFileOutcome.Failed(
            operation.PhotographyUploadOperationId,
            1,
            "broken.jpg",
            "file-fingerprint-1",
            "Image could not be processed."));
        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void Retry_can_inspect_persisted_outcomes_while_operation_is_still_in_progress()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.MarkSeen();

        Assert.Equal(PhotographyUploadOperationStatus.InProgress, operation.Status);
        Assert.Null(operation.CompletedAt);
        Assert.Collection(operation.FileOutcomes, outcome =>
        {
            Assert.Equal(0, outcome.ClientFileOrdinal);
            Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, outcome.Status);
            Assert.NotNull(outcome.ArtifactImageId);
            Assert.NotNull(outcome.OriginalObjectKey);
        });
    }

    [Fact]
    public void Cleanup_pending_and_recovery_needed_do_not_appear_completed()
    {
        var operation = StartCreateSetOperation();
        var cleanupOutcome = PhotographyUploadFileOutcome.CleanupPending(
            operation.PhotographyUploadOperationId,
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            "Metadata failed after object write; cleanup is pending.",
            originalObjectKey: ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg"));

        operation.AddFileOutcome(cleanupOutcome);
        operation.AddFileOutcome(SuccessfulOutcome(operation, 1));
        operation.AttachPhotographySet(Guid.NewGuid());
        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, operation.Status);
        Assert.Null(operation.CompletedAt);
        Assert.True(cleanupOutcome.IsUnresolved);

        cleanupOutcome.ResolveToFailed("Cleanup completed after metadata failure.");
        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, operation.Status);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void Recovery_needed_operation_rejects_new_file_outcome_and_photography_set_attachment()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(PhotographyUploadFileOutcome.CleanupPending(
            operation.PhotographyUploadOperationId,
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            "Cleanup is pending."));
        operation.FinalizeBatch(expectedFileCount: 1);

        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, operation.Status);
        Assert.Throws<InvalidOperationException>(() => operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            1,
            "bad.txt",
            "file-fingerprint-1",
            "Unsupported file type.")));
        Assert.Throws<InvalidOperationException>(() => operation.AttachPhotographySet(Guid.NewGuid()));
    }

    [Fact]
    public void Recovery_needed_operation_accepts_same_photography_set_reattach()
    {
        var operation = StartCreateSetOperation();
        var setId = Guid.NewGuid();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AddFileOutcome(PhotographyUploadFileOutcome.CleanupPending(
            operation.PhotographyUploadOperationId,
            1,
            "image-2.jpg",
            "file-fingerprint-1",
            "Cleanup is pending."));
        operation.AttachPhotographySet(setId);
        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, operation.Status);
        operation.AttachPhotographySet(setId);

        Assert.Equal(setId, operation.PhotographySetId);
    }

    [Fact]
    public void Terminal_operations_reject_new_file_outcome_and_photography_set_attachment()
    {
        var completed = StartCreateSetOperation();
        completed.AddFileOutcome(SuccessfulOutcome(completed, 0));
        completed.AttachPhotographySet(Guid.NewGuid());
        completed.FinalizeBatch(expectedFileCount: 1);

        var completedWithFailures = StartCreateSetOperation();
        completedWithFailures.AddFileOutcome(SuccessfulOutcome(completedWithFailures, 0));
        completedWithFailures.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            completedWithFailures.PhotographyUploadOperationId,
            1,
            "bad.txt",
            "file-fingerprint-1",
            "Unsupported file type."));
        completedWithFailures.AttachPhotographySet(Guid.NewGuid());
        completedWithFailures.FinalizeBatch(expectedFileCount: 2);

        var failed = StartCreateSetOperation();
        failed.AddFileOutcome(PhotographyUploadFileOutcome.Failed(
            failed.PhotographyUploadOperationId,
            0,
            "broken.jpg",
            "file-fingerprint-0",
            "Image could not be processed."));
        failed.FinalizeBatch(expectedFileCount: 1);

        foreach (var operation in new[] { completed, completedWithFailures, failed })
        {
            Assert.Throws<InvalidOperationException>(() => operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
                operation.PhotographyUploadOperationId,
                9,
                "bad.txt",
                "file-fingerprint-9",
                "Unsupported file type.")));
            Assert.Throws<InvalidOperationException>(() => operation.AttachPhotographySet(Guid.NewGuid()));
        }
    }

    [Fact]
    public void Cleanup_pending_outcome_can_transition_to_recovery_needed_without_losing_object_identity()
    {
        var objectKey = ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg");
        var outcome = PhotographyUploadFileOutcome.CleanupPending(
            Guid.NewGuid(),
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            "Cleanup pending.",
            originalObjectKey: objectKey);

        outcome.MarkRecoveryNeeded("Cleanup failed and requires recovery.");

        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, outcome.Status);
        Assert.Null(outcome.FinalizedAt);
        Assert.Equal(objectKey, outcome.OriginalObjectKey);
    }

    [Fact]
    public void Existing_file_outcome_ordinal_cannot_be_replaced_with_second_row()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));

        Assert.Throws<InvalidOperationException>(() => operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            0,
            "bad.txt",
            "file-fingerprint-retry",
            "Unsupported file type.")));
    }

    [Fact]
    public void Unresolved_outcome_cannot_replace_stable_object_identity_when_resolved()
    {
        var outcome = PhotographyUploadFileOutcome.CleanupPending(
            Guid.NewGuid(),
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            "Metadata failed after object write.",
            originalObjectKey: ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg"));

        Assert.Throws<InvalidOperationException>(() => outcome.ResolveToSucceeded(
            Guid.NewGuid(),
            ImageStorageObjectKey.Create("artifact-images/originals/different.jpg"),
            [],
            "Recovered."));
    }

    [Fact]
    public void Final_file_outcome_cannot_transition_again()
    {
        var outcome = PhotographyUploadFileOutcome.Failed(
            Guid.NewGuid(),
            0,
            "broken.jpg",
            "file-fingerprint-0",
            "Image could not be processed.");

        Assert.Throws<InvalidOperationException>(() => outcome.ResolveToFailed("Different final result."));
    }

    [Fact]
    public void In_progress_create_set_without_successful_outcome_rejects_photography_set_attachment()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            0,
            "bad.txt",
            "file-fingerprint-0",
            "Unsupported file type."));

        Assert.Throws<InvalidOperationException>(() => operation.AttachPhotographySet(Guid.NewGuid()));
        Assert.Null(operation.PhotographySetId);
    }

    [Fact]
    public void Create_set_upload_attaches_photography_set_once_and_rejects_replacement()
    {
        var operation = StartCreateSetOperation();
        var setId = Guid.NewGuid();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AttachPhotographySet(setId);
        operation.AttachPhotographySet(setId);

        Assert.Equal(setId, operation.PhotographySetId);
        Assert.Throws<InvalidOperationException>(() => operation.AttachPhotographySet(Guid.NewGuid()));
    }

    [Fact]
    public void Append_upload_requires_existing_photography_set_and_keeps_it_immutable()
    {
        var setId = Guid.NewGuid();
        var operation = PhotographyUploadOperation.Start(
            "photographer-1",
            PhotographyUploadOperationKind.AppendToSetUpload,
            "key-1",
            "fingerprint-1",
            Guid.NewGuid(),
            setId);

        Assert.Equal(setId, operation.PhotographySetId);
        Assert.Throws<InvalidOperationException>(() => operation.AttachPhotographySet(Guid.NewGuid()));
    }

    [Fact]
    public void Invalid_operation_kind_and_set_id_combinations_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => PhotographyUploadOperation.Start(
            "photographer-1",
            PhotographyUploadOperationKind.CreateSetUpload,
            "key-1",
            "fingerprint-1",
            Guid.NewGuid(),
            Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() => PhotographyUploadOperation.Start(
            "photographer-1",
            PhotographyUploadOperationKind.AppendToSetUpload,
            "key-1",
            "fingerprint-1",
            Guid.NewGuid()));
    }

    [Fact]
    public void Create_set_successful_batch_cannot_finalize_without_established_set()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));

        Assert.Throws<InvalidOperationException>(() => operation.FinalizeBatch(expectedFileCount: 1));
        Assert.Equal(PhotographyUploadOperationStatus.InProgress, operation.Status);
        Assert.Null(operation.CompletedAt);
    }

    [Fact]
    public void Create_set_success_and_rejection_cannot_finalize_without_established_set()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            1,
            "bad.txt",
            "file-fingerprint-1",
            "Unsupported file type."));

        Assert.Throws<InvalidOperationException>(() => operation.FinalizeBatch(expectedFileCount: 2));
        Assert.Equal(PhotographyUploadOperationStatus.InProgress, operation.Status);
    }

    [Fact]
    public void Create_set_success_and_unresolved_recovery_cannot_enter_recovery_needed_without_established_set()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AddFileOutcome(PhotographyUploadFileOutcome.CleanupPending(
            operation.PhotographyUploadOperationId,
            1,
            "image-2.jpg",
            "file-fingerprint-1",
            "Cleanup is pending."));

        Assert.Throws<InvalidOperationException>(() => operation.FinalizeBatch(expectedFileCount: 2));
        Assert.Equal(PhotographyUploadOperationStatus.InProgress, operation.Status);
    }

    [Fact]
    public void All_invalid_or_failed_create_set_batch_can_finalize_failed_without_set()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(PhotographyUploadFileOutcome.Rejected(
            operation.PhotographyUploadOperationId,
            0,
            "bad.txt",
            "file-fingerprint-0",
            "Unsupported file type."));
        operation.AddFileOutcome(PhotographyUploadFileOutcome.Failed(
            operation.PhotographyUploadOperationId,
            1,
            "broken.jpg",
            "file-fingerprint-1",
            "Image could not be processed."));

        operation.FinalizeBatch(expectedFileCount: 2);

        Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);
        Assert.Null(operation.PhotographySetId);
    }

    [Fact]
    public void Attaching_set_then_finalizing_create_set_success_succeeds()
    {
        var operation = StartCreateSetOperation();
        var setId = Guid.NewGuid();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AttachPhotographySet(setId);
        operation.FinalizeBatch(expectedFileCount: 1);

        Assert.Equal(PhotographyUploadOperationStatus.Completed, operation.Status);
        Assert.Equal(setId, operation.PhotographySetId);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void Recovered_successful_create_set_outcome_can_establish_missing_set_and_finalize()
    {
        var operation = StartCreateSetOperation();
        var setId = Guid.NewGuid();
        var originalObjectKey = ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg");
        var outcome = PhotographyUploadFileOutcome.CleanupPending(
            operation.PhotographyUploadOperationId,
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            "Cleanup pending.",
            originalObjectKey: originalObjectKey);

        operation.AddFileOutcome(outcome);
        operation.FinalizeBatch(expectedFileCount: 1);

        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, operation.Status);

        outcome.ResolveToSucceeded(
            Guid.NewGuid(),
            originalObjectKey,
            [],
            "Recovered.");

        Assert.Throws<InvalidOperationException>(() => operation.FinalizeBatch(expectedFileCount: 1));
        operation.AttachPhotographySet(setId);
        operation.AttachPhotographySet(setId);
        Assert.Throws<InvalidOperationException>(() => operation.AttachPhotographySet(Guid.NewGuid()));

        operation.FinalizeBatch(expectedFileCount: 1);

        Assert.Equal(PhotographyUploadOperationStatus.Completed, operation.Status);
        Assert.Equal(setId, operation.PhotographySetId);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void Terminal_refinalization_is_rejected_and_completed_at_is_stable()
    {
        var operation = StartCreateSetOperation();

        operation.AddFileOutcome(SuccessfulOutcome(operation, 0));
        operation.AttachPhotographySet(Guid.NewGuid());
        operation.FinalizeBatch(expectedFileCount: 1);

        var completedAt = operation.CompletedAt;

        Assert.Throws<InvalidOperationException>(() => operation.FinalizeBatch(expectedFileCount: 1));
        Assert.Equal(PhotographyUploadOperationStatus.Completed, operation.Status);
        Assert.Equal(completedAt, operation.CompletedAt);
    }

    [Fact]
    public void Recovery_needed_can_be_finalized_after_existing_outcome_is_repaired()
    {
        var operation = StartCreateSetOperation();
        var outcome = PhotographyUploadFileOutcome.CleanupPending(
            operation.PhotographyUploadOperationId,
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            "Cleanup pending.");

        operation.AddFileOutcome(outcome);
        operation.FinalizeBatch(expectedFileCount: 1);

        outcome.ResolveToRejected("Cleanup completed; file rejected.");
        operation.FinalizeBatch(expectedFileCount: 1);

        Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);
        Assert.NotNull(operation.CompletedAt);
    }

    [Fact]
    public void Succeeded_outcome_requires_original_object_key_and_derivative_collection()
    {
        var operationId = Guid.NewGuid();

        Assert.Throws<ArgumentNullException>(() => PhotographyUploadFileOutcome.Succeeded(
            operationId,
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            Guid.NewGuid(),
            null!,
            []));

        Assert.Throws<ArgumentNullException>(() => PhotographyUploadFileOutcome.Succeeded(
            operationId,
            0,
            "image-1.jpg",
            "file-fingerprint-0",
            Guid.NewGuid(),
            ImageStorageObjectKey.Create("artifact-images/originals/image-1.jpg"),
            null!));
    }

    private static PhotographyUploadOperation StartCreateSetOperation() =>
        PhotographyUploadOperation.Start(
            "photographer-1",
            PhotographyUploadOperationKind.CreateSetUpload,
            "key-1",
            "fingerprint-1",
            Guid.NewGuid());

    private static PhotographyUploadFileOutcome SuccessfulOutcome(PhotographyUploadOperation operation, int ordinal) =>
        PhotographyUploadFileOutcome.Succeeded(
            operation.PhotographyUploadOperationId,
            ordinal,
            $"image-{ordinal}.jpg",
            $"file-fingerprint-{ordinal}",
            Guid.NewGuid(),
            ImageStorageObjectKey.Create($"artifact-images/originals/image-{ordinal}.jpg"),
            [
                ImageStorageObjectKey.Create($"artifact-images/thumbnails/image-{ordinal}.jpg"),
                ImageStorageObjectKey.Create($"artifact-images/previews/image-{ordinal}.jpg")
            ]);
}

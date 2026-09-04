# Data Model: Feature 003 - Artifact Photography & Image Stewardship

## PhotographyRequest

Represents a request by an authorized museum user/workflow for Photography to photograph an existing Artifact.

**Fields**:

- `PhotographyRequestId`
- `ArtifactId` - required reference to Feature 001 Artifact
- `Purpose` - `GeneralDocumentation`, `PreMaintenance`, `DuringMaintenance`, `PostMaintenance`
- `RequestedByUserId`
- `RequestedAt`
- `Status` - `Pending`, `Completed`, `Cancelled`
- `FulfillingPhotographySetId` - required only when Completed
- `CompletedByUserId`
- `CompletedAt`
- `CancelledByUserId`
- `CancelledAt`
- `ConcurrencyToken`

**Rules**:

- Newly created requests start Pending.
- Valid transitions are only `Pending -> Completed` and `Pending -> Cancelled`.
- Completed and Cancelled are terminal.
- Completed requires a fulfilling Photography Set with the same Artifact, same Purpose, and at least one successfully stored image.
- Completion requires `Photography.Upload`.
- The original requester may cancel their own Pending request.
- `Photography.Manage` may cancel any Pending request.
- Many Photography Requests may reference the same fulfilling Photography Set.
- Each request is completed explicitly and independently; completing one request does not auto-complete matching requests.

## PhotographySet

Represents one photography occasion/context for one Artifact.

**Fields**:

- `PhotographySetId`
- `ArtifactId` - required reference to Feature 001 Artifact
- `Purpose` - supported Photography Purpose value
- `PhotographyDate`
- `PhotographerUserId`
- `CreatedAt`
- `CreatedByUserId`
- `ConcurrencyToken`

**Rules**:

- A set belongs to exactly one existing Artifact.
- A set may contain multiple images.
- Upload-driven set creation persists a set only when at least one image is successfully stored and metadata is committed.
- A set may exist without a prior request for valid general photography.
- A set does not imply custody, movement, location, Storehouse, Documentation, or Laboratory ownership changes.

## ArtifactImage

Represents metadata for one uploaded original image associated with an existing Artifact and Photography Set.

**Fields**:

- `ArtifactImageId`
- `ArtifactId` - required reference to Feature 001 Artifact
- `PhotographySetId`
- `OriginalObjectKey`
- `OriginalFilename`
- `ContentType`
- `FileSizeBytes`
- `PixelWidth`
- `PixelHeight`
- `UploadedByUserId`
- `UploadedAt`
- `Caption` or equivalent Photography-owned metadata, if implemented
- `Status` - `Available`, `DeletePending`, `Deleted`
- `DeletionRequestedByUserId` - populated when permanent deletion intent is accepted
- `DeletionRequestedAt` - server-authoritative UTC time when permanent deletion intent is accepted
- `DeletedByUserId`
- `DeletedAt`
- `DeletionMode` - grace-period or privileged
- `DeletionReason` - required for privileged deletion only
- `ConcurrencyToken`

**Rules**:

- Every image belongs to exactly one existing Artifact.
- The image ArtifactId must match the ArtifactId of its Photography Set.
- Images do not duplicate mutable Artifact core data.
- Original binary object is immutable.
- Deleting a wrong image requires authorized permanent deletion and new upload.
- DeletePending images retain the original accepted deletion actor/time in deletion-request fields while DeletedBy/DeletedAt remain finalization metadata.
- Final deletion metadata is derived from the persisted deletion-request fields, including during internal retry/recovery after application restart.
- Legacy DeletePending rows without deletion-request attribution are incomplete and require manual attention before finalization.
- Deleted image metadata remains only for audit/history and is not a soft-delete binary-retention model.
- Deleted and DeletePending images cannot be selected as Primary Image.
- Views for ordinary staff show only available images unless an audit/recovery workflow requires otherwise.

## ArtifactPhotographyState

Represents the Artifact-level Photography state used to serialize Primary Image changes.

**Fields**:

- `ArtifactId` - primary key and required reference to Feature 001 Artifact
- `PrimaryImageId` - nullable reference to an available ArtifactImage belonging to the same Artifact
- `ConcurrencyToken`
- `UpdatedAt`
- `UpdatedByUserId`

**Rules**:

- One row exists per Artifact that has Photography state.
- `PrimaryImageId` may be null.
- SetPrimary and deletion of the current Primary Image contend on this same row.
- A Primary Image target must exist, belong to the same Artifact, and be Available.
- Deleting the current Primary Image sets `PrimaryImageId` to null and does not select a replacement.
- Stale competing writes fail and require reload/review.

## ArtifactImageDerivative

Represents a lightweight storage derivative belonging exclusively to one Artifact Image.

**Fields**:

- `ArtifactImageDerivativeId`
- `ArtifactImageId`
- `Kind` - thumbnail or preview
- `ObjectKey`
- `ContentType`
- `FileSizeBytes`
- `PixelWidth`
- `PixelHeight`
- `CreatedAt`

**Rules**:

- Derivatives never replace or mutate the original binary.
- Derivatives are used for efficient gallery/search/detail viewing.
- Permanent deletion of an Artifact Image deletes all exclusive derivatives.
- Derivative object keys are private implementation metadata and not staff-facing.

## StorageOperationRecovery

Represents an auditable recoverable inconsistency between PostgreSQL metadata and object storage.

**Fields**:

- `StorageOperationRecoveryId`
- `OperationType` - upload cleanup, delete cleanup, derivative cleanup, missing object, derivative generation
- `ArtifactId`
- `ArtifactImageId` when known
- `ObjectKeys`
- `Status` - pending, retrying, resolved, failed-needs-attention
- `FailureSummary`
- `CreatedAt`
- `LastAttemptedAt`
- `ResolvedAt`

**Rules**:

- Used only for storage consistency and operational recovery.
- Does not retain deleted binaries.
- Does not expose object keys to ordinary staff workflows.
- Storage recovery events are auditable through the existing audit infrastructure.

## PhotographyUploadOperation

Represents persisted upload idempotency for a multi-image upload command.

**Fields**:

- `PhotographyUploadOperationId`
- `ActorUserId`
- `OperationKind` - create-set-upload or append-to-set-upload
- `IdempotencyKey`
- `RequestFingerprint` - stable hash/summary of Artifact/Set/Purpose/date/photographer and file descriptors
- `ArtifactId`
- `PhotographySetId` - nullable until the first successful image establishes a new set
- `Status` - in-progress, completed, completed-with-failures, failed, recovery-needed
- `StartedAt`
- `CompletedAt`
- `LastSeenAt`

**Rules**:

- Unique constraint on `(ActorUserId, OperationKind, IdempotencyKey)`.
- Same key with same fingerprint returns the previously authoritative per-file outcomes, including after application restart.
- Same key with a different fingerprint is rejected as a conflict.
- All-invalid batches may persist the upload operation and rejected file outcomes without persisting a usable Photography Set.
- Records may be cleaned after a configurable operational retention period only when no unresolved storage recovery remains.

## PhotographyUploadFileOutcome

Represents the persisted outcome for one file in a multi-image upload operation.

**Fields**:

- `PhotographyUploadFileOutcomeId`
- `PhotographyUploadOperationId`
- `ClientFileOrdinal`
- `OriginalFilename`
- `InputFingerprint`
- `Status` - succeeded, rejected, failed, cleanup-pending, recovery-needed
- `ArtifactImageId` - populated for successful files
- `OriginalObjectKey` - stable generated key when storage was attempted
- `DerivativeObjectKeys`
- `StaffFacingMessage`
- `CreatedAt`
- `FinalizedAt`

**Rules**:

- Unique constraint on `(PhotographyUploadOperationId, ClientFileOrdinal)`.
- A succeeded outcome references exactly one available ArtifactImage.
- A rejected outcome creates no ArtifactImage and no accepted storage object.
- Failed/cleanup-pending/recovery-needed outcomes are retryable or auditable according to storage consistency rules.
- The upload response is reconstructed from these persisted outcomes.

## PostgreSQL Constraints and Indexes

- Foreign key from PhotographyRequest, PhotographySet, and ArtifactImage to the existing Artifact table.
- Foreign key from ArtifactImage to PhotographySet.
- Supporting unique constraint on `PhotographySet(PhotographySetId, ArtifactId)`.
- Composite foreign key from `ArtifactImage(PhotographySetId, ArtifactId)` to `PhotographySet(PhotographySetId, ArtifactId)` so PostgreSQL rejects an image whose ArtifactId differs from its set's ArtifactId.
- Foreign key from ArtifactImageDerivative to ArtifactImage.
- Unique constraint on `ArtifactImage(ArtifactImageId, ArtifactId)` to support same-Artifact relational checks from ArtifactPhotographyState.
- `ArtifactPhotographyState.ArtifactId` is PK/FK to Artifact.
- Composite foreign key from `ArtifactPhotographyState(PrimaryImageId, ArtifactId)` to `ArtifactImage(ArtifactImageId, ArtifactId)` when `PrimaryImageId` is not null, preventing a Primary Image from another Artifact.
- Unique object key for each stored original/derivative.
- Optional defense-in-depth index/constraint for current primary lookup, but the authoritative primary state is `ArtifactPhotographyState.PrimaryImageId`.
- Request status check constraint for `Pending`, `Completed`, `Cancelled`.
- Request completion check constraint requiring `FulfillingPhotographySetId` when Status is Completed.
- Purpose check constraint or equivalent enum mapping for the four approved purposes.
- Unique constraint on `PhotographyUploadOperation(ActorUserId, OperationKind, IdempotencyKey)`.
- Unique constraint on `PhotographyUploadFileOutcome(PhotographyUploadOperationId, ClientFileOrdinal)`.
- Concurrency token on request, set, and image rows where stale writes can corrupt state.
- Concurrency token on `ArtifactPhotographyState` for SetPrimary/DeletePrimary serialization.

## Relationships

```text
Artifact (Feature 001)
  1 -> many PhotographyRequest
  1 -> many PhotographySet
  1 -> many ArtifactImage

PhotographySet
  1 -> many ArtifactImage
  0 -> many fulfilled PhotographyRequest

ArtifactImage
  1 -> many ArtifactImageDerivative

Artifact
  0/1 -> 1 ArtifactPhotographyState

PhotographyUploadOperation
  1 -> many PhotographyUploadFileOutcome
```

## Non-Owned Data

Photography must not store or own:

- Museum Number generation or identity.
- Artifact category administration.
- Storehouse location.
- Custody or movement.
- Documentation Records/Templates.
- Laboratory maintenance records.
- Exhibition or loans data.

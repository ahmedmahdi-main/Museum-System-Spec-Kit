# Application Use-Case Contracts: Feature 003

These contracts describe application behavior and boundaries. They are not API route designs.

## CreatePhotographyRequest

**Permission**: `Photography.Request`

**Input**:

- ArtifactId
- PhotographyPurpose

**Output**:

- Created PhotographyRequest summary
- Failure if Artifact does not exist or purpose is unsupported

**Rules**:

- Request starts Pending.
- RequestedBy and RequestedAt are captured from the current actor/time.
- No Artifact core data is duplicated.
- No custody/movement/location changes occur.

## CancelPhotographyRequest

**Permission**:

- Original requester may cancel their own Pending request.
- `Photography.Manage` may cancel any Pending request.

**Input**:

- PhotographyRequestId
- ExpectedConcurrencyToken

**Output**:

- Cancelled request summary
- Not found, forbidden, conflict, or invalid-state failure

**Rules**:

- `Photography.Request` alone does not authorize cancelling another user's request.
- Completed and Cancelled requests cannot be cancelled.
- Concurrent complete/cancel attempts return one authoritative final state; stale writes are rejected.

## CreatePhotographySetWithImages

**Permission**: `Photography.Upload`

**Input**:

- ArtifactId
- PhotographyPurpose
- PhotographyDate
- PhotographerUserId
- Optional PhotographyRequestId
- IdempotencyKey
- Multiple file streams with original filenames

**Output**:

- PhotographySet summary when at least one file succeeds
- File-level results: succeeded, rejected, failed
- No persisted set when no file succeeds

**Rules**:

- Artifact must exist.
- Valid JPEG/JPG and PNG files are accepted individually.
- Invalid files are rejected individually and do not discard other valid files.
- Rejected files create no valid ArtifactImage record and no accepted storage object.
- Stored originals are immutable.
- Generated derivatives are linked to the accepted image.
- Browser retry with the same persisted idempotency key and same input returns the authoritative prior per-file results, including after application restart.
- Reuse of the same idempotency key with different input is rejected as a conflict.
- For a new set, the first successful file atomically creates the Photography Set and that file's image metadata.
- Later file successes/failures are persisted independently and do not roll back prior successful images.
- Uploading images does not complete a request unless the completion use case validates and records fulfillment.
- No custody/movement/location changes occur.

## AppendImagesToPhotographySet

**Permission**: `Photography.Upload`

**Input**:

- Existing PhotographySetId
- IdempotencyKey
- Multiple file streams with original filenames
- Optional ArtifactId/Purpose values only when used as client-side confirmation

**Output**:

- Existing PhotographySet summary
- File-level results: succeeded, rejected, failed
- Failure when the set does not exist or confirmation input conflicts with the existing set

**Rules**:

- The existing set's ArtifactId, Purpose, PhotographyDate, and Photographer identity are authoritative.
- Uploaded images may be appended only to that set.
- Conflicting Artifact/Purpose input is rejected.
- Append does not silently mutate set identity/context.
- Valid and invalid files follow the same intentional partial-success behavior as create-with-upload.
- Per-file persistence/cleanup boundaries prevent one later failed file from rolling back prior successful files.
- Retry behavior is persisted through the same upload operation idempotency model.
- No custody/movement/location changes occur.

## CompletePhotographyRequest

**Permission**: `Photography.Upload`

**Input**:

- PhotographyRequestId
- FulfillingPhotographySetId
- ExpectedConcurrencyToken

**Output**:

- Completed request summary
- Forbidden, conflict, invalid-state, or invalid-fulfillment failure

**Rules**:

- Request must be Pending.
- Fulfilling set must belong to the same Artifact as the request.
- Fulfilling set must have the same Photography Purpose as the request.
- Fulfilling set must contain at least one successfully stored Artifact Image.
- Multiple requests may reference the same fulfilling set when each request is explicitly completed and independently satisfies these rules.
- Completing one request does not auto-complete other matching Pending requests.
- `Photography.Request` alone does not authorize completion.
- `Photography.Manage` alone does not authorize completion unless the user also has `Photography.Upload`.
- Completed is terminal.
- No custody/movement/location changes occur.

## ViewArtifactImages

**Permission**: `Photography.View`

**Input**:

- ArtifactId
- Optional image/derivative selection

**Output**:

- Available images and staff-safe viewing access
- Controlled unavailable result if binary is missing

**Rules**:

- Viewing is not restricted to photographers.
- Viewing does not grant upload, manage, or delete rights.
- Staff do not see bucket names, object keys, UUID-only storage identifiers, or permanent public URLs.
- Access is through an opaque application endpoint or application-mediated streaming by default.
- Raw MinIO presigned URLs or provider URLs must not be exposed when they reveal bucket names, object keys, provider endpoints, or other storage internals.

## UpdateArtifactImageMetadata

**Permission**: `Photography.Manage`

**Input**:

- ArtifactImageId
- Editable Photography-owned metadata
- ExpectedConcurrencyToken

**Output**:

- Updated image summary

**Rules**:

- Metadata edits do not mutate the original binary.
- Artifact identity, custody, movement, and Documentation data are not changed.
- Material changes are audited.

## SetPrimaryArtifactImage

**Permission**: `Photography.Manage`

**Input**:

- ArtifactId
- ArtifactImageId
- ExpectedConcurrencyToken or expected current primary state

**Output**:

- Updated primary-image summary
- Conflict if stale concurrent write occurs

**Rules**:

- Image must exist.
- Image must be available and not permanently deleted.
- Image must belong to the same Artifact.
- At most one current Primary Image may exist per Artifact.
- Selecting an image from another Artifact is blocked.
- SetPrimary contends on the Artifact-level Photography state row and rejects stale competing writes.
- SetPrimary racing with deletion of the same/current image cannot leave a deleted image as Primary.

## DeleteArtifactImageByUploaderGrace

**Permission**: `Photography.Upload`

**Input**:

- ArtifactImageId
- ExpectedConcurrencyToken

**Output**:

- Deleted image audit summary
- Forbidden, conflict, or expired-grace failure

**Rules**:

- Current user must have `Photography.Upload` at deletion time.
- Current user must be the original uploader.
- UploadedAt is server-generated.
- No more than 60 minutes may have elapsed since upload according to server-authoritative UTC time.
- Client-provided timestamps are never trusted for deletion authorization.
- Manually entered deletion reason is not required.
- Audit records the grace-period correction rule.
- If the image is current Primary Image, deletion leaves the Artifact with no Primary Image.
- Original and exclusive derivatives are permanently deleted.
- Accepted deletion intent persists the original actor and server time before binary deletion; restart/internal recovery uses that persisted attribution for final DeletedBy/DeletedAt values.
- If storage deletion succeeds but final metadata/audit commit fails, a durable pending state is retried idempotently and full success is not reported until metadata finalization succeeds.

## DeleteArtifactImagePrivileged

**Permission**: `Photography.Delete`

**Input**:

- ArtifactImageId
- Non-empty deletion reason
- ExpectedConcurrencyToken

**Output**:

- Deleted image audit summary
- Forbidden, validation, conflict, or storage-recovery failure

**Rules**:

- Non-empty staff-facing reason is mandatory.
- Applies after the 60-minute grace period, to another user's image, or any privileged permanent deletion.
- Audit/history retains the reason and identity metadata after binary removal.
- If the image is current Primary Image, deletion leaves the Artifact with no Primary Image.
- Original and exclusive derivatives are permanently deleted.
- Accepted deletion intent persists the original actor and server time before binary deletion; restart/internal recovery uses that persisted attribution for final DeletedBy/DeletedAt values.
- If storage deletion succeeds but final metadata/audit commit fails, a durable pending state is retried idempotently and full success is not reported until metadata finalization succeeds.

## InternalStorageRecovery

**Permission**: None exposed to normal staff in Feature 003.

**Rules**:

- `StorageOperationRecovery` is an internal/system operational consistency mechanism.
- Automatic/internal retry and audit are allowed.
- Feature 003 does not expose a staff-facing manual recovery UI/use case and does not add a sixth Photography permission.
- A future manual administrator recovery UI requires a separate explicit authorization decision before exposure.

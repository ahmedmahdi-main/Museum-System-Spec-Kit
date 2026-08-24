# Research: Feature 003 - Artifact Photography & Image Stewardship

## Decision 1: MinIO Deployment on Windows Server 2019

**Decision**: Keep MinIO as the preferred object-storage provider, but treat MinIO on the current Windows Server 2019 server as a provisional deployment candidate. Production reliance requires a go/no-go PoC on the actual museum server. Do not require Docker Desktop, WSL, or a Linux VM for production.

**Provisional current-environment candidate**:

- Install MinIO as a Windows executable on the server.
- Store data on a dedicated folder on the separate `D:\` storage disk, for example `D:\MuseumObjectStorage\minio-data`.
- Ensure the data directory is dedicated to MinIO and is not manually edited by users or other services.
- Run MinIO under a Windows service wrapper such as WinSW, with service restart on failure and log capture.
- Configure MinIO endpoint, bucket, access key, secret key, TLS settings, and region through protected environment/app configuration, not code.
- Use a private bucket and an application service account with least privilege.

**Operational notes**:

- The application must tolerate MinIO process restart by returning controlled errors and retrying only idempotent operations.
- Object keys remain stable across server migration and are independent of Museum Number or mutable Artifact names.
- Direct Windows deployment is not yet an approved final production topology. Current official MinIO guidance describes Windows deployment primarily for local development/evaluation, and single-node/single-drive storage provides no additional storage reliability beyond the underlying disk.
- The single `D:\` storage disk is a single point of failure.
- MinIO is not backup.
- Backup and restore implementation is out of scope for Feature 003, but production operational acceptance must acknowledge that PostgreSQL metadata and image object storage require coordinated backup/recovery.

**Production go/no-go PoC gate**:

The Windows Server 2019 candidate must verify at minimum:

- installation without Docker, WSL, or Linux VM;
- Windows service startup and reboot auto-start;
- restart-on-failure behavior;
- persistence on `D:\`;
- dedicated storage directory ownership/access;
- TLS/network access;
- protected credentials;
- private bucket;
- upload/read/delete;
- multiple-image workload;
- representative large JPEG/PNG files;
- application restart;
- MinIO restart during or around operations;
- cleanup and `StorageOperationRecovery` behavior;
- disk-full/low-space handling where safely testable;
- migration/export feasibility to future Linux.

If the PoC fails, do not silently fall back to filesystem storage. Retain the provider abstraction, and require a separately approved object-storage provider/deployment decision or the future Linux environment before production reliance.

**Future Linux migration**:

- Keep S3-compatible object keys and metadata provider-neutral.
- Move object data with MinIO/S3 tooling such as bucket mirroring.
- Change Infrastructure configuration for endpoint/credentials/storage provider without changing Domain/Application behavior or Artifact identity.

**Sources**:

- MinIO Windows deployment docs: https://docs.min.io/aistor/installation/windows/
- MinIO community Windows deployment notes: https://minio.community/community/minio-object-store/operations/deployments/baremetal-deploy-minio-on-windows.html
- WinSW Windows service wrapper: https://winsw.github.io/
- Windows Task Scheduler restart-on-failure setting: https://learn.microsoft.com/en-us/windows/win32/taskschd/taskschedulerschema-restartonfailure-settingstype-element

## Decision 2: Storage Abstraction

**Decision**: Add an Application-facing object-storage abstraction, conceptually `IArtifactImageStorage`, with MinIO implemented only in Infrastructure.

**Required contract behavior**:

- Upload an original image stream to a generated stable object key.
- Upload derivative streams linked to the original image identity.
- Check object existence/metadata before committing image metadata.
- Open a read stream for application-mediated staff access.
- Optionally produce short-lived provider access only for future/internal use when it does not expose storage internals to staff.
- Delete an original image object.
- Delete all exclusive derivative objects for an image.
- Return structured success/failure results without exposing MinIO SDK types.
- Distinguish retryable storage failure, not-found, conflict/already-exists, and unauthorized/misconfigured provider states.

**Rationale**: Domain/Application must not depend on MinIO, object-storage paths, bucket names, permanent URLs, or OS-specific filesystem locations. Feature 003 staff UI uses opaque application endpoints/application-mediated streaming by default so bucket names, object keys, and provider endpoints are not leaked.

**Source**:

- MinIO .NET SDK operations for put/stat/get/presigned access: https://docs.min.io/aistor/developers/sdk/dotnet/api/

## Decision 3: Database/Object-Storage Consistency

**Decision**: Use explicit compensating and recoverable behavior. Do not assume PostgreSQL and object storage share a distributed transaction.

**Upload consistency**:

- Validate content before storage.
- Generate object keys before storage; keys are stable, opaque, and independent of Artifact names/Museum Number.
- Upload original and derivatives before marking an image as available.
- Persist set/image metadata in a PostgreSQL transaction after storage succeeds.
- If PostgreSQL persistence fails after storage succeeds, delete the uploaded objects immediately.
- If cleanup fails, record an auditable recoverable storage issue containing object keys, image identity if available, attempted operation, failure time, and retry state.

**Missing object prevention**:

- Application verifies the object exists before metadata is finalized.
- Viewing detects missing object despite metadata and returns a controlled unavailable result while recording a storage consistency event.
- Recovery jobs or administrative repair tools may reconcile pending cleanup/unavailable states later.

**Deletion consistency**:

- Authorize and record deletion intent before object deletion.
- Put the image into a non-viewable, non-primary-eligible pending deletion state before deleting storage objects.
- Delete original and all exclusive derivatives.
- Finalize metadata as deleted only when required object deletion succeeds.
- If one or more deletes fail, preserve a recoverable/auditable state and do not silently claim complete success.
- If original and derivative deletion succeeds but PostgreSQL final Deleted/audit commit fails, retain the durable `DeletePending` or equivalent recovery state, retry metadata/audit finalization idempotently, do not attempt to fabricate or restore the deleted binary, and do not report full successful deletion until authoritative metadata is finalized.
- Deleted metadata and audit entries remain; deleted binaries and derivatives are not intentionally retained.

**Concurrency and retries**:

- Persist upload idempotency state in PostgreSQL so retries survive application restart.
- Repeated upload requests with the same idempotency key and same request fingerprint return the existing per-file result.
- Reuse of the same idempotency key with different input is rejected as a conflict.
- Request completion/cancellation and Primary Image changes use concurrency tokens and transaction-scoped validation.

## Decision 4: Storage Capacity Model

**Decision**: Persist only the original binary plus lightweight thumbnail/preview derivatives. Avoid multiple full-resolution duplicate copies. Make maximum upload size configurable and review it during UAT against actual camera output.

**Approximate scenarios for 50,000 artifacts**:

| Scenario | Images per Artifact | Avg Original Size | Original Storage | Derivative Overhead | Approx Total |
| --- | ---: | ---: | ---: | ---: | ---: |
| Low | 3 | 3 MB | 450 GB | 2-3% | 459-464 GB |
| Moderate | 5 | 5 MB | 1,250 GB | 2-3% | 1,275-1,288 GB |
| High | 8 | 8 MB | 3,200 GB | 2-3% | 3,264-3,296 GB |

**Implications**:

- The current approximately 1 TB disk can handle a low-volume compressed JPEG scenario but is tight for moderate capture volume.
- The requested future 5 TB environment is more appropriate for sustained growth.
- A configurable maximum upload size is required; an initial deployment value around 20 MiB per original is a planning candidate pending museum camera/UAT review.
- Thumbnail and preview derivatives should be bounded and compressed to keep overhead small.
- Deletion must remove original and exclusive derivatives to avoid indefinite capacity loss.

## Decision 5: Thumbnail/Preview Strategy

**Decision**: Persist lightweight derivatives for each accepted image:

- a small thumbnail for list/grid/search views;
- a bounded preview for normal staff viewing.

**Rationale**:

- Repeatedly generating thumbnails on demand would use CPU and repeatedly read original binaries.
- Streaming originals for galleries would waste bandwidth and make day-to-day work slower.
- Stored derivatives satisfy efficient viewing while preserving original immutability.

**Rules**:

- The original uploaded binary is never overwritten by thumbnail/preview generation.
- Derivatives are linked to exactly one Artifact Image.
- Permanent deletion removes the original and all exclusive derivatives.
- Derivative generation failure is recorded as a recoverable storage/imaging issue; it must not create full-resolution duplicates.

## Decision 6: Empty Photography Set Failure Lifecycle

**Decision**: Avoid persisting uncontrolled empty Photography Sets during upload failures. For the initial feature, a Photography Set created through upload is persisted only when at least one image is successfully stored and its metadata can be committed.

**Behavior**:

- If all selected files are invalid, no Photography Set is persisted.
- If valid files fail storage or metadata persistence and no image succeeds, no Photography Set is left available to staff.
- If at least one file succeeds, the set is persisted with those successful images and file-level failures are reported.
- A completed request still requires the fulfilling set to contain at least one successfully stored image.

**Rationale**: This keeps staff workflows simple and avoids adding a draft/failed set status that is not required by the approved specification.

## Decision 7: Image Validation

**Decision**: Validate JPEG/JPG and PNG by inspecting image content/type, not by extension alone. Use a cross-platform image library behind an abstraction; ImageSharp is a candidate, not an architecture dependency. Do not use `System.Drawing.Common` because it is Windows-specific in modern .NET.

**Rules**:

- Accepted formats are exactly JPEG/JPG and PNG.
- Extension may be used as supporting information but is not authoritative.
- Rejected files create no valid Artifact Image record.
- Rejected unsupported files must not remain as accepted objects in storage.
- No TIFF, RAW, HEIC, PDF, video, image editing, cropping, filters, or enhancement tooling is planned.

**Dependency/license gate**:

- Before Tasks select a concrete image-processing package, verify package licensing is compatible with the repository/project licensing and museum deployment.
- Current ImageSharp releases use the Six Labors licensing model, so they require explicit compatibility review.
- If the selected ImageSharp version is not compatible, choose another cross-platform JPEG/PNG-capable library without changing Domain/Application contracts.
- Domain/Application contracts must not expose ImageSharp-specific types.

**Sources**:

- ImageSharp image format detection and built-in JPEG/PNG support: https://docs.sixlabors.com/articles/imagesharp/imageformats.html
- Microsoft `System.Drawing.Common` Windows-only guidance: https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only

## Decision 8: Partial Multi-Image Upload and Idempotency

**Decision**: Implement intentional partial success for multi-image upload.

**Behavior**:

- Each file is validated and processed independently.
- Valid JPEG/JPG and PNG files may succeed even when other files in the same selection fail.
- Invalid or unsupported files fail with file-level feedback.
- Failed files create no available ArtifactImage metadata and no accepted orphan object.
- A staff-facing upload response reports succeeded, rejected, and failed files individually.
- Upload requests include an idempotency key persisted in PostgreSQL so browser retry does not duplicate metadata or object keys.
- The idempotency scope is actor plus operation kind plus idempotency key, with a request fingerprint to detect same-key/different-input conflicts.
- Per-file outcome rows retain stable object identity and authoritative status for succeeded, rejected, failed, cleanup-pending, and recovery-needed outcomes.
- All-invalid batches persist the upload operation and file rejections for repeatable retry response but do not persist a usable Photography Set.
- Idempotency records can be retained for a configurable operational period and cleaned only when no unresolved storage recovery remains.

**Rationale**: Museum photographers commonly upload several angles at once; one bad file should not discard otherwise valid work.

**Per-file transaction boundaries**:

- For a new upload-driven set, the first successful image establishes and persists the Photography Set atomically with that image metadata.
- Later file successes/failures are handled independently.
- One later storage or database failure does not roll back prior successful images.
- Object cleanup/recovery is per file.
- The final response is reconstructed from persisted per-file outcomes.

## Decision 9: Primary Image Integrity

**Decision**: Enforce the optional one-primary invariant through a shared `ArtifactPhotographyState` row per Artifact, plus relational constraints and application validation.

**Rules**:

- An Artifact may have zero or one current Primary Image.
- A Primary Image target must exist, must be available/not permanently deleted, and must belong to the same Artifact.
- Selecting an image belonging to another Artifact is blocked.
- Concurrent primary selections cannot result in two current Primary Images.
- Deleting the current Primary Image is allowed under deletion rules, leaves the Artifact with no Primary Image, and does not auto-select a replacement.

**Database/concurrency strategy**:

- `ArtifactPhotographyState` has `ArtifactId` as PK/FK, nullable `PrimaryImageId`, and `ConcurrencyToken`.
- SetPrimary, SetPrimary-vs-SetPrimary, SetPrimary-vs-DeletePrimary, and DeletePrimary-vs-SetPrimary all update the same Artifact-level state row.
- The selected primary image must be loaded and validated as belonging to the same Artifact and being Available in the same transaction.
- A composite FK or equivalent relational constraint ensures `PrimaryImageId` cannot point to an image for another Artifact.
- Deleting the current Primary Image sets `PrimaryImageId` to null in the same serialized path before the image is finalized as deleted.
- Stale competing writes lose and must reload/review the latest authoritative state.
- A database uniqueness/relational constraint remains defense in depth; it is not the sole concurrency mechanism.

## Decision 10: Photography Request State

**Decision**: Model an explicit terminal state machine:

```text
Pending -> Completed
Pending -> Cancelled
```

Completed and Cancelled are terminal. Reopening is not supported.

**Completion rules**:

- Requires `Photography.Upload`.
- `Photography.Request` alone does not authorize completion.
- `Photography.Manage` alone does not authorize completion unless the user also has `Photography.Upload`.
- The fulfilling Photography Set must belong to the same Artifact as the request.
- The fulfilling Photography Set must have the same Photography Purpose as the request.
- The fulfilling Photography Set must contain at least one successfully stored Artifact Image.
- Many Photography Requests may reference the same fulfilling Photography Set when each request is explicitly completed and independently satisfies same-Artifact, same-purpose, at-least-one-image validation.
- Completing one request does not auto-complete any other request.

**Cancellation rules**:

- The original requester may cancel their own Pending request.
- A user with `Photography.Manage` may cancel any Pending request.
- `Photography.Request` alone does not authorize cancelling another user's request.
- Completed and Cancelled requests cannot be cancelled.

## Decision 11: Permissions

**Decision**: Add capability-oriented permissions to the existing permission constants, policy registration, and role preset conventions.

**Permissions**:

- `Photography.View`
- `Photography.Upload`
- `Photography.Manage`
- `Photography.Request`
- `Photography.Delete`

**Enforcement**:

- Enforcement is permission-based, not role-display-name based.
- Default role assignment intentions may grant `Photography.Delete` to Photography Supervisor and System Administrator through existing role/permission configuration.
- System Administrator should not need a special-case bypass if the existing model grants all permissions.

## Decision 12: Audit

**Decision**: Reuse the existing audit infrastructure.

**Audit coverage**:

- Image upload.
- Metadata changes.
- Primary Image change.
- Photography Request create, complete, and cancel.
- Uploader 60-minute grace-period deletion.
- Privileged `Photography.Delete` deletion with mandatory reason.
- Storage consistency issue creation, cleanup retry, and recovery.

**Deletion audit**:

- Grace-period deletion records the uploader correction rule and normal Artifact/Image/User/Timestamp identity metadata.
- Privileged deletion records the non-empty staff-facing reason.
- Audit/history retains lightweight metadata after binary and derivative deletion.

## Decision 13: Storage Recovery Scope

**Decision**: `StorageOperationRecovery` is an internal/system operational consistency mechanism in Feature 003. Do not expose a normal staff-facing manual recovery use case and do not add a sixth Photography permission.

**Rules**:

- Automatic/internal retry and audit are allowed.
- Staff workflows receive controlled unavailable/retry messages without object keys or provider details.
- If a future manual administrator recovery UI is required, it needs a separate explicit authorization decision before exposure.

## Decision 14: Server-Authoritative Time

**Decision**: Use server-generated UTC time for upload timestamps and grace-period deletion authorization.

**Rules**:

- `UploadedAt` is generated by the server.
- Grace-period deletion uses server-authoritative UTC time.
- Client-provided timestamps are never trusted for deletion authorization.
- Use the repository's existing time abstraction or .NET `TimeProvider` so boundary tests are deterministic.

## Decision 15: Testing

**Decision**: Use the existing layered test structure and add real PostgreSQL/object-storage verification for persistence and storage behavior.

**Domain tests**:

- Request state machine and terminal states.
- Request completion validation.
- Cancellation authorization decisions.
- 60-minute uploader deletion rule.
- Exactly 60 minutes versus immediately after 60 minutes.
- Permission revoked before grace expiry.
- Privileged deletion reason requirement.
- Primary Image target eligibility.

**Application tests**:

- Permission checks for View, Upload, Manage, Request, Delete.
- Partial multi-upload file-level results.
- Persistent idempotency across application restart.
- Same key/same payload retry.
- Same key/different payload conflict.
- Partial multi-upload database failure on one file without rolling back earlier successes.
- No persisted usable set after all-invalid/all-failed upload.
- Appending to an existing set rejects wrong Artifact/Purpose input and does not mutate set context.
- Request fulfillment with same Artifact/purpose/image-count validation.
- Deletion behavior for current Primary Image.
- No custody/movement/location side effects.

**PostgreSQL integration tests**:

- Partial unique index for Primary Image.
- ArtifactImage ArtifactId versus PhotographySet ArtifactId mismatch rejection through database-level invariant.
- Foreign keys to existing Artifacts.
- Request terminal-state constraints.
- Many requests referencing the same fulfilling set.
- Concurrent complete/cancel conflict.
- Concurrent Primary Image SetPrimary/SetPrimary conflict.
- Concurrent Primary Image SetPrimary/DeletePrimary conflict.

**Object-storage integration tests**:

- Upload/stat/read/delete.
- Private bucket access through application boundary.
- Original and derivative deletion.
- Missing object detection.
- Upload cleanup after metadata failure.
- Successful object deletion followed by PostgreSQL finalization/audit failure.
- Recovery finalization retry.
- Delete recovery after partial storage failure.
- Retry/idempotency behavior.

**Web acceptance tests**:

- Photographer creates set, uploads multiple files, reviews thumbnails, selects primary.
- Authorized non-Photography staff can view images but cannot manage them.
- Requester creates and cancels own Pending request.
- Photographer completes Pending request with valid set.
- Unauthorized delete/manage actions are blocked.
- Storage internals are not exposed in the UI.
- Raw MinIO bucket names, object keys, provider endpoints, and raw presigned URLs are not exposed to Web users.

**Operational PoC tests**:

- MinIO Windows Server 2019 production go/no-go checklist from Decision 1.

# UI Workflow Contracts: Feature 003

These workflows describe staff-facing behavior only. They do not prescribe page implementation.

## Photographer Upload Workflow

1. Photographer searches/selects an existing Artifact.
2. System shows essential Artifact information from Feature 001.
3. Photographer creates a Photography Set with images or opens an existing Photography Set for appending.
4. For a new set, photographer selects one of the four approved purposes, date, and photographer; for an existing set, the existing Artifact, Purpose, Photography Date, and Photographer are authoritative.
5. Photographer uploads multiple JPEG/JPG and PNG files in one operation.
6. System shows file-level results:
   - accepted files;
   - rejected invalid/unsupported files;
   - failed files that require retry.
7. System shows thumbnails/previews for accepted images.
8. Photographer may choose one accepted image as Primary Image if authorized.

**Rules**:

- No storage internals are displayed.
- Invalid files do not discard valid files.
- If no file succeeds, no uncontrolled empty set is shown as usable work.
- Appending to an existing set rejects conflicting Artifact/Purpose input and never silently changes the set context.
- Photography does not move Artifact custody/location.

## Photography Request Workflow

1. Authorized user selects an existing Artifact.
2. User creates a Photography Request with a Photography Purpose.
3. Request starts Pending and records RequestedBy/RequestedAt.
4. Photographer performs photography and uploads at least one valid image into a matching Photography Set.
5. Photographer completes the request by linking the valid fulfilling set.

**Rules**:

- Completion requires `Photography.Upload`.
- Fulfilling set must match request Artifact and Purpose.
- Fulfilling set must contain at least one successfully stored image.
- Multiple matching requests may explicitly reference the same fulfilling set; completing one does not auto-complete another.
- Completed and Cancelled requests are terminal.

## Request Cancellation Workflow

1. Original requester opens their own Pending request and cancels it, or a `Photography.Manage` user cancels any Pending request.
2. System records who cancelled and when.

**Rules**:

- `Photography.Request` alone does not allow cancelling another user's request.
- Completed requests cannot be cancelled.
- Concurrent complete/cancel attempts resolve to one final authoritative state.

## Authorized Viewing Workflow

1. Authorized staff opens an Artifact detail or Photography gallery.
2. System displays available thumbnails/previews.
3. Staff opens an image through an opaque application endpoint/application-mediated stream.

**Rules**:

- `Photography.View` permits viewing but not upload/manage/delete.
- Documentation, Laboratory, Storehouse, and other staff can view when permissioned.
- Staff do not see object keys, bucket names, raw provider endpoints, raw presigned URLs, permanent public URLs, or infrastructure details.

## Deletion Workflow

1. User requests permanent deletion of an image.
2. System determines whether the uploader grace path or privileged path applies.
3. For grace deletion, system verifies current `Photography.Upload`, original uploader identity, and upload age of no more than 60 minutes using server-authoritative UTC time.
4. For privileged deletion, system requires `Photography.Delete` and a non-empty reason.
5. System deletes original and exclusive derivatives.
6. System records audit/history metadata.

**Rules**:

- Deleting the current Primary Image is allowed when deletion authorization succeeds.
- The Artifact may have no Primary Image after deletion.
- The system does not automatically choose a replacement.
- Failed storage deletion produces a recoverable/auditable state and does not claim complete success.
- Successful storage deletion followed by metadata/audit finalization failure remains in a durable pending state for idempotent internal retry and does not claim complete success.

## Storage Recovery Workflow

Feature 003 has no normal staff-facing storage recovery workflow.

**Rules**:

- Storage recovery is internal/system operational behavior with audit.
- Staff receive controlled unavailable/retry messages when needed.
- A future manual administrator recovery UI requires a separate authorization decision.

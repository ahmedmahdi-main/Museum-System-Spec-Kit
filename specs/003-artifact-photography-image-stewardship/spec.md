# Feature Specification: Feature 003 - Artifact Photography & Image Stewardship

**Feature Branch**: `003-artifact-photography-image-stewardship`

**Created**: 2026-08-23

**Status**: Approved

**Input**: User description: "Feature 003 - Artifact Photography & Image Stewardship. Arabic business meaning: تصوير القطعة وإدارة صورها. Build the museum Photography capability around the existing central Artifact record."

## Clarifications

### Session 2026-08-23

- Q: Who may cancel a Pending Photography Request? -> A: The original requester may cancel their own Pending request, and a user with `Photography.Manage` may cancel any Pending request. `Photography.Request` alone does not grant permission to cancel another user's request, and a Completed request cannot be cancelled.
- Q: What happens when the current Primary Image is deleted? -> A: Deleting the current Primary Image is allowed when the user otherwise satisfies deletion authorization. After deletion, the Artifact may have no Primary Image; the system must not automatically choose a replacement, and an authorized Photography user may later explicitly designate another existing image as Primary.
- Q: Is a deletion reason mandatory? -> A: A manually entered deletion reason is not required for uploader 60-minute grace-period deletion, but the audit trail must record that the deletion occurred under the uploader grace-period correction rule. A non-empty staff-facing deletion reason is mandatory for deletion performed using `Photography.Delete` and remains in audit/history metadata after binary removal.
- Q: Who may complete a Pending Photography Request? -> A: Completing or fulfilling a Pending Photography Request requires `Photography.Upload`. `Photography.Request` alone does not authorize completion, and `Photography.Manage` alone is not sufficient unless the user also has `Photography.Upload`.
- Q: What data must a Photography Request retain? -> A: A Photography Request must retain Artifact, Photography Purpose, RequestedBy, RequestedAt, Status, and the fulfilling Photography Set when Completed. A newly created request starts Pending and must not duplicate mutable Artifact core data.
- Q: What makes a Photography Request fulfillment valid? -> A: The fulfilling Photography Set must belong to the same Artifact as the request, have the same Photography Purpose as the request, and contain at least one successfully stored Artifact Image before the request may become Completed.
- Q: What state transitions does a Photography Request support? -> A: Feature 003 supports only Pending -> Completed and Pending -> Cancelled. Completed and Cancelled are terminal; reopening and transitions from terminal states are not supported.
- Q: How should mixed valid and invalid multi-image uploads behave? -> A: Multi-image upload uses intentional partial success: valid JPEG/JPG and PNG files are accepted individually, invalid or unsupported files are rejected individually, staff receive file-level success/failure feedback, and rejected files do not become Artifact Image records or accepted storage objects.
- Q: What authorizes uploader grace-period deletion? -> A: The ordinary grace-period deletion path requires the current user to have `Photography.Upload` at deletion time, be the original uploader, and delete no more than 60 minutes after upload. If any condition fails, the grace path is unavailable, while `Photography.Delete` may still authorize privileged deletion with a mandatory reason.
- Q: What is deleted when permanent image deletion succeeds? -> A: Successful permanent deletion removes the original stored image binary and all storage derivatives belonging exclusively to that image, including thumbnails and previews; derivatives are not retained merely for audit.
- Q: Which images may be selected as Primary Image? -> A: An image may be designated Primary only when the Artifact Image exists, has not been permanently deleted, and belongs to the same Artifact whose Primary Image is being changed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a Photography Set and upload artifact images (Priority: P1)

A Photography staff member searches for an existing Artifact, confirms the essential central registry information, creates a Photography Set for one photography occasion, selects the allowed purpose, records the photography date and photographer, and uploads multiple JPEG/JPG or PNG images in one operation.

**Why this priority**: This is the core operational capability. The museum cannot steward artifact images until Photography can attach organized image sets to the central Artifact without creating a second artifact identity.

**Independent Test**: Use an existing ArtifactId from the central registry, create a Photography Set with purpose, date, and photographer, upload multiple valid images in one operation, and verify that each image is associated with the Artifact through the set while artifact custody and movement remain unchanged.

**Acceptance Scenarios**:

1. **Given** an Artifact exists in the central registry and the user has `Photography.Upload`, **When** the user creates a Photography Set with purpose, photography date, photographer, and multiple valid image files, **Then** the system creates one Photography Set for that Artifact and records each uploaded Artifact Image under that set.
2. **Given** the searched Museum Number or ArtifactId does not resolve to an existing Artifact, **When** the user attempts to create a Photography Set, **Then** the system prevents creation and does not create a new Artifact or duplicate artifact data.
3. **Given** an Artifact is physically held by the Laboratory, **When** Photography records a DuringMaintenance Photography Set for that Artifact, **Then** the system records the photography work without changing custody, current location, movement history, or Storehouse return state.
4. **Given** a multi-image upload contains valid image files, **When** the upload is accepted, **Then** the uploaded original image binaries are preserved as museum source files and normal metadata changes do not mutate those original binaries.
5. **Given** a multi-image upload contains a mix of valid JPEG/JPG or PNG files and invalid or unsupported files, **When** the user submits the selection, **Then** the valid files are accepted individually, invalid files are rejected individually, staff receive file-level success/failure feedback, and rejected files do not become Artifact Image records or accepted storage objects.

---

### User Story 2 - Request artifact photography from an authorized workflow (Priority: P1)

An authorized museum user from any division with the photography request capability requests photographs for an existing Artifact, and Photography later fulfills that request with a Photography Set. The original requester may cancel their own Pending request, and users with `Photography.Manage` may cancel any Pending request.

**Why this priority**: Photography must serve museum workflows such as Laboratory and Documentation without hard-coding the feature to one department or duplicating those workflows.

**Independent Test**: Create a Pending Photography Request for an existing Artifact using `Photography.Request`, fulfill it with `Photography.Upload` by linking a valid Photography Set for the same Artifact and purpose with at least one successfully stored image, verify that the request becomes Completed and points to the fulfilling set without changing custody or Documentation records, and verify the approved cancellation and terminal-state rules.

**Acceptance Scenarios**:

1. **Given** an authorized user has `Photography.Request` and selects an existing Artifact, **When** they submit a Photography Request with the supported photography purpose, **Then** the system creates a Pending request associated with that Artifact and retains the Artifact, Photography Purpose, RequestedBy, RequestedAt, and Status values.
2. **Given** a Pending Photography Request exists, **When** a user with `Photography.Upload` links a Photography Set for the same Artifact and purpose that contains at least one successfully stored Artifact Image, **Then** the request becomes Completed and is traceably associated with that Photography Set.
3. **Given** Photography performs valid general photography without a prior request, **When** the set is created, **Then** the system allows the Photography Set without requiring a Photography Request.
4. **Given** a Pending request is being cancelled while another user tries to complete it, **When** both actions are submitted, **Then** only one final state is accepted and the losing stale action must be retried after reviewing the latest request state.
5. **Given** a Pending request exists, **When** the original requester cancels their own request or a user with `Photography.Manage` cancels it, **Then** the request becomes Cancelled; **When** another user with only `Photography.Request` attempts to cancel it, **Then** cancellation is blocked.
6. **Given** a Photography Request is Completed, **When** any user attempts to cancel it, **Then** the system prevents cancellation.
7. **Given** a user has `Photography.Request` but lacks `Photography.Upload`, or has `Photography.Manage` but lacks `Photography.Upload`, **When** they attempt to complete a Pending request, **Then** completion is blocked.
8. **Given** a Pending request is linked to a Photography Set for a different Artifact, a different Photography Purpose, or no successfully stored images, **When** completion is attempted, **Then** completion is blocked and the request remains Pending.
9. **Given** a Photography Request is Completed or Cancelled, **When** any user attempts to reopen it or move it to another status, **Then** the system prevents the transition.

---

### User Story 3 - View artifact images across authorized museum roles (Priority: P1)

Authorized museum staff across divisions view available Artifact images and thumbnails from artifact-related workflows without receiving upload, management, primary-selection, or deletion rights.

**Why this priority**: Images are useful to Documentation, Laboratory, Storehouse, and other authorized staff, but viewing must not become a general image-management capability.

**Independent Test**: Grant `Photography.View` to a non-Photography staff user, verify they can inspect Artifact images, then verify they cannot upload images, edit image metadata, select the Primary Image, or delete images.

**Acceptance Scenarios**:

1. **Given** an Artifact has uploaded images and a user has `Photography.View`, **When** the user opens the artifact image view, **Then** the system displays the authorized image list and day-to-day previews without exposing storage internals.
2. **Given** a user has `Photography.View` but lacks Photography management permissions, **When** they inspect an image, **Then** they can view permitted image content but cannot change metadata, upload files, set a Primary Image, or delete images.
3. **Given** an Artifact exists with no images, **When** an authorized viewer opens its image area, **Then** the system clearly shows that no images are currently available without creating placeholder images or changing Artifact state.
4. **Given** image metadata exists but the image binary is temporarily unavailable from object storage, **When** an authorized user attempts to view it, **Then** the system reports the image availability problem without silently pretending the image does not exist.

---

### User Story 4 - Manage image metadata and the Primary Image (Priority: P1)

Photography-authorized staff review thumbnails within a Photography Set, edit Photography-owned metadata when allowed, and designate, replace, or clear through deletion the optional current Primary Image for an Artifact.

**Why this priority**: The Primary Image is the reusable visual identity for search results, artifact details, Documentation workspace, and future Laboratory screens, so it must be authoritative and consistent.

**Independent Test**: Upload several images for one Artifact, set one image as Primary, replace it with another image, delete the current Primary Image under authorized deletion rules, and verify that the Artifact never has more than one current Primary Image and may have no Primary Image until an authorized user explicitly selects another image.

**Acceptance Scenarios**:

1. **Given** an Artifact has multiple images and the user has `Photography.Manage`, **When** the user designates one image as Primary, **Then** that image becomes the only current Primary Image for the Artifact.
2. **Given** an Artifact already has a Primary Image, **When** an authorized user selects a different image as Primary, **Then** the previous image is no longer Primary and the selected image becomes Primary without duplicating the image file.
3. **Given** two authorized users concurrently attempt to set different Primary Images for the same Artifact, **When** both submit changes, **Then** the final authoritative state contains only one Primary Image and stale conflicting writes are not silently accepted.
4. **Given** one user attempts to delete an image while another user attempts to select it as Primary, **When** both actions race, **Then** the system prevents an inconsistent state where a deleted image remains the current Primary Image.
5. **Given** the current Primary Image is deleted by a user who satisfies the applicable deletion authorization rule, **When** deletion succeeds, **Then** the Artifact has no Primary Image and the system does not automatically select a replacement.
6. **Given** a user attempts to select an image that does not exist, has been permanently deleted, or belongs to another Artifact, **When** they designate it as Primary, **Then** the system blocks the selection.

---

### User Story 5 - Permanently delete images under controlled rules (Priority: P2)

An ordinary photographer with current `Photography.Upload` can correct accidental uploads by permanently deleting their own recently uploaded image within 60 minutes, while older, other-user, or post-revocation image deletion requires the privileged `Photography.Delete` capability and remains auditable after the binary is removed.

**Why this priority**: Storage capacity is finite for a collection of approximately 50,000 artifacts, so real deletion is required, but deletion must not undermine stewardship, authorization, or auditability.

**Independent Test**: Exercise deletion attempts by the uploader with and without current `Photography.Upload` at 59 minutes, by the same uploader after 60 minutes, by another ordinary photographer, and by a `Photography.Delete` holder, then verify binary and derivative removal plus retained audit metadata for each permitted deletion.

**Acceptance Scenarios**:

1. **Given** an ordinary photographer uploaded an image 59 minutes ago and still has `Photography.Upload`, **When** the same user permanently deletes that image, **Then** the system permits deletion under the uploader grace-period rule without requiring a manually entered deletion reason and records an auditable deletion event identifying the grace-period correction rule.
2. **Given** an ordinary photographer uploaded an image more than 60 minutes ago, **When** the same user attempts deletion without `Photography.Delete`, **Then** the system prevents deletion.
3. **Given** an ordinary photographer attempts to delete another photographer's image, **When** they lack `Photography.Delete`, **Then** the system prevents deletion regardless of image age.
4. **Given** a user has `Photography.Delete`, **When** they delete an older image with a non-empty staff-facing deletion reason, **Then** the system permanently removes the image object and retains lightweight audit metadata sufficient to trace the operation and reason.
5. **Given** a user has `Photography.Delete` but provides no deletion reason, **When** they attempt privileged deletion, **Then** the system prevents deletion until a non-empty staff-facing reason is provided.
6. **Given** the target image is the current Primary Image and the user satisfies the applicable deletion authorization rule, **When** deletion succeeds, **Then** the Artifact may have no Primary Image and the system does not automatically select a replacement.
7. **Given** the original uploader's `Photography.Upload` capability has been revoked before the 60-minute period expires and the user lacks `Photography.Delete`, **When** they attempt grace-period deletion, **Then** the system prevents deletion.
8. **Given** permanent deletion succeeds, **When** the image has generated thumbnails or previews belonging exclusively to it, **Then** the system removes the original binary and those derivatives and retains only audit/history metadata.

---

### User Story 6 - Preserve storage consistency, auditability, and deployment independence (Priority: P2)

Museum administrators and auditors can trust that image metadata, stored binaries, permissions, audit events, and future backup/recovery planning remain coherent across current Windows Server deployment and future portable object-storage deployments.

**Why this priority**: Image stewardship introduces binary storage outside the central database, so the feature must protect referential consistency and auditability without tying business behavior to MinIO, Docker, or a Windows-only filesystem path.

**Independent Test**: Simulate upload, metadata persistence, deletion, and storage failures, then verify the system either completes the operation consistently or leaves a recoverable and auditable state without orphaned image binaries, missing referenced objects, or hidden storage errors.

**Acceptance Scenarios**:

1. **Given** object storage accepts an uploaded image but image metadata persistence fails, **When** the operation completes unsuccessfully, **Then** the system does not leave an untracked orphan object without a recoverable cleanup or audit path.
2. **Given** image metadata would point to an object that cannot be confirmed in storage, **When** the operation is detected, **Then** the system does not silently accept the inconsistent reference.
3. **Given** database deletion is recorded but object deletion unexpectedly fails, **When** the failure is detected, **Then** the system exposes a recoverable and auditable state rather than presenting the deletion as fully complete without qualification.
4. **Given** the museum later moves from Windows Server 2019 storage arrangements to a Linux deployment or another object-storage provider, **When** Photography images are migrated, **Then** Artifact identity and Photography business meaning remain unchanged.
5. **Given** permanent deletion of an image succeeds for metadata but deletion of the original binary or one of its exclusive derivatives fails, **When** the failure is detected, **Then** the system follows the recoverable and auditable storage consistency requirement rather than silently claiming complete success.

### Edge Cases

- Museum Number or ArtifactId does not exist in the central registry.
- Artifact exists but has no images.
- Artifact already has a Primary Image when a new Primary Image is selected.
- Two users attempt simultaneous Primary Image changes for the same Artifact.
- Upload includes an unsupported format.
- A multi-image upload contains a mix of valid and invalid files and must complete with file-level partial success for valid files.
- Storage upload succeeds but metadata persistence fails.
- Metadata operation succeeds or fails inconsistently with object-storage operation.
- Duplicate or retried upload attempts occur after an uncertain result.
- Ordinary photographer tries deleting another photographer's image.
- Ordinary uploader tries deleting their own image at 59 minutes after upload.
- Ordinary uploader tries deleting their own image at 59 minutes after upload after losing `Photography.Upload`.
- Ordinary uploader tries deleting their own image after 60 minutes have elapsed.
- `Photography.Delete` holder deletes an older image.
- Permanent deletion succeeds for the original image but fails for an exclusive thumbnail/preview derivative.
- Deletion targets the current Primary Image and leaves the Artifact with no Primary Image.
- Deletion races with viewing or primary-image selection.
- Pending Photography Request is fulfilled.
- A user with `Photography.Request` but not `Photography.Upload` tries to complete a Pending request.
- A user with `Photography.Manage` but not `Photography.Upload` tries to complete a Pending request.
- A Pending request is fulfilled by a Photography Set for a different Artifact.
- A Pending request is fulfilled by a Photography Set with a different Photography Purpose.
- A Pending request is fulfilled by a Photography Set with no successfully stored Artifact Images.
- A Completed or Cancelled request is reopened or moved to another status.
- Photography Set is created without a prior request.
- The original requester cancels their own Pending Photography Request.
- A `Photography.Manage` holder cancels any Pending Photography Request.
- A user with only `Photography.Request` tries to cancel another user's Pending request.
- A user tries to cancel a Completed Photography Request.
- Request is cancelled while another user tries to complete it.
- Uploader grace-period deletion occurs without a manually entered deletion reason.
- Privileged `Photography.Delete` deletion is attempted without a non-empty deletion reason.
- A Primary Image selection targets a missing, permanently deleted, or different-Artifact image.
- Laboratory custody remains unchanged while DuringMaintenance images are captured.
- Authorized non-Photography staff view images but cannot manage them.
- Image binary is unavailable from object storage while metadata exists.
- Future migration of storage provider or operating system occurs without changing Artifact identity or Photography business meaning.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST consume the existing central Artifact record as the only Artifact identity used for Photography.
- **FR-002**: Every Photography Set, Photography Request, and Artifact Image MUST be associated with an existing ArtifactId from the central registry.
- **FR-003**: The feature MUST NOT create a new Artifact identity, duplicate Museum Number ownership, duplicate artifact category ownership, or copy mutable core Artifact data into Photography as a competing source of truth.
- **FR-004**: The feature MUST display essential Artifact information from the central registry when staff select an Artifact for Photography work, sufficient for staff to confirm they selected the correct Artifact.
- **FR-005**: The feature MUST NOT allow Photography users to create Artifacts, generate Museum Numbers, edit core Artifact data, administer artifact categories, manage Storehouse locations, or own custody/movement workflows.
- **FR-006**: Photography activity MUST NOT automatically transfer custody, create a movement, change current location, or return an Artifact to Storehouse.
- **FR-007**: The feature MUST allow Photography work for an Artifact regardless of which authorized division physically holds the Artifact, provided the user is authorized for the Photography action.
- **FR-008**: The feature MUST distinguish Photography purpose from physical custody/movement state in user-facing status and actions.
- **FR-009**: The feature MUST provide a Photography Set concept representing one photography occasion or context for one Artifact.
- **FR-010**: A Photography Set MUST contain exactly one Artifact association, one Purpose, one Photography Date, and one Photographer value.
- **FR-011**: A Photography Set MAY contain multiple Artifact Images.
- **FR-012**: The feature MUST support uploading multiple images in one operation to a Photography Set.
- **FR-013**: The initial supported Photography Purpose values MUST be exactly `GeneralDocumentation`, `PreMaintenance`, `DuringMaintenance`, and `PostMaintenance`.
- **FR-014**: The feature MUST NOT add speculative Photography Purpose values such as exhibition, loan, damage report, publication, archive export, or other future workflows in Feature 003.
- **FR-015**: The purpose model MUST remain conceptually extensible for later workflows without implementing those workflows in Feature 003.
- **FR-016**: The feature MUST include Photography Requests that allow authorized museum workflows or users to request photography for an existing Artifact.
- **FR-017**: Creating a Photography Request MUST require `Photography.Request`.
- **FR-018**: Photography Request authorization MUST be capability-based and MUST NOT be hard-coded to Laboratory or any other division.
- **FR-019**: Photography Request status values in Feature 003 MUST be limited to Pending, Completed, and Cancelled.
- **FR-020**: The feature MUST NOT include request approval chains, assignment workflow, SLA engines, complex queue states, or notification systems.
- **FR-021**: A Completed Photography Request MUST be traceably associated with the Photography Set that fulfilled it.
- **FR-022**: A Photography Set MAY exist without a prior Photography Request when Photography performs valid general photography.
- **FR-023**: A Pending Photography Request MAY be cancelled by its original requester or by a user with `Photography.Manage`; `Photography.Request` alone MUST NOT authorize cancelling another user's request, and a Completed request MUST NOT be cancelled.
- **FR-024**: Authorized museum staff with `Photography.View` MUST be able to view permitted Artifact images across divisions.
- **FR-025**: Viewing images MUST NOT be restricted only to photographers.
- **FR-026**: `Photography.View` MUST NOT imply upload, management, primary-selection, or deletion rights.
- **FR-027**: Uploading Artifact images MUST require `Photography.Upload`.
- **FR-028**: Creating Photography Sets for upload MUST require `Photography.Upload`.
- **FR-029**: Editing Photography-owned metadata and organizing normal Photography-owned information MUST require `Photography.Manage`.
- **FR-030**: Designating or replacing an Artifact's Primary Image MUST require `Photography.Manage`.
- **FR-031**: Privileged permanent deletion beyond the uploader 60-minute grace rule MUST require `Photography.Delete`.
- **FR-032**: The feature MUST define and reuse capability-oriented permissions consistent with the existing Museum-System authorization model: `Photography.View`, `Photography.Upload`, `Photography.Manage`, `Photography.Request`, and `Photography.Delete`.
- **FR-033**: The feature MUST NOT redesign authentication or authorization infrastructure.
- **FR-034**: Each Artifact MUST have at most one current Primary Image; an Artifact MAY have no Primary Image.
- **FR-035**: The feature MUST prevent inconsistent states where multiple images are current Primary Images for the same Artifact.
- **FR-036**: Changing an Artifact's Primary Image MUST NOT duplicate the image binary.
- **FR-037**: Primary Image state MUST be reusable by other authorized screens such as artifact search/results, artifact details, Documentation workspace, and future Laboratory screens.
- **FR-038**: Deleting the current Primary Image MUST be allowed when the requesting user satisfies the applicable deletion authorization rule; after deletion the Artifact MAY have no Primary Image, the system MUST NOT automatically choose a replacement, and an authorized Photography user MAY later explicitly designate another existing image as Primary.
- **FR-039**: The initial accepted image formats MUST be limited to JPEG/JPG and PNG.
- **FR-040**: File validation MUST verify acceptable image content or type and MUST NOT rely only on file extension.
- **FR-041**: The exact maximum file size MAY be determined during planning or configuration, but Feature 003 MUST enforce a finite maximum.
- **FR-042**: Uploaded original image binaries MUST be treated as museum source files.
- **FR-043**: Normal image editing, metadata updates, caption or description changes MUST NOT overwrite or mutate the original uploaded binary.
- **FR-044**: Replacing a wrong image MUST be handled by authorized permanent deletion according to the deletion rules and uploading the correct image as a new Artifact Image.
- **FR-045**: The feature MUST NOT include image editing, cropping, filters, AI enhancement, watermark editing, OCR, recognition, video, audio, RAW, TIFF, HEIC, PDF, or other media support.
- **FR-046**: Real permanent deletion of image binaries MUST be supported.
- **FR-047**: The feature MUST NOT implement an Excluded, soft-delete, or indefinite binary-retention model for deleted images.
- **FR-048**: An ordinary photographer MAY permanently delete an image through the uploader grace-period path only when the current user has `Photography.Upload` at deletion time, the same user uploaded that image, and no more than 60 minutes have elapsed since upload.
- **FR-049**: After more than 60 minutes have elapsed, the ordinary uploader MUST NOT be able to permanently delete the image unless they also have `Photography.Delete`.
- **FR-050**: An ordinary photographer MUST NOT permanently delete another user's image using the uploader grace-period privilege.
- **FR-051**: Permanent deletion after the uploader grace period, and permanent deletion by users other than the uploader, MUST require `Photography.Delete`.
- **FR-052**: Permanent deletion MUST remove the actual original image object from image storage when the deletion operation succeeds.
- **FR-053**: Permanent deletion MUST remain auditable even though the binary file is removed.
- **FR-054**: Deletion audit/history MUST retain enough lightweight metadata to answer which Artifact or image was deleted, who deleted it, when, the original filename or stable image identity, whether deletion used the uploader grace-period rule or `Photography.Delete`, and enough identity information to trace the deleted image operation.
- **FR-055**: A manually entered deletion reason MUST NOT be required for uploader 60-minute grace-period deletion, but deletion performed using `Photography.Delete` MUST require a non-empty staff-facing deletion reason that remains in audit/history metadata after the binary object is permanently removed.
- **FR-056**: The feature MUST NOT retain the deleted binary merely for audit purposes.
- **FR-057**: PostgreSQL MUST be used only for structured Photography and Artifact Image metadata and relationships.
- **FR-058**: Image binaries MUST be stored in object storage, with MinIO as the preferred object-storage provider for planning.
- **FR-059**: Application and domain behavior MUST remain independent of MinIO-specific business coupling by using a storage-provider abstraction owned outside the domain model.
- **FR-060**: The image bucket or container MUST be private.
- **FR-061**: Authorized users MUST access images through the application security boundary, such as secure short-lived access or application-mediated streaming.
- **FR-062**: The feature MUST NOT expose a permanently public bucket or treat public permanent object URLs as authority.
- **FR-063**: Object identity MUST be stable and independent of Museum Number, mutable Artifact names, or deployment-specific filesystem paths.
- **FR-064**: The feature MUST account for failure consistency between metadata and object-storage operations.
- **FR-065**: The system MUST NOT silently accept metadata pointing to a missing object.
- **FR-066**: The system MUST NOT silently accept an uploaded orphan object after a failed operation without a recoverable cleanup or audit path.
- **FR-067**: The system MUST NOT silently record database deletion as complete while the object remains unexpectedly undeleted without a recoverable and auditable state.
- **FR-068**: The feature SHOULD support lightweight preview or thumbnail derivatives for day-to-day viewing so full originals are not unnecessarily transferred for search or list screens.
- **FR-069**: The feature MUST NOT create multiple full-resolution duplicate copies without a documented need.
- **FR-070**: The current deployment baseline MUST account for Windows Server 2019 with a separate approximately 1 TB `D:\` storage disk, without designing product behavior around a Windows-only filesystem path.
- **FR-071**: The feature MUST remain portable to a future Linux deployment and larger storage environment without changing Artifact identity or Photography business meaning.
- **FR-072**: Docker MUST NOT be the only possible production deployment mechanism and MUST NOT become a business or application dependency.
- **FR-073**: Photography-specific sensitive actions MUST reuse the existing Museum-System audit infrastructure.
- **FR-074**: The feature MUST NOT create a second general audit subsystem.
- **FR-075**: Audit traces MUST cover image upload, Primary Image change, privileged deletion, uploader grace-period deletion, request creation, request completion, request cancellation, and material Photography metadata changes where relevant.
- **FR-076**: The feature MUST protect against conflicting concurrent actions that could corrupt Photography state, including competing Primary Image selections, deletion racing with primary selection, request completion racing with cancellation, and duplicate fulfillment or duplicate upload metadata caused by retries.
- **FR-077**: Stale conflicting writes MUST NOT be silently accepted; users must review the latest authoritative state before retrying the affected operation.
- **FR-078**: Staff-facing Photography workflows MUST hide storage internals such as object keys, bucket names, UUIDs, server paths, and provider details.
- **FR-079**: The primary staff workflow MUST support the sequence: search or select Artifact, see essential Artifact information, create or open Photography Set, choose purpose, upload multiple images, review thumbnails, choose Primary Image when appropriate, and complete a Pending Photography Request when applicable.
- **FR-080**: Feature 002 Documentation MUST remain independent: Documentation may later view Photography images when authorized, but Documentation MUST NOT upload, delete, manage images, or have image ownership retrofitted into Documentation.
- **FR-081**: Backup/restore implementation is out of scope for Feature 003, but future system backup and recovery MUST cover both PostgreSQL metadata and image object storage.
- **FR-082**: The feature MUST NOT include artifact creation, Museum Number generation, core Artifact editing, category administration, Storehouse location management, custody/movement ownership, Laboratory maintenance workflow, maintenance forms, exhibition workflow, loans, external archive integration, notifications, email/SMS, public website/gallery, image publication workflow, PDF/Word export, printing, barcode/QR, DAM enterprise features, CDN architecture, microservice extraction, authentication redesign, authorization infrastructure redesign, general audit infrastructure redesign, or backup service implementation.
- **FR-083**: A newly created Photography Request MUST start in Pending status.
- **FR-084**: A Photography Request MUST retain the following business data: Artifact, Photography Purpose, RequestedBy, RequestedAt, Status, and Fulfilling Photography Set when Completed.
- **FR-085**: Photography Request data MUST NOT duplicate mutable Artifact core data.
- **FR-086**: Completing or fulfilling a Pending Photography Request MUST require `Photography.Upload`; `Photography.Request` alone MUST NOT authorize completion, and `Photography.Manage` alone MUST NOT authorize completion unless the user also has `Photography.Upload`.
- **FR-087**: A Photography Request MUST NOT become Completed merely because an arbitrary Photography Set is linked.
- **FR-088**: Completing a Pending Photography Request MUST require the fulfilling Photography Set to belong to the same Artifact as the request.
- **FR-089**: Completing a Pending Photography Request MUST require the fulfilling Photography Set to have the same Photography Purpose as the request.
- **FR-090**: Completing a Pending Photography Request MUST require the fulfilling Photography Set to contain at least one successfully stored Artifact Image.
- **FR-091**: The Photography Request lifecycle MUST allow only Pending -> Completed and Pending -> Cancelled transitions; Completed and Cancelled are terminal, and Feature 003 MUST NOT support Completed -> Pending, Completed -> Cancelled, Cancelled -> Pending, Cancelled -> Completed, or reopening a request.
- **FR-092**: Multi-image upload MUST use intentional partial success: valid JPEG/JPG and PNG files are accepted individually, invalid or unsupported files are rejected individually, and one invalid file MUST NOT cause otherwise valid files in the same selection to be discarded.
- **FR-093**: Multi-image upload feedback MUST clearly identify which files succeeded and which files failed.
- **FR-094**: Rejected files from a multi-image upload MUST NOT become valid Artifact Image records and rejected unsupported files MUST NOT remain as accepted image objects in storage.
- **FR-095**: Successful permanent deletion MUST remove all storage derivatives belonging exclusively to the deleted image, including generated thumbnails and previews.
- **FR-096**: The system MUST NOT intentionally retain thumbnail or preview binaries for a permanently deleted image merely for audit purposes.
- **FR-097**: If deletion of the original image object or one or more exclusive derivative objects fails, the operation MUST follow the existing recoverable and auditable storage consistency requirement rather than silently claiming complete success.
- **FR-098**: An image MAY be designated as the Primary Image only when the Artifact Image exists, has not been permanently deleted, and belongs to the same Artifact whose Primary Image is being changed.
- **FR-099**: The system MUST prevent selecting an image belonging to another Artifact as the Primary Image.

### Business Rules

- **BR-001**: Artifact remains the single source of truth established by Feature 001.
- **BR-002**: Photography owns Photography Requests, Photography Sets, Artifact Image metadata, image upload/storage lifecycle, Primary Image selection state, photography-specific permissions/actions, and photography-specific audit events.
- **BR-003**: Photography does not own Artifact identity, Museum Number, artifact category, Storehouse locations, custody, movement, Documentation Records/Templates, Laboratory maintenance records, exhibition, or loans.
- **BR-004**: Photography activity and physical custody state are separate business concepts.
- **BR-005**: Actual physical transfer to Photography, when it occurs, remains the existing Feature 001 custody/movement workflow.
- **BR-006**: Initial Photography Purpose meanings are `GeneralDocumentation` for تصوير عام / توثيقي, `PreMaintenance` for قبل الصيانة, `DuringMaintenance` for أثناء الصيانة, and `PostMaintenance` for بعد الصيانة.
- **BR-007**: Ordinary staff from Documentation, Laboratory, Storehouse, or other divisions do not gain image management rights merely because they can view images.
- **BR-008**: The photographer role handles normal image upload and image management, subject to capability permissions.
- **BR-009**: Photography Supervisor / مسؤول التصوير and System Administrator / مدير النظام are intended default recipients of `Photography.Delete`, implemented through the existing permission architecture rather than display-name checks.
- **BR-010**: Deleted image binaries are not retained solely for audit purposes, but deletion operations remain traceable through lightweight metadata; privileged `Photography.Delete` deletion requires a non-empty staff-facing reason, while uploader grace-period deletion records the correction rule without requiring a manually entered reason.
- **BR-011**: Storage efficiency matters because the museum has approximately 50,000 artifacts and current storage is limited.
- **BR-012**: Arabic/RTL museum design-system conventions must be preserved when this feature is later implemented.
- **BR-013**: Photography Requests are fulfilled by actual Photography work; valid fulfillment requires a same-Artifact, same-purpose Photography Set containing at least one successfully stored Artifact Image.
- **BR-014**: Photography Request terminal states are final in Feature 003; Completed and Cancelled requests are not reopened.
- **BR-015**: Mixed multi-image upload is a partial-success workflow where valid files are preserved and invalid files are rejected with file-level feedback.
- **BR-016**: Ordinary uploader grace-period deletion is a current capability rule, not a retained right after `Photography.Upload` is revoked.
- **BR-017**: Permanent image deletion includes the original image binary and exclusive generated derivatives; audit retains metadata, not image binaries or derivatives.

### Key Entities *(include if feature involves data)*

- **Artifact**: The existing central registry item photographed by this feature; it supplies identity, Museum Number, category, and custody/location context but is not created or owned by Photography.
- **Photography Request**: A request by an authorized museum user or workflow for Photography to photograph one existing Artifact for one Photography Purpose; it retains Artifact, Photography Purpose, RequestedBy, RequestedAt, Status, and the fulfilling Photography Set when Completed. It starts Pending and may transition only to Completed or Cancelled.
- **Photography Set**: One photography occasion or context for one Artifact, containing Purpose, Photography Date, Photographer, and zero or more Artifact Images.
- **Artifact Image**: Metadata record for one uploaded image associated with a Photography Set and therefore one Artifact; it includes stable image identity, original filename or equivalent staff-facing identity, upload metadata, storage reference, format/type, storage success state relevant to fulfillment, and primary-selection eligibility.
- **Primary Image State**: The authoritative indication that at most one current Artifact Image is the Primary Image for one Artifact; an Artifact may temporarily or permanently have no Primary Image until an authorized Photography user explicitly selects one.
- **Image Storage Object**: The private stored binary object for an Artifact Image, independent of Museum Number, mutable Artifact names, and operating-system paths.
- **Image Preview/Thumbnail**: A lightweight derivative intended for day-to-day viewing where appropriate, not a replacement for the uploaded original source file, and removed when the source image is permanently deleted if the derivative belongs exclusively to that image.
- **Photography Audit Event**: Traceable event recorded through the existing audit infrastructure for sensitive Photography actions such as upload, deletion, Primary Image change, request lifecycle changes, and material metadata changes.
- **Photography Permission**: Capability-oriented authorization such as `Photography.View`, `Photography.Upload`, `Photography.Manage`, `Photography.Request`, and `Photography.Delete`, reused through the existing authorization model.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of authorized Photography staff participating in acceptance testing can locate an existing Artifact and start a Photography Set in under 1 minute after entering the search value.
- **SC-002**: 100% of tested valid multi-image uploads containing only JPEG/JPG and PNG files attach all accepted images to the selected Photography Set in one operation.
- **SC-003**: 100% of tested mixed multi-image uploads accept valid JPEG/JPG and PNG files individually, reject invalid or unsupported files individually, show file-level success/failure feedback, and do not store rejected files as valid Artifact Images or accepted image objects.
- **SC-004**: 100% of authorized viewers in Documentation, Laboratory, Storehouse, and other tested roles can inspect permitted Artifact images without gaining upload, management, primary-selection, or deletion capability.
- **SC-005**: 100% of tested Primary Image changes and Primary Image deletions leave at most one current Primary Image for the Artifact, and deletion of the current Primary Image does not automatically select a replacement.
- **SC-006**: 100% of unauthorized deletion attempts tested are prevented, including another ordinary photographer deleting someone else's image and the original uploader deleting their own image after 60 minutes without `Photography.Delete`.
- **SC-007**: 100% of tested uploader grace-period deletions behave exactly as the three-condition rule: permitted only when the current user has `Photography.Upload`, is the original uploader, and deletes at or before 60 minutes; blocked when any condition fails unless the user has `Photography.Delete`.
- **SC-008**: 100% of tested privileged deletions require a non-empty staff-facing deletion reason, remove the original image binary and exclusive derivatives from image storage, and leave auditable lightweight metadata sufficient to trace the deleted image operation and reason.
- **SC-009**: 100% of tested Photography Sets and Photography Requests leave Artifact custody, current location, movement history, and Storehouse return state unchanged unless a separate Feature 001 custody/movement workflow is executed.
- **SC-010**: 100% of tested Photography Requests can be created, completed, or cancelled only according to their allowed lifecycle, authorization rules, and fulfillment validation, without duplicating Laboratory, Documentation, or Storehouse workflow ownership.
- **SC-011**: 100% of tested metadata and storage failure scenarios either complete consistently or expose a recoverable and auditable state; no tested scenario silently leaves metadata pointing to a missing object or an untracked orphan object.
- **SC-012**: 100% of tested existing Feature 001 Artifact identity, registry, custody/movement behavior, and Feature 002 Documentation behavior remains stable after enabling Feature 003.
- **SC-013**: During future manual UAT, participating museum Photography staff confirm that the primary workflow uses familiar museum concepts and does not expose storage internals such as bucket names, object keys, UUIDs, or server paths.
- **SC-014**: 100% of tested request completion attempts require `Photography.Upload` and a fulfilling Photography Set for the same Artifact and Photography Purpose containing at least one successfully stored Artifact Image.
- **SC-015**: 100% of tested Primary Image selections are blocked when the target image is missing, permanently deleted, or belongs to another Artifact.

## Assumptions

- Feature 001 provides the central Artifact registry, ArtifactId, Museum Number, artifact category, custody/movement state, permissions infrastructure, and audit infrastructure consumed by this feature.
- Feature 002 Documentation remains complete and independent; it may later consume Photography images through viewing permissions but does not own image upload, deletion, or management.
- Users are authenticated through the existing Museum-System authentication and authorization model before using Photography capabilities.
- Photography Date represents the staff-entered date of the photography occasion; planning may determine whether time-of-day is also captured without expanding the business scope.
- Photographer is the staff-facing identity of the person responsible for the Photography Set and may default from the current user if existing conventions support that.
- Image captions or descriptions are optional unless planning confirms an existing staff need; metadata changes, if supported, do not alter the original binary.
- The exact maximum image file size is a configuration/planning decision, provided the implemented feature enforces a finite maximum.
- Secure short-lived access and application-mediated streaming are both acceptable product behaviors for authorized image access, provided the private storage and application security-boundary requirements are met.
- Preview/thumbnail derivatives may be generated for operational efficiency, but derivative generation details belong in planning and must not create unnecessary full-resolution duplicates; exclusive derivatives are deleted with their source image during successful permanent deletion.
- Backup/restore implementation is outside Feature 003, but any future museum backup/recovery design must cover both structured metadata and object storage binaries together.

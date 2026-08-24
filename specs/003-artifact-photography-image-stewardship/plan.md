# Implementation Plan: Feature 003 - Artifact Photography & Image Stewardship

**Branch**: `003-artifact-photography-image-stewardship` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-artifact-photography-image-stewardship/spec.md`

## Summary

Add a Photography module to the existing modular monolith so authorized museum staff can request artifact photography, photographers can upload and steward artifact images in Photography Sets, one optional Primary Image can be selected per Artifact, and permanent deletion remains controlled and auditable.

The design consumes the Feature 001 central Artifact identity and never changes custody, movement, location, Museum Number, category, or Storehouse state merely because photography occurs. PostgreSQL stores structured Photography metadata only. Image binaries and lightweight derivatives are stored in private object storage through an application-facing storage abstraction, with MinIO as the preferred Infrastructure provider. MinIO on the current Windows Server 2019 server is a provisional deployment candidate that requires a production go/no-go PoC before production reliance.

## Technical Context

**Language/Version**: C# on .NET 10
**Primary Dependencies**: ASP.NET Core, EF Core/Npgsql, existing authorization policies, existing audit writer, MinIO .NET SDK in Infrastructure only, cross-platform JPEG/PNG image processing selected behind an abstraction after dependency/license review
**Storage**: PostgreSQL metadata; private object storage for originals and derivatives
**Testing**: xUnit domain/application/integration tests, PostgreSQL integration tests, object-storage integration tests, Playwright web acceptance tests
**Target Platform**: Current production baseline Windows Server 2019 with approximately 1 TB on `D:\`; future Linux server with approximately 5 TB requested
**Project Type**: Modular monolith web application
**Performance Goals**: Efficient gallery/search viewing through stored lightweight derivatives; avoid unnecessary full-resolution duplication
**Constraints**: No duplicate Artifact identity, no custody/movement ownership, no raw storage internals in staff UI, no public permanent object URLs, no Docker production dependency, no distributed transaction assumption between PostgreSQL and object storage
**Scale/Scope**: Approximately 50,000 artifacts; potentially multiple images per artifact

## Constitution Check

*GATE: Must pass before Phase 0 research.*

| Principle | Result | Notes |
| --- | --- | --- |
| Museum domain integrity | PASS | Photography references existing ArtifactId and does not own Artifact identity, Museum Number, custody, movement, location, Documentation, Laboratory, exhibition, or loans. |
| Arabic-first staff workflows | PASS | Plan preserves existing Arabic/RTL design-system conventions and avoids exposing technical storage details to staff. |
| Authorization and audit by default | PASS | Reuses permission policies and audit infrastructure; adds capability-oriented Photography permissions and audit events. |
| PostgreSQL-backed consistency | PASS | Metadata remains in PostgreSQL with real constraints for primary-image uniqueness, request lifecycle, and stale-write protection. |
| Modular monolith boundaries | PASS | Adds module-level Domain/Application/Infrastructure/Web surfaces without microservice extraction. |
| Testable delivery | PASS | Plan defines domain, application, real PostgreSQL, object-storage, MinIO Windows PoC, and web acceptance coverage before tasks. |

No constitution violations identified.

## Project Structure

### Documentation

```text
specs/003-artifact-photography-image-stewardship/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
`-- contracts/
    |-- application-use-cases.md
    |-- storage-abstraction.md
    `-- ui-workflows.md
```

### Source Code

```text
src/
|-- MuseumSystem.Domain/
|   `-- Modules/Photography/
|-- MuseumSystem.Application/
|   `-- Modules/Photography/
|-- MuseumSystem.Infrastructure/
|   |-- Persistence/Configurations/
|   |-- Persistence/Migrations/
|   `-- Photography/
`-- MuseumSystem.Web/
    |-- Components/Pages/Photography/
    `-- Components/Photography/

tests/
|-- MuseumSystem.Domain.Tests/
|-- MuseumSystem.Application.Tests/
|-- MuseumSystem.Integration.Tests/
`-- MuseumSystem.Web.AcceptanceTests/
```

**Structure Decision**: Extend the existing modular monolith with Photography folders matching the current Domain/Application/Infrastructure/Web conventions used by Artifact, custody, audit, and Documentation features. Do not introduce a service boundary, separate database, or separate authentication/authorization/audit subsystem.

## Phase 0: Research

Research findings are captured in [research.md](./research.md), including:

- MinIO deployment on Windows Server 2019 as a provisional candidate requiring a production go/no-go PoC, without Docker Desktop, WSL, or a Linux VM.
- Storage abstraction contracts and failure semantics.
- PostgreSQL/object-storage consistency and recovery design.
- Storage capacity model for approximately 50,000 artifacts.
- Thumbnail/preview derivative strategy.
- Empty Photography Set failure lifecycle.
- JPEG/JPG and PNG content validation.
- Partial multi-image upload and idempotent retry behavior.
- Primary Image integrity and request state-machine protections.
- Permission, audit, and test strategy.
- Image-processing dependency/license gate.

## Phase 1: Design

Design artifacts:

- [data-model.md](./data-model.md)
- [contracts/application-use-cases.md](./contracts/application-use-cases.md)
- [contracts/storage-abstraction.md](./contracts/storage-abstraction.md)
- [contracts/ui-workflows.md](./contracts/ui-workflows.md)
- [quickstart.md](./quickstart.md)

## Architecture Decisions

### Domain Model

Photography owns these conceptual entities:

- `PhotographyRequest`
- `PhotographySet`
- `ArtifactImage`
- `ArtifactImageDerivative`
- `ArtifactPhotographyState`, one Artifact-level contention/state row with nullable Primary Image
- `PhotographyUploadOperation` and per-file upload outcome state for persistent idempotency
- `StorageOperationRecovery` or equivalent recoverable consistency record

Photography introduces these value objects/enums:

- `PhotographyPurpose`: `GeneralDocumentation`, `PreMaintenance`, `DuringMaintenance`, `PostMaintenance`
- `PhotographyRequestStatus`: `Pending`, `Completed`, `Cancelled`
- `ArtifactImageStatus`: `Available`, `DeletePending`, `Deleted`
- `ImageStorageObjectKey`
- `ImageDerivativeKind`: thumbnail and preview
- `DeletionReason`, mandatory only for privileged deletion

### Application Use Cases

Planned use cases:

- Search/select an existing Artifact for Photography workflows.
- Create Photography Request.
- Cancel Pending Photography Request.
- Create a Photography Set with uploaded images.
- Append uploaded images to an existing Photography Set without mutating set context.
- Complete Pending Photography Request with a valid fulfilling Photography Set.
- View Artifact images and previews.
- Edit Photography-owned metadata.
- Designate or replace Primary Image.
- Delete image under uploader 60-minute grace rule.
- Delete image under privileged `Photography.Delete` rule.
- Record and retry storage consistency issues internally.

### Infrastructure

Application code depends on storage and image-processing abstractions. Infrastructure provides:

- MinIO/S3-compatible object storage provider.
- Cross-platform JPEG/PNG validation and derivative generation selected only after dependency/license compatibility review.
- EF Core mappings and PostgreSQL constraints.
- Existing audit writer integration.
- Existing authorization-policy registration additions.

No MinIO SDK types, bucket names, object URLs, or OS-specific storage paths are exposed to Domain/Application or staff-facing UI. Feature 003 staff viewing uses an opaque application endpoint/application-mediated streaming by default; provider-generated short-lived access remains an internal storage capability only when it does not leak storage internals.

## Data and Consistency Strategy

PostgreSQL remains authoritative for business metadata, but binary availability is verified through object-storage operations. The plan does not assume distributed transactions between PostgreSQL and MinIO.

Upload uses persisted, per-file consistency and idempotency:

1. Validate file content as JPEG/JPG or PNG.
2. Create or reuse a `PhotographyUploadOperation` scoped by actor/operation idempotency key and request fingerprint.
3. Store original and planned derivatives under stable generated object keys per file.
4. Persist each successful file's metadata in its own PostgreSQL boundary so later file failures do not roll back prior successes.
5. For a new upload-driven set, the first successful file atomically establishes the set and image metadata; all-invalid/all-failed batches leave no usable persisted set.
6. If metadata persistence fails after object upload, immediately delete uploaded objects and record a recoverable storage issue if cleanup fails.
7. Retries after application restart return the authoritative per-file outcomes or reject conflicting reuse of the same idempotency key for different input.

Deletion uses a recoverable pending/finalized pattern:

1. Authorize deletion path.
2. Record intent and prevent new primary/viewing mutations for that image.
3. Delete original and exclusive derivatives.
4. Finalize metadata as deleted and write audit history.
5. If any storage deletion fails, keep an auditable recoverable state and do not claim complete success.
6. If storage deletion succeeds but PostgreSQL final Deleted/audit commit fails, keep the durable pending state, retry metadata/audit finalization idempotently, do not attempt to fabricate the deleted binary, and do not report full success until authoritative metadata is finalized.

Primary Image state is protected by an `ArtifactPhotographyState` row keyed by ArtifactId. SetPrimary and deleting the current Primary Image contend on that same row and concurrency token. Relational constraints ensure the selected image belongs to the same Artifact, application validation ensures it is Available, and a database uniqueness/defense-in-depth constraint prevents multiple current primaries.

Photography Request terminal states are protected by a request concurrency token, transactional reads, and database constraints for valid completed-state data.

## Security, Permissions, and Audit

Permissions:

- `Photography.View`: view authorized Artifact images.
- `Photography.Upload`: create Photography Sets as allowed, upload Artifact images, and complete Pending Photography Requests with valid fulfillment.
- `Photography.Manage`: edit Photography-owned metadata, organize Photography information, designate/change Primary Image, and cancel any Pending request.
- `Photography.Request`: create a Photography Request and cancel the user's own Pending requests only.
- `Photography.Delete`: privileged permanent deletion beyond the uploader grace rule, with a mandatory non-empty reason.

Audit uses the existing audit infrastructure for upload, metadata changes, primary-image changes, request create/complete/cancel, grace-period deletion, privileged deletion, and storage consistency recovery events.

## Testing Strategy

Test coverage will be added at these levels:

- Domain tests for request lifecycle, deletion policy, purpose matching, primary-image eligibility, and business invariants.
- Application tests for permissions, partial multi-upload, persistent idempotent retry behavior, request fulfillment validation, existing-set append validation, server-authoritative 60-minute deletion boundaries, and no custody/movement side effects.
- PostgreSQL integration tests for image/set Artifact mismatch rejection, primary-state constraints, request terminal-state checks, request-to-set many-to-one cardinality, and concurrency conflicts.
- Object-storage integration tests for upload/read/delete, private access through the application boundary, derivative deletion, upload/delete finalization failure recovery, and retry semantics.
- Web acceptance tests for staff workflows and role boundaries, including Arabic/RTL layout preservation and no exposure of raw storage internals.
- Operational PoC tests for MinIO on Windows Server 2019 before production reliance.

Test infrastructure may use containers where existing integration-test conventions do, but Docker is not a production deployment requirement.

## Complexity Tracking

No constitution violations require justification.

## Phase 2 Preview

Task generation should preserve the following order:

1. Domain model and invariants.
2. Application contracts and authorization checks.
3. Persistence mappings and PostgreSQL constraints.
4. Storage abstraction and MinIO provider.
5. Image validation/derivative generation.
6. Web workflows and acceptance tests.
7. Consistency recovery and operational quickstart verification.

`tasks.md` is intentionally not created in this planning turn.

# Tasks: Feature 003 - Artifact Photography & Image Stewardship

**Input**: Design documents from `/specs/003-artifact-photography-image-stewardship/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/application-use-cases.md`, `contracts/storage-abstraction.md`, `contracts/ui-workflows.md`, `checklists/requirements.md`, `.specify/memory/constitution.md`

**Tests**: Mandatory for this feature because upload idempotency, PostgreSQL/object-storage consistency, primary-image concurrency, deletion, permissions, and cross-module regression behavior are critical business rules.

**Primary Design Guards**:

- `ArtifactPhotographyState.PrimaryImageId` is the authoritative Artifact-level Primary Image state. Do not add an independently mutable persisted `ArtifactImage.IsPrimary` authority.
- Preserve the PostgreSQL composite invariant `ArtifactImage(PhotographySetId, ArtifactId) -> PhotographySet(PhotographySetId, ArtifactId)` with the supporting unique constraint on `PhotographySet(PhotographySetId, ArtifactId)`.
- Use `ArtifactPhotographyState` as the common concurrency point for SetPrimary/SetPrimary, SetPrimary/DeletePrimary, and DeletePrimary/SetPrimary races.
- Persist `PhotographyUploadOperation` and `PhotographyUploadFileOutcome` for idempotency across retries and application restart using a strong request fingerprint, not display filename alone.
- Staff image access must remain application-mediated and opaque; never expose raw MinIO bucket names, object keys, endpoints, or provider-specific presigned URLs to staff.
- Use exactly these permissions: `Photography.View`, `Photography.Upload`, `Photography.Manage`, `Photography.Request`, `Photography.Delete`.
- Photography must never change Artifact custody, movement, current location, Storehouse return state, Documentation ownership, or Laboratory workflow state.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel when it touches different files and does not depend on incomplete tasks.
- **[Story]**: User-story tasks only, mapped to `spec.md`.
- Every task includes an exact repository file path.

---

## Phase 1: Setup (Shared Prerequisites)

**Purpose**: Package, configuration, and repository prerequisites only.

- [x] T001 Evaluate the JPEG/PNG processing package, verify license/project compatibility, and record the selected package in specs/003-artifact-photography-image-stewardship/implementation-decisions.md
- [x] T002 Add the approved image-processing package reference only after T001 compatibility approval in src/MuseumSystem.Infrastructure/MuseumSystem.Infrastructure.csproj
- [x] T003 Add the MinIO .NET SDK package reference for Infrastructure-only usage in src/MuseumSystem.Infrastructure/MuseumSystem.Infrastructure.csproj
- [x] T004 Add Photography storage, image-size, thumbnail, preview, and idempotency retention configuration keys without secrets in src/MuseumSystem.Web/appsettings.json
- [x] T005 Add local development Photography storage configuration placeholders without secrets in src/MuseumSystem.Web/appsettings.Development.json
- [x] T006 [P] Create the Photography domain test folder marker in tests/MuseumSystem.Domain.Tests/Photography/.gitkeep
- [x] T007 [P] Create the Photography application test folder marker in tests/MuseumSystem.Application.Tests/Photography/.gitkeep
- [x] T008 [P] Create the Photography integration test folder marker in tests/MuseumSystem.Integration.Tests/Photography/.gitkeep
- [x] T009 [P] Create the Photography web acceptance test folder marker in tests/MuseumSystem.Web.AcceptanceTests/Photography/.gitkeep

---

## Phase 2: Foundational (Blocking Architecture)

**Purpose**: Core Domain/Application/Infrastructure architecture that must exist before user-story implementation.

**Critical**: No user story implementation should start until this phase is complete.

**Classification note**: Foundational tasks below are shared prerequisites for multiple stories or for the schema/migration boundary. The request-specific aggregate is intentionally deferred to US2. Persistent upload idempotency and storage recovery remain foundational because upload, deletion, recovery, PostgreSQL constraints, and migration shape depend on those entities before story implementation begins.

### Tests for Foundational Architecture

- [x] T010 [P] Add domain invariant tests for Photography purpose values and package-neutral model behavior in tests/MuseumSystem.Domain.Tests/Photography/PhotographyPurposeTests.cs
- [x] T011 [P] Add application authorization-policy tests proving only the five approved Photography permissions exist in tests/MuseumSystem.Application.Tests/Photography/PhotographyPermissionPolicyTests.cs
- [x] T012 [P] Add PostgreSQL migration/schema tests covering core Photography table creation and required indexes excluding PhotographyRequest in tests/MuseumSystem.Integration.Tests/Photography/PhotographyMigrationTests.cs
- [x] T013 [P] Add PostgreSQL tests for Artifact FKs, PhotographySet composite uniqueness, ArtifactImage composite FK, unique object keys, and idempotency uniqueness in tests/MuseumSystem.Integration.Tests/Photography/PhotographyRelationalInvariantTests.cs
- [x] T014 [P] Add PostgreSQL optimistic concurrency tests for image, set, upload operation, and ArtifactPhotographyState rows excluding PhotographyRequest in tests/MuseumSystem.Integration.Tests/Photography/PhotographyOptimisticConcurrencyTests.cs

### Implementation for Foundational Architecture

- [x] T015 [P] Create shared Photography enum and value-object definitions for purposes, image status, derivative kind, deletion mode, and object keys excluding request-specific types in src/MuseumSystem.Domain/Modules/Photography/PhotographyTypes.cs
- [x] T016 [P] Create PhotographySet aggregate with ArtifactId, purpose, photography date, photographer, and immutable set context rules in src/MuseumSystem.Domain/Modules/Photography/PhotographySet.cs
- [x] T017 [P] Create ArtifactImage aggregate with ArtifactId, PhotographySetId, immutable original metadata, deletion status, and no persisted IsPrimary authority in src/MuseumSystem.Domain/Modules/Photography/ArtifactImage.cs
- [x] T018 [P] Create ArtifactImageDerivative entity for thumbnail and preview metadata in src/MuseumSystem.Domain/Modules/Photography/ArtifactImageDerivative.cs
- [x] T019 [P] Create ArtifactPhotographyState aggregate with authoritative nullable PrimaryImageId and concurrency token in src/MuseumSystem.Domain/Modules/Photography/ArtifactPhotographyState.cs
- [x] T020 [P] Create PhotographyUploadOperation and PhotographyUploadFileOutcome entities for persistent idempotency and per-file outcomes in src/MuseumSystem.Domain/Modules/Photography/PhotographyUploadOperation.cs
- [x] T021 [P] Create StorageOperationRecovery entity for internal recoverable consistency records in src/MuseumSystem.Domain/Modules/Photography/StorageOperationRecovery.cs
- [x] T022 Create shared Photography domain service rules for set/image association, primary eligibility, deletion reason, grace-period, and server-time boundaries excluding PhotographyRequest fulfillment rules in src/MuseumSystem.Domain/Modules/Photography/PhotographyRules.cs
- [x] T023 Add shared Photography DbSet members excluding PhotographyRequest to the application persistence abstraction in src/MuseumSystem.Application/Common/Persistence/IMuseumDbContext.cs
- [x] T024 [P] Add shared non-request Photography DTOs, command records, result records, and staff-safe summary shapes in src/MuseumSystem.Application/Modules/Photography/Contracts/PhotographyDtos.cs
- [x] T025 [P] Add package-neutral image processor abstraction and structured validation/derivative result types in src/MuseumSystem.Application/Modules/Photography/Imaging/IArtifactImageProcessor.cs
- [x] T026 [P] Add package-neutral object-storage abstraction and structured storage result types in src/MuseumSystem.Application/Modules/Photography/Storage/IArtifactImageStorage.cs
- [x] T027 [P] Add upload idempotency fingerprint service using artifact/set/purpose/date/photographer, file ordinal, size, content hash, and detected media descriptors in src/MuseumSystem.Application/Modules/Photography/PhotographyUploadFingerprintService.cs
- [x] T028 Add the five approved Photography permission constants and no recovery permission in src/MuseumSystem.Application/Modules/IdentityAccess/PermissionNames.cs
- [x] T029 Register Photography permissions and role/policy metadata through the existing authorization model in src/MuseumSystem.Application/Modules/IdentityAccess/Permissions.cs
- [x] T030 Add Photography audit action names for upload, metadata, primary, request lifecycle, deletion, and storage recovery through existing audit infrastructure in src/MuseumSystem.Application/Modules/Photography/PhotographyAuditActions.cs
- [x] T031 Add shared Photography entities excluding PhotographyRequest to MuseumDbContext DbSets and model discovery in src/MuseumSystem.Infrastructure/Persistence/MuseumDbContext.cs
- [x] T032 Create EF Core core Photography mappings excluding PhotographyRequest with all shared FKs, composite FK, unique constraints, check constraints, object-key uniqueness, and concurrency tokens in src/MuseumSystem.Infrastructure/Persistence/Configurations/PhotographyConfiguration.cs
- [x] T033 Create the core Photography schema migration excluding PhotographyRequest with PostgreSQL constraints and indexes in src/MuseumSystem.Infrastructure/Persistence/Migrations/20260824000100_AddPhotographyCoreSchema.cs
- [x] T034 Update the EF model snapshot for the core Photography schema excluding PhotographyRequest in src/MuseumSystem.Infrastructure/Persistence/Migrations/MuseumDbContextModelSnapshot.cs
- [x] T035 [P] Create Infrastructure-only MinIO options with protected configuration binding and no Domain/Application dependency in src/MuseumSystem.Infrastructure/Photography/Storage/MinioArtifactImageStorageOptions.cs
- [x] T036 Implement Foundation dependency injection registrations only for Photography options/configuration binding, shared abstractions with Foundation implementations, fingerprint/idempotency services, permissions, audit action names, and core services; do not register concrete MinioArtifactImageStorage or ArtifactImageProcessor before US1 in src/MuseumSystem.Infrastructure/DependencyInjection.cs
- [x] T037 Add Foundation web route, authorization, and shared service registrations without referencing US1 pages or concrete storage/image processor implementations and without redesigning authentication in src/MuseumSystem.Web/Program.cs

**Checkpoint**: Foundation ready. Shared Domain entities, abstractions, permissions, audit names, core persistence mappings, and configuration are in place for story work without requiring PhotographyRequest.

---

## Phase 3: User Story 1 - Create a Photography Set and Upload Artifact Images (Priority: P1) MVP

**Goal**: Photography staff can select an existing Artifact, create a Photography Set for one occasion, upload multiple JPEG/JPG or PNG images with intentional partial success, and preserve valid originals and derivatives without changing custody.

**Independent Test**: Use an existing ArtifactId, create a Photography Set with purpose/date/photographer and multiple valid files, include at least one invalid file, verify file-level results, stable ArtifactImage associations through the set, immutable originals, derivatives, no raw storage internals, and unchanged custody/movement/location.

### Tests for User Story 1

- [x] T038 [P] [US1] Add domain tests for PhotographySet invariants, approved purposes, no Artifact data duplication, and no custody/movement meaning in tests/MuseumSystem.Domain.Tests/Photography/PhotographySetTests.cs
- [x] T039 [P] [US1] Add domain tests for ArtifactImage lifecycle, immutable original metadata, derivative association, and rejected-file non-record behavior in tests/MuseumSystem.Domain.Tests/Photography/ArtifactImageTests.cs
- [x] T040 [P] [US1] Add application tests for create-set upload with all-valid files, mixed partial success, all-invalid replay, and no usable set when no file succeeds in tests/MuseumSystem.Application.Tests/Photography/CreatePhotographySetWithImagesUseCaseTests.cs
- [x] T041 [P] [US1] Add application tests for append upload rejecting conflicting Artifact/Purpose input and preserving existing set context in tests/MuseumSystem.Application.Tests/Photography/AppendImagesToPhotographySetUseCaseTests.cs
- [x] T042 [P] [US1] Add application tests for persistent idempotency same key/same request, same key/conflicting request, partial-success replay, stable image identity, stable object identity, and restart replay in tests/MuseumSystem.Application.Tests/Photography/PhotographyUploadIdempotencyTests.cs
- [x] T043 [P] [US1] Add image-processing tests with actual JPEG, JPG, PNG, spoofed extension/MIME rejection, dimensions, bounded preview, thumbnail, original immutability, and configured max size in tests/MuseumSystem.Integration.Tests/Photography/ArtifactImageProcessorTests.cs
- [x] T044 [P] [US1] Add MinIO/provider integration tests for upload, stat, read, private bucket behavior, object-key stability, and derivative creation in tests/MuseumSystem.Integration.Tests/Photography/ArtifactImageStorageUploadTests.cs
- [x] T045 [P] [US1] Add PostgreSQL integration tests proving prior successful file records survive later per-file failures and rejected files create no ArtifactImage rows in tests/MuseumSystem.Integration.Tests/Photography/PhotographyUploadPersistenceTests.cs
- [x] T046 [P] [US1] Add web acceptance tests for Arabic/RTL artifact search/select, multi-image upload, intentional partial-success feedback, thumbnails, and no raw storage internals in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyUploadFlowTests.cs

### Implementation for User Story 1

- [x] T047 [US1] Implement create-set upload use case with per-file validation, first-success set creation, per-file persistence boundaries, idempotency operation reuse, and no custody changes in src/MuseumSystem.Application/Modules/Photography/CreatePhotographySetWithImagesUseCase.cs
- [x] T048 [US1] Implement append upload use case with existing set context validation, partial success, idempotency operation reuse, and no set context mutation in src/MuseumSystem.Application/Modules/Photography/AppendImagesToPhotographySetUseCase.cs
- [x] T049 [US1] Implement repository/query helpers for Artifact lookup, PhotographySet creation, ArtifactImage creation, derivatives, upload outcomes, and object existence checks in src/MuseumSystem.Application/Modules/Photography/PhotographyUploadPersistenceService.cs
- [x] T050 [US1] Implement the selected package-backed JPEG/PNG validator and derivative generator behind the abstraction in src/MuseumSystem.Infrastructure/Photography/Imaging/ArtifactImageProcessor.cs and register its concrete DI binding after implementation in src/MuseumSystem.Infrastructure/DependencyInjection.cs
- [x] T051 [US1] Implement MinIO original/derivative upload, stat, read, private-bucket error mapping, and SDK isolation in src/MuseumSystem.Infrastructure/Photography/Storage/MinioArtifactImageStorage.cs and register its concrete DI binding after implementation in src/MuseumSystem.Infrastructure/DependencyInjection.cs
- [x] T052 [US1] Implement deterministic object-key generation independent of Museum Number, Artifact names, and OS paths in src/MuseumSystem.Application/Modules/Photography/PhotographyObjectKeyFactory.cs
- [x] T053 [US1] Implement upload audit writing for accepted files, rejected files, failed files, and recovery-needed outcomes through existing audit writer in src/MuseumSystem.Application/Modules/Photography/PhotographyUploadAuditService.cs
- [x] T054 [US1] Implement staff-safe upload response mapping with no bucket, object key, endpoint, UUID-only storage identifier, or provider detail exposure in src/MuseumSystem.Application/Modules/Photography/PhotographyResponseMapper.cs
- [x] T055 [US1] Build the Blazor Photography upload workflow after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Pages/Photography/Upload.razor
- [x] T056 [US1] Build shared upload result and thumbnail components after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Photography/PhotographyUploadResults.razor
- [x] T057 [US1] Add Photography navigation entry using existing shared layout patterns after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Layout/NavMenu.razor

**Checkpoint**: US1 is independently functional and testable as the first implementation slice.

---

## Phase 4: User Story 2 - Request Artifact Photography from an Authorized Workflow (Priority: P1)

**Goal**: Authorized users can create Pending Photography Requests for existing Artifacts, cancel only according to approved rules, and Photography can complete requests with valid matching sets.

**Independent Test**: Create a Pending request with `Photography.Request`, cancel own and managed Pending requests, block unauthorized/terminal cancellation, complete with `Photography.Upload` using a same-Artifact same-purpose set containing a stored image, block invalid fulfillment, and verify custody/Documentation remain unchanged.

### Request Persistence Prerequisites for User Story 2

- [x] T058 [US2] Create PhotographyRequest aggregate with request-specific types, Pending/Completed/Cancelled lifecycle, fulfillment rules, and concurrency token in src/MuseumSystem.Domain/Modules/Photography/PhotographyRequest.cs
- [x] T059 [US2] Add PhotographyRequest DbSet to the application persistence abstraction after T058 in src/MuseumSystem.Application/Common/Persistence/IMuseumDbContext.cs
- [x] T060 [US2] Add PhotographyRequest DbSet and model discovery after T058 in src/MuseumSystem.Infrastructure/Persistence/MuseumDbContext.cs
- [x] T061 [US2] Create EF Core PhotographyRequest mapping with status constraints, FulfillingPhotographySet relationship, many-requests-per-set support, request indexes, concurrency token, and fulfillment consistency hooks after T058-T060 in src/MuseumSystem.Infrastructure/Persistence/Configurations/PhotographyRequestConfiguration.cs
- [x] T062 [US2] Create the request-specific PostgreSQL migration after T061 for PhotographyRequest table, columns, FKs, indexes, status constraints, fulfilling-set relationship, and concurrency configuration in src/MuseumSystem.Infrastructure/Persistence/Migrations/20260824000200_AddPhotographyRequestSchema.cs
- [x] T063 [US2] Update the EF model snapshot for the request-specific PhotographyRequest schema after T062 in src/MuseumSystem.Infrastructure/Persistence/Migrations/MuseumDbContextModelSnapshot.cs

### Tests for User Story 2

- [x] T064 [P] [US2] Add domain tests for PhotographyRequest lifecycle, terminal states, completion validation, cancellation rules, and request fulfillment invariants in tests/MuseumSystem.Domain.Tests/Photography/PhotographyRequestTests.cs
- [x] T065 [P] [US2] Add PostgreSQL tests after T062-T063 for request status constraints, Completed fulfilling-set requirement, many requests referencing one set, and terminal-state persistence in tests/MuseumSystem.Integration.Tests/Photography/PhotographyRequestPersistenceTests.cs
- [x] T066 [P] [US2] Add PostgreSQL concurrency tests after T062-T063 for request complete/cancel races and stale losing writes in tests/MuseumSystem.Integration.Tests/Photography/PhotographyRequestConcurrencyTests.cs
- [x] T067 [P] [US2] Add application tests for CreatePhotographyRequest authorization, existing Artifact requirement, Pending defaults, no Artifact data duplication, and audit in tests/MuseumSystem.Application.Tests/Photography/CreatePhotographyRequestUseCaseTests.cs
- [x] T068 [P] [US2] Add application tests for CancelPhotographyRequest own-Pending, Manage-any-Pending, Request-only forbidden, Completed forbidden, terminal state, conflict, and audit in tests/MuseumSystem.Application.Tests/Photography/CancelPhotographyRequestUseCaseTests.cs
- [x] T069 [P] [US2] Add application tests for CompletePhotographyRequest requiring Photography.Upload, same Artifact, same purpose, at least one available image, many requests per set, explicit independent completion, and audit in tests/MuseumSystem.Application.Tests/Photography/CompletePhotographyRequestUseCaseTests.cs

### Application and UI Implementation for User Story 2

- [x] T070 [US2] Implement CreatePhotographyRequest use case after T064-T069 with Artifact existence validation, permission check, Pending defaults, audit, and no Artifact data duplication in src/MuseumSystem.Application/Modules/Photography/CreatePhotographyRequestUseCase.cs
- [x] T071 [US2] Implement CancelPhotographyRequest use case after T064-T069 with requester/Manage authorization, expected concurrency token, terminal-state checks, and audit in src/MuseumSystem.Application/Modules/Photography/CancelPhotographyRequestUseCase.cs
- [x] T072 [US2] Implement CompletePhotographyRequest use case after T064-T069 with Upload permission, matching Artifact/Purpose, available image count validation, concurrency, terminal-state rules, and audit in src/MuseumSystem.Application/Modules/Photography/CompletePhotographyRequestUseCase.cs
- [x] T073 [US2] Implement request DTOs plus list/detail query use cases after request persistence exists with staff-safe artifact summaries from the central registry in src/MuseumSystem.Application/Modules/Photography/PhotographyRequestQueries.cs
- [x] T074 [US2] Add web acceptance tests after T070-T073 for Arabic/RTL request create, own cancellation, Manage cancellation, valid completion, invalid fulfillment display, and no Documentation/Laboratory ownership in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyRequestFlowTests.cs
- [x] T075 [US2] Build the Blazor Photography request workflow after T070-T074 and after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Pages/Photography/Requests.razor
- [x] T076 [US2] Build request create/cancel/complete components after T070-T074 and after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Photography/PhotographyRequestPanel.razor

**Checkpoint**: US2 is independently functional and testable without requiring Laboratory or Documentation workflow implementation.

---

## Phase 5: User Story 3 - View Artifact Images Across Authorized Museum Roles (Priority: P1)

**Goal**: Authorized staff can view available Artifact images and thumbnails through application-mediated access without gaining management rights or seeing storage internals.

**Independent Test**: Grant `Photography.View` to a non-Photography staff user, verify images and no-image states render, verify temporarily missing binaries produce controlled unavailable results, and verify upload/manage/primary/delete actions are unavailable.

### Tests for User Story 3

- [x] T077 [P] [US3] Add application tests for ViewArtifactImages permission boundaries, no management rights, no raw storage identifiers, no-image state, and missing-object unavailable result in tests/MuseumSystem.Application.Tests/Photography/ViewArtifactImagesUseCaseTests.cs
- [x] T078 [P] [US3] Add object-storage integration tests for application-mediated read streaming, stat/read unavailable mapping, private bucket behavior, and no raw MinIO URL exposure in tests/MuseumSystem.Integration.Tests/Photography/ArtifactImageAccessBoundaryTests.cs
- [x] T079 [P] [US3] Add web acceptance tests for Documentation/Laboratory/Storehouse viewer roles, Arabic/RTL gallery, no-image state, missing-binary message, and blocked management actions in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyGalleryFlowTests.cs
- [x] T080 [P] [US3] Add security acceptance tests proving raw bucket names, object keys, provider endpoints, and provider presigned URLs never appear in staff HTML or responses in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyStorageBoundaryTests.cs

### Implementation for User Story 3

- [x] T081 [US3] Implement ViewArtifactImages query use case with permission checks, available-image filtering, no-image response, missing-object detection, and staff-safe view models in src/MuseumSystem.Application/Modules/Photography/ViewArtifactImagesUseCase.cs
- [x] T082 [US3] Implement opaque application image streaming endpoint authorization and storage read mapping in src/MuseumSystem.Web/Components/Pages/Photography/ImageStreamEndpoint.cs
- [x] T083 [US3] Implement gallery query mapping for thumbnails, previews, unavailable states, and no storage internals in src/MuseumSystem.Application/Modules/Photography/PhotographyGalleryMapper.cs
- [x] T084 [US3] Build the Blazor Artifact image gallery after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Pages/Photography/Gallery.razor
- [x] T085 [US3] Integrate the authorized image panel into Artifact details after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Pages/Artifacts/Details.razor
- [x] T086 [US3] Add permission-aware UI state for view-only users after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Photography/PhotographyGalleryToolbar.razor

**Checkpoint**: US3 is independently functional and proves viewing does not imply image stewardship permissions.

---

## Phase 6: User Story 4 - Manage Image Metadata and the Primary Image (Priority: P1)

**Goal**: Photography managers can edit Photography-owned metadata and designate, replace, or clear through deletion the optional authoritative Primary Image using `ArtifactPhotographyState`.

**Independent Test**: Upload multiple images, update metadata, set one Primary Image, replace it, try missing/deleted/different-Artifact images, delete the current Primary Image, and verify the Artifact has at most one Primary Image or no Primary Image after deletion.

### Tests for User Story 4

- [x] T087 [P] [US4] Add domain tests for Primary Image eligibility, same-Artifact requirement, deleted image rejection, no auto replacement, and ArtifactPhotographyState authority in tests/MuseumSystem.Domain.Tests/Photography/PrimaryImageRulesTests.cs
- [ ] T088 [P] [US4] Add application tests for metadata update permission, audit, concurrency, and original binary immutability in tests/MuseumSystem.Application.Tests/Photography/UpdateArtifactImageMetadataUseCaseTests.cs
- [ ] T089 [P] [US4] Add application tests for SetPrimary requiring Photography.Manage, replacing current primary, blocking missing/deleted/different-Artifact targets, stale write conflicts, and expected audit record with Artifact, previous Primary Image, new Primary Image, acting user, and server timestamp in tests/MuseumSystem.Application.Tests/Photography/SetPrimaryArtifactImageUseCaseTests.cs
- [x] T090 [P] [US4] Add PostgreSQL tests for ArtifactPhotographyState primary FK, same-Artifact relational constraint, nullable PrimaryImageId, and absence of independent ArtifactImage.IsPrimary authority in tests/MuseumSystem.Integration.Tests/Photography/ArtifactPhotographyStatePersistenceTests.cs
- [x] T091 [P] [US4] Add PostgreSQL race tests for SetPrimary/SetPrimary, SetPrimary/DeletePrimary, and DeletePrimary/SetPrimary using the ArtifactPhotographyState concurrency token in tests/MuseumSystem.Integration.Tests/Photography/PrimaryImageConcurrencyTests.cs
- [ ] T092 [P] [US4] Add web acceptance tests for metadata editing, primary selection, primary replacement, blocked invalid primary targets, Arabic/RTL layout, and no raw storage internals in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyManagementFlowTests.cs

### Implementation for User Story 4

- [ ] T093 [US4] Implement UpdateArtifactImageMetadata use case with Photography.Manage, expected concurrency, audit, and original binary immutability in src/MuseumSystem.Application/Modules/Photography/UpdateArtifactImageMetadataUseCase.cs
- [ ] T094 [US4] Implement SetPrimaryArtifactImage use case using ArtifactPhotographyState.PrimaryImageId as the only authoritative persisted primary state and writing the existing audit event with Artifact, previous Primary Image, new Primary Image, acting user, and server timestamp through the normal audit infrastructure in src/MuseumSystem.Application/Modules/Photography/SetPrimaryArtifactImageUseCase.cs
- [ ] T095 [US4] Implement ArtifactPhotographyState query/update helpers with same-Artifact validation and concurrency conflict mapping in src/MuseumSystem.Application/Modules/Photography/ArtifactPhotographyStateService.cs
- [ ] T096 [US4] Implement Primary Image summary projection for artifact search/details and Documentation reuse without duplicating image binaries in src/MuseumSystem.Application/Modules/Photography/PrimaryImageProjectionQueries.cs
- [ ] T097 [US4] Build metadata and Primary Image management UI after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Photography/PhotographyImageManagementPanel.razor
- [ ] T098 [US4] Add Primary Image display integration to Artifact search after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Pages/Artifacts/Search.razor

**Checkpoint**: US4 is independently functional and confirms exactly one authoritative Primary Image relation per Artifact.

---

## Phase 7: User Story 5 - Permanently Delete Images Under Controlled Rules (Priority: P2)

**Goal**: Uploaders can delete their own recent images only under the 60-minute current-permission grace rule, and privileged deletion requires `Photography.Delete`, a non-empty reason, binary removal, derivative removal, and audit metadata.

**Independent Test**: Exercise deletion at 59 minutes, exactly 60 minutes, just after 60 minutes, after permission revocation, by another photographer, and by a privileged deleter with and without reason; verify originals/derivatives are removed, audit remains, and deleting current Primary leaves no Primary.

### Tests for User Story 5

- [ ] T099 [P] [US5] Add domain tests for grace-period deletion boundaries, server-authoritative UTC, current Photography.Upload requirement, uploader identity, permission revocation, privileged reason requirement, and deletion mode metadata in tests/MuseumSystem.Domain.Tests/Photography/ArtifactImageDeletionRulesTests.cs
- [ ] T100 [P] [US5] Add application tests for DeleteArtifactImageByUploaderGrace at 59 minutes, exactly 60 minutes, after 60 minutes, after Upload revocation, other-user block, audit, and current Primary clearing audit trace through existing audit infrastructure in tests/MuseumSystem.Application.Tests/Photography/DeleteArtifactImageByUploaderGraceUseCaseTests.cs
- [ ] T101 [P] [US5] Add application tests for DeleteArtifactImagePrivileged requiring Photography.Delete, mandatory non-empty reason, audit metadata, binary non-retention, derivative deletion, and current Primary clearing audit trace through existing audit infrastructure in tests/MuseumSystem.Application.Tests/Photography/DeleteArtifactImagePrivilegedUseCaseTests.cs
- [ ] T102 [P] [US5] Add object-storage integration tests for deleting original objects, thumbnails, previews, already-missing object handling, and partial derivative failure recovery in tests/MuseumSystem.Integration.Tests/Photography/ArtifactImageStorageDeletionTests.cs
- [ ] T103 [P] [US5] Add PostgreSQL integration tests for DeletePending/Deleted lifecycle, deleted image primary ineligibility, audit metadata retention, and PrimaryImageId nulling in tests/MuseumSystem.Integration.Tests/Photography/ArtifactImageDeletionPersistenceTests.cs
- [ ] T104 [P] [US5] Add web acceptance tests for grace deletion, privileged deletion with mandatory reason, blocked unauthorized deletion, current Primary deletion, Arabic/RTL deletion dialogs, and no raw storage internals in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyDeletionFlowTests.cs

### Implementation for User Story 5

- [ ] T105 [US5] Implement DeleteArtifactImageByUploaderGrace use case with current Upload permission, original uploader check, server UTC 60-minute boundary, expected concurrency, existing audit infrastructure, object deletion, and Primary clearing traceability when deleting the current Primary Image in src/MuseumSystem.Application/Modules/Photography/DeleteArtifactImageByUploaderGraceUseCase.cs
- [ ] T106 [US5] Implement DeleteArtifactImagePrivileged use case with Photography.Delete, non-empty deletion reason validation, expected concurrency, existing audit metadata, object deletion, and Primary clearing traceability when deleting the current Primary Image in src/MuseumSystem.Application/Modules/Photography/DeleteArtifactImagePrivilegedUseCase.cs
- [ ] T107 [US5] Implement shared deletion consistency service for DeletePending, object/derivative deletion, final Deleted metadata, audit finalization including Primary clearing when applicable, and no binary retention in src/MuseumSystem.Application/Modules/Photography/ArtifactImageDeletionService.cs
- [ ] T108 [US5] Implement internal retry helper for storage-deletion success followed by PostgreSQL finalization/audit failure in src/MuseumSystem.Application/Modules/Photography/ArtifactImageDeletionFinalizationService.cs
- [ ] T109 [US5] Build deletion controls and mandatory-reason UI after reading .agents/skills/frontend-design/SKILL.md and docs/design-system.md in src/MuseumSystem.Web/Components/Photography/PhotographyImageDeletionDialog.razor

**Checkpoint**: US5 is independently functional and proves controlled permanent deletion with audit and storage cleanup.

---

## Phase 8: User Story 6 - Preserve Storage Consistency, Auditability, and Deployment Independence (Priority: P2)

**Goal**: Administrators and auditors can trust that metadata, object storage, audit, retry, and deployment portability remain coherent without a distributed transaction or Windows-only assumption.

**Independent Test**: Simulate upload/object-store/metadata/delete failures and restarts, verify cleanup or durable `StorageOperationRecovery`, opaque user messages, audit events, idempotent finalization, and provider-neutral configuration.

### Tests for User Story 6

- [ ] T110 [P] [US6] Add application tests for object write succeeds/DB commit fails, cleanup succeeds, cleanup fails creating durable StorageOperationRecovery, and prior file successes surviving later failures in tests/MuseumSystem.Application.Tests/Photography/PhotographyUploadRecoveryUseCaseTests.cs
- [ ] T111 [P] [US6] Add application tests for object deletion succeeds/DB finalization fails, pending metadata recovery finalizing idempotently, and controlled not-full-success reporting in tests/MuseumSystem.Application.Tests/Photography/PhotographyDeletionRecoveryUseCaseTests.cs
- [ ] T112 [P] [US6] Add application tests for internal StorageOperationRecovery retry behavior, audit events, no sixth Photography permission, and staff-safe unavailable messages in tests/MuseumSystem.Application.Tests/Photography/StorageOperationRecoveryUseCaseTests.cs
- [ ] T113 [P] [US6] Add PostgreSQL integration tests for durable StorageOperationRecovery rows, retry state transitions, unresolved recovery retention blocking idempotency cleanup, and recovery audit metadata in tests/MuseumSystem.Integration.Tests/Photography/StorageOperationRecoveryPersistenceTests.cs
- [ ] T114 [P] [US6] Add object-storage integration tests for metadata failure cleanup, cleanup failure recovery, missing object detection, MinIO restart around operations, and provider error mapping in tests/MuseumSystem.Integration.Tests/Photography/StorageConsistencyRecoveryTests.cs
- [ ] T115 [P] [US6] Add deployment portability tests proving Domain/Application have no MinIO SDK, bucket, endpoint, Windows path, Docker, WSL, or Linux-specific dependency in tests/MuseumSystem.Integration.Tests/Photography/PhotographyInfrastructureBoundaryTests.cs
- [ ] T116 [P] [US6] Add web acceptance tests for controlled storage unavailable/retry messaging and no raw operational internals in staff workflows in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyStorageFailureFlowTests.cs

### Implementation for User Story 6

- [ ] T117 [US6] Implement upload consistency coordinator for storage-before-metadata, object-exists verification, cleanup, recovery creation, and per-file outcome finalization in src/MuseumSystem.Application/Modules/Photography/PhotographyUploadConsistencyService.cs
- [ ] T118 [US6] Implement StorageOperationRecovery internal retry use case with audit and no staff-facing recovery permission in src/MuseumSystem.Application/Modules/Photography/StorageOperationRecoveryUseCase.cs
- [ ] T119 [US6] Implement provider-neutral storage health and error classification service for controlled retry/unavailable behavior in src/MuseumSystem.Application/Modules/Photography/ArtifactImageStorageHealthService.cs
- [ ] T120 [US6] Implement MinIO retry/error translation, misconfiguration handling, and restart-safe behavior behind the storage abstraction in src/MuseumSystem.Infrastructure/Photography/Storage/MinioStorageErrorMapper.cs
- [ ] T121 [US6] Document provider-neutral migration, configuration assumptions, and future coordinated recovery requirement for PostgreSQL Photography metadata plus object-storage originals/derivatives without backup/HA implementation in docs/operations/photography-storage-provider.md

**Checkpoint**: US6 is independently functional and proves recoverable storage consistency without a distributed transaction assumption.

---

## Phase 9: Polish, Cross-Cutting Validation, and Operational Readiness

**Purpose**: Cross-story verification, regression protection, UI review, and operational acceptance tasks.

- [ ] T122 [P] Add Feature 001 custody, movement, current location, Storehouse return, Artifact identity, Museum Number, and category regression tests for Photography workflows in tests/MuseumSystem.Integration.Tests/Photography/PhotographyCustodyBoundaryRegressionTests.cs
- [ ] T123 [P] Add Feature 002 Documentation regression tests proving Documentation records/templates are unaffected and only authorized future viewing integration is projected in tests/MuseumSystem.Integration.Tests/Photography/PhotographyDocumentationBoundaryRegressionTests.cs
- [ ] T124 [P] Add authentication, permission matrix, and audit preservation regression tests for existing system behavior after Photography permissions are enabled in tests/MuseumSystem.Web.AcceptanceTests/Photography/PhotographyPermissionMatrixTests.cs
- [ ] T125 [P] Add quickstart validation tests for the end-to-end Feature 003 planning verification checklist in tests/MuseumSystem.Integration.Tests/Quickstart/PhotographyQuickstartTests.cs
- [ ] T126 Run frontend design review using .agents/skills/frontend-design-review/SKILL.md and record Arabic/RTL, design-system, minimal-click workflow, and no-conflicting-local-styling findings in specs/003-artifact-photography-image-stewardship/ui-review.md
- [ ] T127 Verify all Web/UI implementation follows .agents/skills/frontend-design/SKILL.md and docs/design-system.md, then record any required centralized component gaps in specs/003-artifact-photography-image-stewardship/ui-review.md
- [ ] T128 Create the Windows Server 2019 MinIO production go/no-go PoC checklist without marking it passed in docs/operations/photography-minio-windows-2019-poc.md
- [ ] T129 Add operational acceptance notes that MinIO on Windows Server 2019 is provisional, Docker is not production-required, Windows paths are configuration only, MinIO/object storage is not backup, single D:\ storage is not HA, restores must avoid mutually inconsistent PostgreSQL metadata and object binary points, and backup/HA implementation is out of Feature 003 scope in docs/operations/photography-storage-provider.md
- [ ] T130 Run the full Domain test suite and record Feature 003 failures or gaps in specs/003-artifact-photography-image-stewardship/verification-report.md
- [ ] T131 Run the full Application test suite and record Feature 003 failures or gaps in specs/003-artifact-photography-image-stewardship/verification-report.md
- [ ] T132 Run the full PostgreSQL/object-storage Integration test suite and record Feature 003 failures or gaps in specs/003-artifact-photography-image-stewardship/verification-report.md
- [ ] T133 Run the full Web acceptance suite and record Feature 003 failures or gaps in specs/003-artifact-photography-image-stewardship/verification-report.md
- [ ] T134 Create the Feature 003 UAT record with status Pending manual museum staff UAT unless actual staff evidence exists, covering SC-001 normal create-set plus multi-image upload elapsed time, observer/date, pass/fail/pending, and SC-013 staff comments/confirmation in specs/003-artifact-photography-image-stewardship/checklists/uat-results.md
- [ ] T135 Verify every Functional Requirement FR-001 through FR-099 and Success Criterion SC-001 through SC-015 has implementation, test, and manual UAT evidence where required in specs/003-artifact-photography-image-stewardship/verification-report.md

---

## Dependencies & Execution Order

### Phase Dependencies

Phase numbering is organizational and reflects the recommended implementation order; actual parallel execution follows this dependency graph and the `[P]` markers.

- **Phase 1 Setup**: No dependencies; package/license/configuration prerequisites.
- **Phase 2 Foundational**: Depends on Phase 1; blocks every user story.
- **US1 Upload MVP**: Depends on Phase 2; establishes storage, image processing, upload idempotency, and initial gallery data.
- **US2 Requests**: Depends on Phase 2; request persistence starts at T058-T063 after foundation without depending on US1, but completion scenarios need a valid set/image fixture from US1 tests or builders.
- **US3 Viewing**: Depends on Phase 2; can run in parallel with US1 using seeded image fixtures, but production workflow benefits from US1 upload completion.
- **US4 Metadata/Primary**: Depends on Phase 2 and requires available image fixtures; can use seeded images if US1 is not complete.
- **US5 Deletion**: Depends on Phase 2 and requires available image/derivative fixtures; primary-deletion tests also depend on US4 behavior or explicit fixtures.
- **US6 Consistency/Recovery**: Depends on Phase 2; can start once storage abstraction, idempotency, and persistence foundations exist.
- **Phase 9 Polish**: Depends on all selected user stories for the release slice.

### User Story Order

- **MVP / First Implementation Slice**: Phase 1 + Phase 2 + US1.
- **Next P1 Slice**: US2 + US3 + US4, with each independently testable using seeded fixtures.
- **P2 Slice**: US5 + US6.
- **Final Slice**: Phase 9 regression, UI review, operational readiness, and full validation.

### Foundational Blockers

- T001 image-processing package/license decision.
- T015-T022 shared Domain model and rules.
- T023-T027 shared Application persistence, storage, image-processing, and idempotency abstractions excluding PhotographyRequest.
- T028-T030 permissions and audit names.
- T031-T034 shared EF model, PostgreSQL constraints, core migration, and snapshot excluding PhotographyRequest.
- T035-T037 Foundation Infrastructure/Web registration boundaries excluding US1 concrete storage and image processor implementations.

### Parallel Opportunities

- Phase 1 folder marker tasks T006-T009 can run in parallel.
- Foundational entity/interface tasks T015-T021 and T024-T027 can run in parallel after T001 is understood.
- Story test files marked `[P]` can be written in parallel once their explicit prerequisites exist.
- Infrastructure storage implementation T051 can proceed in parallel with image processing T050 after abstractions T025-T026 exist.
- US2 request persistence tasks T059-T063 run after aggregate T058; PostgreSQL tests T065-T066 run after migration T062-T063; request use cases T070-T072 can be implemented in parallel after T064-T069.
- US3 gallery UI T084-T086 can proceed in parallel with application query T081 once view models are agreed.
- US4 metadata and primary tasks T093-T096 can proceed in parallel with UI T097-T098 after seeded fixtures exist.
- US5 grace and privileged deletion use cases T105-T106 can proceed in parallel after shared deletion rules are complete.
- US6 recovery tests T110-T116 can be developed in parallel against fake storage/provider seams.

---

## Independent Test Criteria by User Story

- **US1**: Existing Artifact selection, set creation, valid JPEG/JPG/PNG upload, mixed partial success, all-invalid behavior, immutable originals, derivative generation, idempotent retry after restart, no custody/movement/location change.
- **US2**: Request create/cancel/complete lifecycle, permissions, terminal states, completion validation for same Artifact/purpose/available image, complete/cancel race handling, no Documentation/Laboratory ownership.
- **US3**: `Photography.View` can inspect images through opaque application access, cannot manage images, sees no raw storage internals, receives controlled unavailable state when binary is missing.
- **US4**: Metadata edits require `Photography.Manage`, SetPrimary uses `ArtifactPhotographyState.PrimaryImageId`, writes the expected audit trace, blocks missing/deleted/different-Artifact targets, concurrent primary/deletion races fail stale writes safely, deletion may leave no Primary.
- **US5**: Grace deletion requires current `Photography.Upload`, same uploader, and at most 60 minutes; privileged deletion requires `Photography.Delete` and non-empty reason; originals/derivatives removed; audit metadata retained.
- **US6**: Upload/delete failure scenarios produce cleanup or durable recovery, recovery retries are idempotent, provider details remain Infrastructure-only, staff messages stay opaque, MinIO Windows deployment remains a PoC gate.

---

## Critical Coverage Map

- **Primary Image single source of truth**: T019, T032, T090, T091, T094, T095.
- **ArtifactImage/PhotographySet relational invariant**: T013, T032, T033, T045.
- **Primary concurrency**: T014, T091, T094, T095.
- **Persistent upload idempotency**: T020, T027, T032, T042, T047, T048, T117.
- **Storage consistency and recovery**: T021, T110-T114, T117-T120.
- **MinIO provider and opaque access**: T003, T035, T044, T051, T078-T080, T082, T120.
- **Permissions exactly as approved**: T011, T028, T029, T124.
- **Custody/Laboratory/Documentation boundaries**: T038, T046, T067, T074, T122, T123.
- **Image processing validation**: T001, T043, T050.
- **Audit coverage including Primary Image changes**: T030, T053, T067-T072, T088-T089, T094, T100-T101, T105-T107, T112, T118, T124.
- **Manual staff UAT for SC-001 and SC-013**: T134.
- **Windows Server 2019 MinIO PoC placement**: T128, with operational context in T129.

---

## Implementation Strategy

### MVP First: US1 Only

1. Complete Phase 1 setup and package/license gate.
2. Complete Phase 2 foundational blockers.
3. Complete US1 tests and implementation.
4. Stop and validate the upload workflow independently before broadening to requests, viewing, primary management, deletion, and recovery.

### Reviewable Slices

- **A. Domain + persistence foundation**: T010-T034.
- **B. Storage/image-processing foundation**: T001-T003, T025-T027, T035-T036, T043-T044, T050-T051.
- **C. Core upload/view workflow**: US1 then US3.
- **D. Requests + metadata + Primary Image**: US2 then US4.
- **E. Deletion + recovery**: US5 then US6.
- **F. Permissions/UI/regression/operational finalization**: T124-T129 and full verification T130-T135.

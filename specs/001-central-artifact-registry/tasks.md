# Tasks: 001-central-artifact-registry

**Input**: Design documents from `/specs/001-central-artifact-registry/`

**Prerequisites**: `spec.md`, `plan.md`, `data-model.md`, `research.md`, `quickstart.md`, `contracts/`, `.specify/memory/constitution.md`

**Scope Guard**: Phase-one Modular Monolith only. Do not add Microservices, Event Bus, CQRS/MediatR/Event Sourcing, gRPC, RabbitMQ, or optional Docker runtime work.

**Task Format**: `- [ ] T### [P?] [US?] Description with file path`

## Traceability Map

- **US1**: إنشاء الهوية المركزية للقطعة.
- **US2**: استيراد الجرد الحالي من Excel.
- **US3**: البحث السريع عن القطعة وحالتها.
- **US4**: تسليم قطعة أو مجموعة قطع إلى جهة داخلية.
- **US5**: استلام القطع العائدة إلى المخزن وتحديد موقعها.
- **US6**: الجرد والمطابقة.
- **US7**: إدارة مواقع الخزن وقاعات العرض.

## Phase 1: Foundation

**Goal**: Establish the deployable Modular Monolith skeleton, shared persistence, Identity baseline, audit foundation, and RTL Blazor shell.

**Independent Test Criteria**: `dotnet restore`, `dotnet build`, and an initial test run can execute against the empty solution; the app starts with an authenticated RTL shell and no feature code beyond foundation.

### Foundation Tests

- [x] T001 [P] Add solution smoke test that verifies all projects load in `tests/MuseumSystem.Integration.Tests/Foundation/SolutionSmokeTests.cs` (Plan Phase A, Constitution III)
- [x] T002 [P] Add authorization policy registration tests for planned permissions in `tests/MuseumSystem.Application.Tests/IdentityAccess/PermissionPolicyTests.cs` (Plan Authorization Boundaries, Constitution IX)
- [x] T003 [P] Add audit actor context unit tests in `tests/MuseumSystem.Application.Tests/Audit/AuditActorContextTests.cs` (Plan audit foundation, Constitution IX)
- [x] T004 [P] Add RTL shell acceptance placeholder test in `tests/MuseumSystem.Web.AcceptanceTests/Foundation/RtlShellTests.cs` (Plan Staff UX, Constitution IV)

### Foundation Implementation

- [x] T005 Create `Museum-System.sln` and add planned projects in `Museum-System.sln` (Plan Phase A, Constitution III)
- [x] T006 [P] Create Blazor Web App project structure in `src/MuseumSystem.Web/MuseumSystem.Web.csproj` (Plan Phase A)
- [x] T007 [P] Create domain class library structure in `src/MuseumSystem.Domain/MuseumSystem.Domain.csproj` (Plan Phase A)
- [x] T008 [P] Create application class library structure in `src/MuseumSystem.Application/MuseumSystem.Application.csproj` (Plan Phase A)
- [x] T009 [P] Create infrastructure class library structure in `src/MuseumSystem.Infrastructure/MuseumSystem.Infrastructure.csproj` (Plan Phase A)
- [x] T010 [P] Create test project structure in `tests/MuseumSystem.Domain.Tests/MuseumSystem.Domain.Tests.csproj` (Plan Testing Strategy, Constitution XIII)
- [x] T011 [P] Create application and integration test project files in `tests/MuseumSystem.Application.Tests/MuseumSystem.Application.Tests.csproj` and `tests/MuseumSystem.Integration.Tests/MuseumSystem.Integration.Tests.csproj` (Plan Testing Strategy, Constitution XIII)
- [x] T012 [P] Create web acceptance test project file in `tests/MuseumSystem.Web.AcceptanceTests/MuseumSystem.Web.AcceptanceTests.csproj` (Plan Testing Strategy, Constitution XIII)
- [x] T013 Configure project references from Web to Application/Infrastructure and from Application to Domain in `src/MuseumSystem.Web/MuseumSystem.Web.csproj` and `src/MuseumSystem.Application/MuseumSystem.Application.csproj` (Plan Module Boundaries, Constitution III)
- [x] T014 Configure EF Core, Npgsql, ASP.NET Core Identity, and ClosedXML package references in `src/MuseumSystem.Infrastructure/MuseumSystem.Infrastructure.csproj` and `src/MuseumSystem.Web/MuseumSystem.Web.csproj` (Research decisions, Plan Technical Context)
- [x] T015 Create shared result contract `UseCaseResult` in `src/MuseumSystem.Application/Common/UseCaseResult.cs` (Application Use Case Contracts, Plan Validation Boundaries)
- [x] T016 Create audit abstractions in `src/MuseumSystem.Application/Common/Audit/IAuditActorContext.cs` and `src/MuseumSystem.Application/Common/Audit/IAuditWriter.cs` (Plan audit foundation, Constitution IX)
- [x] T017 Create EF Core `MuseumDbContext` shell and module configuration folders in `src/MuseumSystem.Infrastructure/Persistence/MuseumDbContext.cs` (Plan Phase A)
- [x] T018 Create Identity user, role, and permission seed placeholders in `src/MuseumSystem.Infrastructure/Identity/IdentitySeed.cs` (Plan Authorization Boundaries, Constitution IX)
- [x] T019 Create authenticated Arabic RTL Blazor layout shell in `src/MuseumSystem.Web/Components/Layout/MainLayout.razor` and `src/MuseumSystem.Web/wwwroot/css/app.css` (Plan Staff UX, Constitution IV)
- [x] T020 Configure environment/appsettings PostgreSQL connection placeholders in `src/MuseumSystem.Web/appsettings.json` (Plan Technical Context, Constitution XII)
- [x] T021 Register module services without cross-module shortcuts in `src/MuseumSystem.Web/Program.cs` (Plan Module Boundaries, Constitution III)

## Phase 2: Artifact Registry

**Goal**: Implement category and artifact identity, official museum number rules, search, detail view, and location foundation needed for artifact creation.

**Independent Test Criteria**: Staff can create a category with unique required `CategoryCode`, create an artifact with `MuseumNumber = CategoryCode + ItemNumber`, search it, and view details; duplicate category codes or museum numbers are rejected.

### Artifact Registry Tests

- [x] T022 [P] [US1] Add domain tests for required unique `CategoryCode` in `tests/MuseumSystem.Domain.Tests/ArtifactRegistry/ArtifactCategoryTests.cs` (FR-001, FR-002, BR-003, BR-004)
- [x] T023 [P] [US1] Add domain tests proving `MuseumNumber` uses `CategoryCode + ItemNumber` and excludes `CategoryId` in `tests/MuseumSystem.Domain.Tests/ArtifactRegistry/MuseumNumberTests.cs` (FR-002, FR-004, BR-003, BR-004)
- [x] T024 [P] [US1] Add domain tests for immutable `ArtifactId` and initial storage state in `tests/MuseumSystem.Domain.Tests/ArtifactRegistry/ArtifactTests.cs` (FR-003, FR-006, BR-001, BR-002)
- [x] T025 [P] [US1] Add application tests for `CreateCategory` and `CreateArtifact` validation in `tests/MuseumSystem.Application.Tests/ArtifactRegistry/CreateArtifactUseCaseTests.cs` (FR-001, FR-005, FR-006)
- [x] T026 [P] [US1] Add integration tests for unique `CategoryCode` and unique museum number persistence in `tests/MuseumSystem.Integration.Tests/ArtifactRegistry/ArtifactRegistryPersistenceTests.cs` (FR-002, FR-004, SC-002)
- [x] T027 [P] [US1] Add web acceptance test for artifact creation flow in `tests/MuseumSystem.Web.AcceptanceTests/ArtifactRegistry/ArtifactRegistryFlowTests.cs` (FR-001, FR-006, SC-001)

### Artifact Registry Implementation

- [x] T028 [P] [US1] Create `ArtifactCategory` entity in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/ArtifactCategory.cs` (FR-001)
- [x] T029 [P] [US1] Create `MuseumNumber` value object in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/MuseumNumber.cs` (FR-002, FR-004, BR-003, BR-004)
- [x] T030 [P] [US1] Create `Artifact` entity with current state fields in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/Artifact.cs` (FR-003, FR-006, FR-007, FR-009)
- [x] T031 [P] [US7] Create initial `Location` entity needed for artifact creation in `src/MuseumSystem.Domain/Modules/StorehouseOperations/Location.cs` (FR-015)
- [x] T032 [US1] Implement category lifecycle rules in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/ArtifactCategoryRules.cs` (FR-001)
- [x] T033 [US1] Implement artifact factory enforcing initial storage location and museum number rules in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/ArtifactFactory.cs` (FR-002, FR-003, FR-006, BR-001)
- [x] T034 [US1] Add EF Core configurations for category, artifact, museum number uniqueness, and initial location FK in `src/MuseumSystem.Infrastructure/Persistence/Configurations/ArtifactRegistryConfiguration.cs` (FR-002, FR-004, SC-002)
- [x] T035 [US7] Add EF Core configuration for locations used by registry screens in `src/MuseumSystem.Infrastructure/Persistence/Configurations/StorehouseLocationConfiguration.cs` (FR-015)
- [x] T036 [US1] Create registry DTOs in `src/MuseumSystem.Application/Modules/ArtifactRegistry/Contracts/ArtifactRegistryDtos.cs` (Application Use Case Contracts: Artifact Registry)
- [x] T037 [US1] Implement `CreateCategory`, `UpdateCategory`, and `DisableCategoryForNewUse` in `src/MuseumSystem.Application/Modules/ArtifactRegistry/CategoryUseCases.cs` (FR-001)
- [x] T038 [US1] Implement `CreateArtifact` and `UpdateArtifactBasicInfo` in `src/MuseumSystem.Application/Modules/ArtifactRegistry/ArtifactWriteUseCases.cs` (FR-003, FR-004, FR-006)
- [x] T039 [US3] Implement `SearchArtifacts` and `GetArtifactDetails` in `src/MuseumSystem.Application/Modules/ArtifactRegistry/ArtifactReadUseCases.cs` (FR-016, FR-017, SC-005)
- [x] T040 [US7] Implement location create/update/disable/list use cases in `src/MuseumSystem.Application/Modules/StorehouseOperations/LocationUseCases.cs` (FR-015)
- [x] T041 [US1] Create Blazor category management page in `src/MuseumSystem.Web/Components/Pages/Artifacts/Categories.razor` (FR-001)
- [x] T042 [US1] Create Blazor artifact create page in `src/MuseumSystem.Web/Components/Pages/Artifacts/Create.razor` (FR-006, SC-001)
- [x] T043 [US3] Create Blazor artifact search and details pages in `src/MuseumSystem.Web/Components/Pages/Artifacts/Search.razor` and `src/MuseumSystem.Web/Components/Pages/Artifacts/Details.razor` (FR-016, FR-017, SC-005)
- [x] T044 [US7] Create Blazor location management page under storehouse module in `src/MuseumSystem.Web/Components/Pages/Storehouse/Locations.razor` (FR-015)
- [x] T045 [US1] Wire artifact registry and location services into DI in `src/MuseumSystem.Web/Program.cs` (Plan Module Boundaries)

## Phase 3: Storehouse Operations

**Goal**: Implement delivery, return, custody, current location/current holder rules, last known storage location, bulk atomicity, and movement history.

**Independent Test Criteria**: Staff can deliver eligible artifacts atomically to documentation/lab/photographer/display hall, return artifacts to storage, see movement history, and observe correct `CurrentLocation`, `CurrentHolder`, and `LastKnownStorageLocation` behavior.

### Storehouse Operations Tests

- [x] T046 [P] [US4] Add domain tests for delivery state transitions in `tests/MuseumSystem.Domain.Tests/StorehouseOperations/MovementStateTransitionTests.cs` (FR-018, FR-019, FR-020, FR-021, BR-008)
- [x] T047 [P] [US4] Add domain tests for `CurrentLocation`, `CurrentHolder`, and `LastKnownStorageLocation` rules in `tests/MuseumSystem.Domain.Tests/StorehouseOperations/CurrentStateRulesTests.cs` (FR-007, FR-008, FR-009, BR-008)
- [x] T048 [P] [US4] Add application tests for bulk delivery atomicity in `tests/MuseumSystem.Application.Tests/StorehouseOperations/DeliverArtifactsUseCaseTests.cs` (FR-027, FR-028, BR-010, SC-010)
- [x] T049 [P] [US5] Add application tests for return validation and location updates in `tests/MuseumSystem.Application.Tests/StorehouseOperations/ReturnArtifactsUseCaseTests.cs` (FR-022, FR-023, FR-024, BR-009)
- [x] T050 [P] [US4] Add integration tests for optimistic concurrency on artifact state changes in `tests/MuseumSystem.Integration.Tests/StorehouseOperations/StorehouseConcurrencyTests.cs` (Plan Testing Strategy, Research: Optimistic Concurrency)
- [x] T051 [P] Add web acceptance tests for delivery and return workflows in `tests/MuseumSystem.Web.AcceptanceTests/Storehouse/DeliveryReturnFlowTests.cs` (FR-018, FR-022, SC-003, SC-004, SC-008)

### Storehouse Operations Implementation

- [x] T052 [P] [US4] Create `MovementRecord` entity in `src/MuseumSystem.Domain/Modules/StorehouseOperations/MovementRecord.cs` (FR-020, FR-023, FR-024, FR-025, FR-026)
- [x] T053 [P] [US4] Create movement enums and holder value objects in `src/MuseumSystem.Domain/Modules/StorehouseOperations/MovementTypes.cs` (FR-008, FR-018, FR-019, BR-008)
- [x] T054 Add artifact state transition methods for deliver/return in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/Artifact.cs` (FR-007, FR-008, FR-009, FR-024)
- [x] T055 [US4] Implement current holder and location validation rules in `src/MuseumSystem.Domain/Modules/StorehouseOperations/CurrentStateRules.cs` (FR-007, FR-008, FR-009, BR-008, BR-009)
- [x] T056 [US4] Add movement persistence configuration in `src/MuseumSystem.Infrastructure/Persistence/Configurations/MovementRecordConfiguration.cs` (FR-024, FR-025, FR-026)
- [x] T057 Create storehouse DTOs in `src/MuseumSystem.Application/Modules/StorehouseOperations/Contracts/StorehouseDtos.cs` (Application Use Case Contracts: Movements)
- [x] T058 [US4] Implement `PreviewDeliveryEligibility` in `src/MuseumSystem.Application/Modules/StorehouseOperations/DeliveryEligibilityUseCase.cs` (FR-021, FR-027, FR-028, SC-010)
- [x] T059 [US4] Implement atomic `DeliverArtifacts` transaction in `src/MuseumSystem.Application/Modules/StorehouseOperations/DeliverArtifactsUseCase.cs` (FR-018, FR-019, FR-020, FR-024, FR-027, FR-028, BR-010)
- [x] T060 [US5] Implement `PreviewReturnEligibility` in `src/MuseumSystem.Application/Modules/StorehouseOperations/ReturnEligibilityUseCase.cs` (FR-022, BR-009)
- [x] T061 [US5] Implement atomic `ReturnArtifacts` transaction in `src/MuseumSystem.Application/Modules/StorehouseOperations/ReturnArtifactsUseCase.cs` (FR-022, FR-023, FR-024, FR-027, FR-028, BR-009, BR-010)
- [x] T062 [US4] Implement `GetMovementHistory` in `src/MuseumSystem.Application/Modules/StorehouseOperations/MovementHistoryUseCase.cs` (FR-025, FR-026, BR-006, BR-007)
- [x] T063 [US4] Create delivery page with bulk eligibility preview in `src/MuseumSystem.Web/Components/Pages/Storehouse/Delivery.razor` (FR-018, FR-019, FR-027, FR-028, SC-003, SC-010)
- [x] T064 [US5] Create return page with return location selection in `src/MuseumSystem.Web/Components/Pages/Storehouse/Return.razor` (FR-022, FR-023, FR-024, BR-009, SC-004)
- [x] T065 [US3] Add movement history panel to artifact details in `src/MuseumSystem.Web/Components/Pages/Artifacts/Details.razor` (FR-017, FR-026, SC-005)
- [x] T066 [US4] Wire storehouse services and authorization policies in `src/MuseumSystem.Web/Program.cs` (Plan Authorization Boundaries, Constitution IX)

## Phase 4: Excel Import

**Goal**: Implement Excel upload preview, row validation, explicit commit, row issue reporting, import audit, and prevention of mutation before commit.

**Independent Test Criteria**: Staff can upload `.xlsx`, preview parsed rows without mutating artifacts, validate accepted/rejected/needs-review rows, and explicitly commit only ready batches atomically.

### Excel Import Tests

- [x] T067 [P] [US2] Add ClosedXML adapter parsing tests in `tests/MuseumSystem.Integration.Tests/Import/ExcelImportReaderTests.cs` (FR-010, Research: ClosedXML)
- [x] T068 [P] [US2] Add application tests proving preview does not mutate artifact/location tables in `tests/MuseumSystem.Application.Tests/Import/UploadImportFileForPreviewTests.cs` (FR-012, SC-009)
- [x] T069 [P] [US2] Add application tests for row validation statuses in `tests/MuseumSystem.Application.Tests/Import/ValidateImportBatchTests.cs` (FR-011, FR-013, FR-014, BR-011)
- [x] T070 [P] [US2] Add application tests for explicit commit refusal and commit success in `tests/MuseumSystem.Application.Tests/Import/CommitImportBatchTests.cs` (FR-011, FR-012, BR-012)
- [x] T071 [P] [US2] Add integration tests for `ImportBatch` concurrency token in `tests/MuseumSystem.Integration.Tests/Import/ImportBatchConcurrencyTests.cs` (Plan Testing Strategy, Research: Optimistic Concurrency)
- [x] T072 [P] [US2] Add web acceptance tests for preview, validation, and commit flow in `tests/MuseumSystem.Web.AcceptanceTests/Import/ExcelImportFlowTests.cs` (FR-011, SC-006)

### Excel Import Implementation

- [x] T073 [P] [US2] Create `ImportBatch` and `ImportRow` entities in `src/MuseumSystem.Domain/Modules/Import/ImportBatch.cs` and `src/MuseumSystem.Domain/Modules/Import/ImportRow.cs` (FR-010)
- [x] T074 [US2] Implement import lifecycle rules in `src/MuseumSystem.Domain/Modules/Import/ImportBatchRules.cs` (FR-011, FR-012, BR-012)
- [x] T075 [US2] Add import persistence configuration in `src/MuseumSystem.Infrastructure/Persistence/Configurations/ImportConfiguration.cs` (FR-010)
- [x] T076 [US2] Implement ClosedXML `.xlsx` reader adapter in `src/MuseumSystem.Infrastructure/Excel/ClosedXmlImportReader.cs` (FR-010, Research: ClosedXML)
- [x] T077 [US2] Create import DTOs and row issue contracts in `src/MuseumSystem.Application/Modules/Import/Contracts/ImportDtos.cs` (Application Use Case Contracts: Excel Import)
- [x] T078 [US2] Implement `UploadImportFileForPreview` without artifact mutation in `src/MuseumSystem.Application/Modules/Import/UploadImportFileForPreviewUseCase.cs` (FR-012)
- [x] T079 [US2] Implement `ValidateImportBatch` with accepted/rejected/needs-review statuses in `src/MuseumSystem.Application/Modules/Import/ValidateImportBatchUseCase.cs` (FR-011, FR-013, FR-014, BR-011)
- [x] T080 [US2] Implement atomic `CommitImportBatch` in `src/MuseumSystem.Application/Modules/Import/CommitImportBatchUseCase.cs` (FR-011, FR-012, BR-012)
- [x] T081 [US2] Implement `CancelImportBatch` in `src/MuseumSystem.Application/Modules/Import/CancelImportBatchUseCase.cs` (FR-011, BR-012)
- [x] T082 [US2] Create Excel import page with upload, preview, validation, and commit states in `src/MuseumSystem.Web/Components/Pages/Imports/ExcelImport.razor` (FR-011, SC-006)
- [x] T083 [US2] Wire import services and `Imports.Preview`/`Imports.Commit` policies in `src/MuseumSystem.Web/Program.cs` (Plan Authorization Boundaries, Constitution IX)

## Phase 5: Reconciliation & Corrections

**Goal**: Implement inventory reconciliation sessions, result classification, documented corrections, audit visibility, and correction rules that do not replace return or movement history.

**Independent Test Criteria**: Staff can start a reconciliation session for a location, record observed items, review classifications, create documented corrections for confirmed conflicts, and see audit/correction history.

### Reconciliation & Corrections Tests

- [x] T084 [P] [US6] Add domain tests for reconciliation result classification in `tests/MuseumSystem.Domain.Tests/StorehouseOperations/ReconciliationClassificationTests.cs` (FR-029, SC-007)
- [x] T085 [P] [US6] Add domain tests for documented correction rules in `tests/MuseumSystem.Domain.Tests/StorehouseOperations/DocumentedCorrectionTests.cs` (FR-030, BR-006, BR-007)
- [x] T086 [P] [US6] Add application tests for reconciliation session lifecycle in `tests/MuseumSystem.Application.Tests/StorehouseOperations/ReconciliationSessionUseCaseTests.cs` (FR-029)
- [x] T087 [P] [US6] Add application tests proving correction does not substitute for return in `tests/MuseumSystem.Application.Tests/StorehouseOperations/CreateDocumentedCorrectionUseCaseTests.cs` (FR-030, BR-006, BR-007, BR-009)
- [x] T088 [P] [US6] Add integration tests for append-only correction and audit records in `tests/MuseumSystem.Integration.Tests/StorehouseOperations/CorrectionAuditPersistenceTests.cs` (FR-030, BR-006, BR-007, Constitution IX)
- [x] T089 [P] [US6] Add web acceptance tests for reconciliation and correction workflow in `tests/MuseumSystem.Web.AcceptanceTests/Storehouse/ReconciliationCorrectionFlowTests.cs` (FR-029, FR-030, SC-007)

### Reconciliation & Corrections Implementation

- [x] T090 [P] [US6] Create `ReconciliationSession` entity in `src/MuseumSystem.Domain/Modules/StorehouseOperations/ReconciliationSession.cs` (FR-029)
- [x] T091 [P] [US6] Create `ReconciliationResult` entity in `src/MuseumSystem.Domain/Modules/StorehouseOperations/ReconciliationResult.cs` (FR-029, SC-007)
- [x] T092 [P] [US6] Create `DocumentedCorrection` entity in `src/MuseumSystem.Domain/Modules/StorehouseOperations/DocumentedCorrection.cs` (FR-030, BR-006, BR-007)
- [x] T093 [US6] Implement reconciliation classification rules in `src/MuseumSystem.Domain/Modules/StorehouseOperations/ReconciliationRules.cs` (FR-029, SC-007)
- [x] T094 [US6] Implement documented correction domain rules in `src/MuseumSystem.Domain/Modules/StorehouseOperations/DocumentedCorrectionRules.cs` (FR-030, BR-006, BR-007)
- [x] T095 [US6] Add reconciliation and correction persistence configuration in `src/MuseumSystem.Infrastructure/Persistence/Configurations/ReconciliationCorrectionConfiguration.cs` (FR-029, FR-030)
- [x] T096 [US6] Implement `StartReconciliationSession` in `src/MuseumSystem.Application/Modules/StorehouseOperations/StartReconciliationSessionUseCase.cs` (FR-029)
- [x] T097 [US6] Implement `RecordReconciliationItems` in `src/MuseumSystem.Application/Modules/StorehouseOperations/RecordReconciliationItemsUseCase.cs` (FR-029, SC-007)
- [x] T098 [US6] Implement `ReviewReconciliationResults` in `src/MuseumSystem.Application/Modules/StorehouseOperations/ReviewReconciliationResultsUseCase.cs` (FR-029, SC-007)
- [x] T099 [US6] Implement `CreateDocumentedCorrection` with audit write in `src/MuseumSystem.Application/Modules/StorehouseOperations/CreateDocumentedCorrectionUseCase.cs` (FR-030, BR-006, BR-007, Constitution IX)
- [x] T100 [US6] Create reconciliation page in `src/MuseumSystem.Web/Components/Pages/Storehouse/Reconciliation.razor` (FR-029, SC-007)
- [x] T101 [US6] Create documented correction dialog in `src/MuseumSystem.Web/Components/Pages/Storehouse/DocumentedCorrectionDialog.razor` (FR-030, BR-006, BR-007)
- [x] T102 Create audit view page in `src/MuseumSystem.Web/Components/Pages/Admin/AuditTrail.razor` (Plan Authorization Boundaries, Constitution IX)

## Phase 6: Hardening & UAT

**Goal**: Validate the complete phase-one system against quickstart scenarios, permissions, RTL usability, backup/restore readiness, and performance expectations.

**Independent Test Criteria**: All quickstart validation scenarios pass, permission boundaries are enforced, import and movement edge cases remain safe, and staff-facing screens are usable in Arabic RTL.

### Hardening & UAT Tests

- [ ] T103 [P] Add end-to-end permission matrix tests in `tests/MuseumSystem.Web.AcceptanceTests/Security/PermissionMatrixTests.cs` (Plan Authorization Boundaries, Constitution IX)
- [ ] T104 [P] Add quickstart build/database validation test notes in `tests/MuseumSystem.Integration.Tests/Quickstart/BuildDatabaseValidationTests.cs` (Quickstart, Plan Phase F)
- [ ] T105 [P] Add RTL usability acceptance coverage for primary screens in `tests/MuseumSystem.Web.AcceptanceTests/Usability/RtlPrimaryScreensTests.cs` (SC-008, Constitution IV)
- [ ] T106 [P] Add backup/restore drill checklist test artifact in `tests/MuseumSystem.Integration.Tests/Deployment/BackupRestoreReadinessTests.cs` (Plan Phase F, Constitution XI)
- [ ] T107 [P] Add performance smoke tests for search and bulk operations in `tests/MuseumSystem.Integration.Tests/Performance/PhaseOnePerformanceSmokeTests.cs` (SC-003, SC-004, SC-005)

### Hardening & UAT Implementation

- [ ] T108 Finalize permission constants and role presets in `src/MuseumSystem.Application/Modules/IdentityAccess/Permissions.cs` (Plan Authorization Boundaries, Constitution IX)
- [ ] T109 Enforce authorization policies on all Blazor pages in `src/MuseumSystem.Web/Components/Routes.razor` (Plan Authorization Boundaries, Constitution IX)
- [ ] T110 Implement application-boundary audit writer in `src/MuseumSystem.Infrastructure/Audit/AuditWriter.cs` (Plan audit foundation, Constitution IX)
- [ ] T111 Add user-facing Arabic validation messages without stack traces in `src/MuseumSystem.Web/Components/Shared/ValidationSummary.razor` (FR-031, FR-032, SC-008)
- [ ] T112 Add migration creation instructions and no-Docker deployment notes in `specs/001-central-artifact-registry/quickstart.md` (Plan Migration / Deployment Approach, Constitution XII)
- [ ] T113 Add initial EF Core migration task output location in `src/MuseumSystem.Infrastructure/Persistence/Migrations/` (Plan Migration / Deployment Approach)
- [ ] T114 Run and record quickstart validation checklist results in `specs/001-central-artifact-registry/checklists/uat-results.md` (Quickstart, SC-001, SC-002, SC-003, SC-004, SC-005, SC-006, SC-007, SC-008, SC-009, SC-010)
- [ ] T115 Review `specs/001-central-artifact-registry/tasks.md` against `specs/001-central-artifact-registry/plan.md` to ensure no out-of-scope architecture was introduced (Constitution XIV)

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Foundation**: no dependencies.
- **Phase 2 Artifact Registry**: depends on Foundation tasks T005-T021.
- **Phase 3 Storehouse Operations**: depends on Artifact Registry entity/location baseline T028-T045.
- **Phase 4 Excel Import**: depends on Artifact Registry and location persistence T034-T040; can begin after Phase 2 if Storehouse movement screens are not touched.
- **Phase 5 Reconciliation & Corrections**: depends on Storehouse movement/current-state model T052-T066.
- **Phase 6 Hardening & UAT**: depends on all selected phase-one functionality.

### Parallel Opportunities

- Foundation project creation tasks T006-T012 can run in parallel after T005.
- Domain test tasks in each phase can run in parallel with application/web test stubs for the same phase.
- Artifact Registry entity tasks T028-T031 can run in parallel before rules and EF mappings.
- Storehouse test tasks T046-T051 can run in parallel; movement entity tasks T052-T053 can run before use cases.
- Excel Import tests T067-T072 can run in parallel; implementation T073-T077 can split by Domain, Infrastructure, and Application.
- Reconciliation entities T090-T092 can run in parallel before rules and use cases.
- Hardening tests T103-T107 can run in parallel once phase functionality is available.

### MVP Scope

- First vertical slice: Foundation -> Artifact Registry -> minimum complete Storehouse Operations.
- MVP demo: create a storage location and category, create an artifact, verify official museum number, search/open the artifact, deliver it to a supported internal holder, then receive it back into a valid storage location with movement history preserved.
- This MVP maps to US1 + US3 + the minimum US4/US5 path required for create -> search -> deliver -> return.

## Implementation Strategy

1. Complete Foundation and verify build/test skeleton.
2. Deliver Artifact Registry plus the minimum Storehouse Operations needed for create -> search -> deliver -> return as the first vertical slice.
3. Expand Storehouse Operations to cover bulk atomicity and full movement history.
4. Add Excel Import after registry uniqueness and location references are stable.
5. Add Reconciliation & Corrections after movement/current-state behavior is reliable.
6. Finish with Hardening & UAT using `quickstart.md` scenarios.

## Notes

- `[P]` means the task touches separate files or can be reviewed independently after its prerequisites.
- Every implementation task stays inside `ArtifactRegistry`, `StorehouseOperations`, `Import`, or `IdentityAccess` module boundaries.
- Tests are included in each phase because the feature specification and constitution require critical business rule testing.
- Do not implement image stewardship or public-facing museum features in this phase.

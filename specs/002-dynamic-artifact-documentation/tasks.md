# Tasks: Dynamic Artifact Documentation

**Input**: Design documents from `/specs/002-dynamic-artifact-documentation/`

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/application-use-cases.md`, `contracts/ui-workflows.md`, `.specify/memory/constitution.md`

**Scope Guard**: Keep Museum-System as the existing .NET 10, Blazor, PostgreSQL Modular Monolith. Do not add microservices, controllers/APIs, event buses, CQRS/MediatR, external document stores, Playwright, bUnit, or parallel authorization/custody models solely for Feature 002. Do not implement photography, archive integration, laboratory/conservation, exhibition/loans, notifications, approval workflows, exports, printing, barcode/QR, OCR, or AI.

**Repository-Aware Custody Decision**: Feature 001 represents Documentation custody with `MovementRecipientType.DocumentationDivision`; delivered artifacts have `CurrentStatus = OutOfStorage` and `CurrentHolderType = recipientType.ToString()`. Feature 002 must centralize holder interpretation in Feature 001, preferably with `CurrentStateRules.IsHeldBy(Artifact, MovementRecipientType)`, and must never compare `CurrentHolderName` for custody or authorization decisions.

**Task Format**: `- [ ] T### [P?] [US?] Description with file path`

## Traceability Map

- **US1**: Document an artifact by museum number.
- **US2**: Prevent draft documentation outside Documentation custody.
- **US3**: Manage category documentation templates.
- **US4**: Correct completed documentation with history.
- **US5**: Enforce documentation permissions.

## Task Counts

- **Total**: 144 tasks
- **Phase A foundation**: 45 tasks
- **US3 template management**: 29 tasks
- **US1/US2 primary workflow and custody boundary**: 30 tasks
- **US4 corrections and revision history**: 20 tasks
- **US5 documentation permissions**: 7 tasks
- **Phase F concurrency and final verification**: 13 tasks

## Phase A: Domain, Integration Inspection, Permissions, and Persistence Foundation

**Goal**: Establish Documentation module boundaries, canonical Feature 001 integration, domain primitives, permission foundation, additive persistence, PostgreSQL test infrastructure, and regression protection before user-story work.

**Independent Test Criteria**: The solution stays buildable after each reviewable group; domain tests prove core invariants; permissions are registered through the existing model; PostgreSQL-specific tests share one reusable fixture; Feature 001 behavior remains stable.

### A1. Repository Inspection and Custody Integration

- [X] T001 Inspect Feature 001 artifact/category and museum-number behavior in `src/MuseumSystem.Domain/Modules/ArtifactRegistry/Artifact.cs` and `src/MuseumSystem.Domain/Modules/ArtifactRegistry/ArtifactCategory.cs`
- [X] T002 Inspect Feature 001 Storehouse recipient and holder behavior for `MovementRecipientType.DocumentationDivision` in `src/MuseumSystem.Domain/Modules/StorehouseOperations/MovementTypes.cs` and `src/MuseumSystem.Application/Modules/StorehouseOperations/DeliverArtifactsUseCase.cs`
- [X] T003 Inspect Feature 001 current-state and concurrency conventions in `src/MuseumSystem.Domain/Modules/StorehouseOperations/CurrentStateRules.cs`, `src/MuseumSystem.Domain/Modules/ArtifactRegistry/Artifact.cs`, and `src/MuseumSystem.Infrastructure/Persistence/Configurations/ArtifactRegistryConfiguration.cs`
- [X] T004 Inspect existing audit writer and audit test conventions in `src/MuseumSystem.Infrastructure/Audit/AuditWriter.cs` and `tests/MuseumSystem.Application.Tests/Audit/SensitiveWriteAuditTests.cs`
- [X] T005 Inspect existing permission constants, policies, role presets, and seed behavior in `src/MuseumSystem.Application/Modules/IdentityAccess/PermissionNames.cs`, `src/MuseumSystem.Application/Modules/IdentityAccess/Permissions.cs`, and `src/MuseumSystem.Infrastructure/Identity/IdentitySeed.cs`
- [X] T006 Inspect actual existing Blazor conventions in `src/MuseumSystem.Web/Components/Pages/Storehouse/Delivery.razor`, `src/MuseumSystem.Web/Components/Pages/Storehouse/Return.razor`, `src/MuseumSystem.Web/Components/Pages/Imports/ExcelImport.razor`, `src/MuseumSystem.Web/Components/Pages/Admin/AuditTrail.razor`, `src/MuseumSystem.Web/Components/Shared/ValidationSummary.razor`, `src/MuseumSystem.Web/Components/Layout/NavMenu.razor`, and `src/MuseumSystem.Web/Components/Routes.razor`
- [X] T007 Record the canonical Documentation custody availability decision and helper location in `specs/002-dynamic-artifact-documentation/research.md`

### A2. Domain Primitives and Rules

- [X] T008 [P] Add domain tests for supported documentation field types in `tests/MuseumSystem.Domain.Tests/Documentation/DocumentationFieldTypeTests.cs`
- [X] T009 [P] Add domain tests for template field key and option key uniqueness in `tests/MuseumSystem.Domain.Tests/Documentation/DocumentationTemplateVersionTests.cs`
- [X] T010 [P] Add domain tests for template lifecycle Draft/Active/Retired, no-more-than-one Active version, and zero-active allowance in `tests/MuseumSystem.Domain.Tests/Documentation/DocumentationTemplateLifecycleTests.cs`
- [X] T011 [P] Add domain tests for used template version immutability except retirement in `tests/MuseumSystem.Domain.Tests/Documentation/UsedTemplateVersionImmutabilityTests.cs`
- [X] T012 [P] Add domain tests for DocumentationRecord Draft/Completed lifecycle and one-record-per-artifact invariant in `tests/MuseumSystem.Domain.Tests/Documentation/DocumentationRecordTests.cs`
- [X] T013 [P] Add domain tests for dynamic value validation across Text, MultilineText, Number, Date, Boolean, SingleSelect, and MultiSelect in `tests/MuseumSystem.Domain.Tests/Documentation/DocumentationValueValidationTests.cs`
- [X] T014 [P] Add domain tests for `CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision)` and Documentation availability without using `CurrentHolderName` in `tests/MuseumSystem.Domain.Tests/Documentation/DocumentationAvailabilityRulesTests.cs`
- [X] T015 Create Documentation domain module marker in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationModule.cs`
- [X] T016 [P] Create Documentation enums in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationEnums.cs`
- [X] T017 [P] Create dynamic value model and typed validation helpers in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationFieldValue.cs`
- [X] T018 [P] Create `DocumentationTemplate` aggregate in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationTemplate.cs`
- [X] T019 [P] Create `DocumentationTemplateVersion` entity with lifecycle and concurrency token in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationTemplateVersion.cs`
- [X] T020 [P] Create `DocumentationTemplateField` entity in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationTemplateField.cs`
- [X] T021 [P] Create `DocumentationTemplateFieldOption` entity in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationTemplateFieldOption.cs`
- [X] T022 [P] Create `DocumentationRecord` entity with Draft/Completed lifecycle and Revision 1 baseline fields in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationRecord.cs`
- [X] T023 [P] Create `DocumentationRevision` entity with authoritative revision numbering and mandatory reason in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationRevision.cs`
- [X] T024 Implement template field and option validation rules in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationTemplateRules.cs`
- [X] T025 Implement dynamic value validation against exact template version in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationValueRules.cs`
- [X] T026 Add read-only holder helper `IsHeldBy(Artifact, MovementRecipientType)` to centralize Feature 001 holder interpretation in `src/MuseumSystem.Domain/Modules/StorehouseOperations/CurrentStateRules.cs`
- [X] T027 Implement Documentation availability rule by delegating to `CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision)` in `src/MuseumSystem.Domain/Modules/Documentation/DocumentationAvailabilityRules.cs`

### A3. Permissions and Role Presets

- [X] T028 [P] Add application permission policy tests for all Documentation permissions and the DocumentationStaff role preset in `tests/MuseumSystem.Application.Tests/IdentityAccess/PermissionPolicyTests.cs`
- [X] T029 Add Documentation permission constants to existing `PermissionNames.All` in `src/MuseumSystem.Application/Modules/IdentityAccess/PermissionNames.cs`
- [X] T030 Add `DocumentationStaff` role preset with `Documentation.View`, `Documentation.Create`, `Documentation.Edit`, `Documentation.Complete`, `Documentation.History.View`, and `Documentation.Templates.View` while keeping `Documentation.Templates.Manage` Admin-only by default in `src/MuseumSystem.Application/Modules/IdentityAccess/Permissions.cs`
- [X] T031 Update existing Identity seed behavior for Documentation permissions and `DocumentationStaff` role in `src/MuseumSystem.Infrastructure/Identity/IdentitySeed.cs`

### A4. Persistence and PostgreSQL Infrastructure

- [X] T032 Add Documentation DbSets to `src/MuseumSystem.Application/Common/Persistence/IMuseumDbContext.cs`
- [X] T033 Add Documentation DbSets to `src/MuseumSystem.Infrastructure/Persistence/MuseumDbContext.cs`
- [X] T034 Create EF configuration for templates, versions, fields, options, active-version constraint, and concurrency tokens in `src/MuseumSystem.Infrastructure/Persistence/Configurations/DocumentationTemplateConfiguration.cs`
- [X] T035 Create EF configuration for records, revisions, JSONB value columns, one-record-per-artifact constraint, foreign keys, and concurrency tokens in `src/MuseumSystem.Infrastructure/Persistence/Configurations/DocumentationRecordConfiguration.cs`
- [X] T036 Create reusable PostgreSQL integration-test fixture and connection configuration in `tests/MuseumSystem.Integration.Tests/Documentation/PostgresDocumentationTestFixture.cs`
- [X] T037 Add PostgreSQL integration-test dependencies/configuration needed by the fixture without making Docker a production requirement in `tests/MuseumSystem.Integration.Tests/MuseumSystem.Integration.Tests.csproj`
- [X] T038 Create additive EF Core migration for Documentation schema only in `src/MuseumSystem.Infrastructure/Persistence/Migrations/`
- [X] T039 [P] Add PostgreSQL migration application test for Documentation schema using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationMigrationTests.cs`
- [X] T040 [P] Add PostgreSQL JSONB mapping round-trip tests for Documentation values and revision snapshots using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationJsonbPersistenceTests.cs`
- [X] T041 [P] Add PostgreSQL uniqueness tests for one DocumentationRecord per Artifact using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationRecordConstraintTests.cs`
- [X] T042 [P] Add PostgreSQL active-template constraint and activation-race tests using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationTemplateConstraintTests.cs`
- [X] T043 [P] Add PostgreSQL foreign-key tests for Artifact and ArtifactCategory restrictive delete behavior using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationForeignKeyTests.cs`
- [X] T044 [P] Add PostgreSQL optimistic concurrency tests for DocumentationRecord and DocumentationTemplateVersion tokens using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationConcurrencyTests.cs`
- [X] T045 [P] Add Feature 001 regression tests proving Documentation migration does not break artifact search, Storehouse movement, or return workflows in `tests/MuseumSystem.Integration.Tests/Documentation/Feature001RegressionTests.cs`

**Checkpoint**: Phase A foundation is complete when domain, permission, persistence, migration, PostgreSQL fixture-backed tests, and Feature 001 regression tests pass.

## Phase B: Template Management (US3)

**Goal**: Authorized template managers can create category template families, create/copy Draft versions, define fields/options for supported types, activate versions, retire versions including active-without-replacement, and view used versions as read-only.

**Independent Test Criteria**: A template manager can create a template for an existing Artifact Category, define valid fields/options, activate a version, create and activate a later version that atomically retires the old Active version, retire an Active version without replacement, and verify used versions cannot be edited except retirement.

### Tests for User Story 3

- [X] T046 [P] [US3] Add application tests for `ListDocumentationTemplates` and `ViewTemplateVersion` in `tests/MuseumSystem.Application.Tests/Documentation/TemplateQueryUseCaseTests.cs`
- [X] T047 [P] [US3] Add application tests for creating a template family for an existing Artifact Category only in `tests/MuseumSystem.Application.Tests/Documentation/CreateDocumentationTemplateUseCaseTests.cs`
- [X] T048 [P] [US3] Add application tests for creating and copying Draft template versions in `tests/MuseumSystem.Application.Tests/Documentation/CreateTemplateVersionDraftUseCaseTests.cs`
- [X] T049 [P] [US3] Add application tests for saving Draft version fields/options across all seven supported field types in `tests/MuseumSystem.Application.Tests/Documentation/SaveTemplateVersionDraftUseCaseTests.cs`
- [X] T050 [P] [US3] Add application tests for field key uniqueness, option key uniqueness, required fields, sections, help text, display order, and select option validation in `tests/MuseumSystem.Application.Tests/Documentation/TemplateFieldValidationUseCaseTests.cs`
- [X] T051 [P] [US3] Add application tests for activating a Draft version and atomically retiring any previous Active version in `tests/MuseumSystem.Application.Tests/Documentation/ActivateTemplateVersionUseCaseTests.cs`
- [X] T052 [P] [US3] Add application tests for retiring an Active version without replacement and zero-active state behavior in `tests/MuseumSystem.Application.Tests/Documentation/RetireTemplateVersionUseCaseTests.cs`
- [X] T053 [P] [US3] Add application tests for used template version immutability except retirement status in `tests/MuseumSystem.Application.Tests/Documentation/UsedTemplateVersionUseCaseTests.cs`
- [X] T054 [P] [US3] Add application tests for stale Template Draft and lifecycle writes using existing `UseCaseResult.Conflict` behavior in `tests/MuseumSystem.Application.Tests/Documentation/TemplateConcurrencyUseCaseTests.cs`
- [X] T055 [P] [US3] Add application tests proving template write use cases create audit entries in `tests/MuseumSystem.Application.Tests/Documentation/TemplateAuditUseCaseTests.cs`
- [X] T056 [P] [US3] Add lightweight xUnit acceptance tests for template administration source structure and routed page conventions in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationTemplateAdminFlowTests.cs`

### Implementation for User Story 3

- [X] T057 [P] [US3] Create template DTO contracts in `src/MuseumSystem.Application/Modules/Documentation/Contracts/DocumentationTemplateDtos.cs`
- [X] T058 [P] [US3] Create template field DTO contracts in `src/MuseumSystem.Application/Modules/Documentation/Contracts/DocumentationTemplateFieldDtos.cs`
- [X] T059 [US3] Implement `ListDocumentationTemplates` in `src/MuseumSystem.Application/Modules/Documentation/TemplateQueryUseCases.cs`
- [X] T060 [US3] Implement `ViewTemplateVersion` with used/read-only indicator in `src/MuseumSystem.Application/Modules/Documentation/TemplateQueryUseCases.cs`
- [X] T061 [US3] Implement `CreateDocumentationTemplate` using existing ArtifactCategory references in `src/MuseumSystem.Application/Modules/Documentation/CreateDocumentationTemplateUseCase.cs`
- [X] T062 [US3] Implement `CreateTemplateVersionDraft` including copy-from-existing-version behavior in `src/MuseumSystem.Application/Modules/Documentation/CreateTemplateVersionDraftUseCase.cs`
- [X] T063 [US3] Implement `SaveTemplateVersionDraft` with seven-field-type validation in `src/MuseumSystem.Application/Modules/Documentation/SaveTemplateVersionDraftUseCase.cs`
- [X] T064 [US3] Implement `ActivateTemplateVersion` with atomic retire-previous-active behavior in `src/MuseumSystem.Application/Modules/Documentation/ActivateTemplateVersionUseCase.cs`
- [X] T065 [US3] Implement `RetireTemplateVersion` allowing zero Active versions temporarily in `src/MuseumSystem.Application/Modules/Documentation/RetireTemplateVersionUseCase.cs`
- [X] T066 [US3] Add template audit writes for create, save Draft, activate, and retire in `src/MuseumSystem.Application/Modules/Documentation/DocumentationAuditActions.cs`
- [X] T067 [US3] Register only Phase B template-management use cases using the explicit scoped registration pattern in `src/MuseumSystem.Application/DependencyInjection.cs`
- [X] T068 [US3] Add Template Administration route with `Documentation.Templates.View` policy in `src/MuseumSystem.Web/Components/Pages/Documentation/Templates.razor`
- [X] T069 [US3] Create Draft template version editor page with `Documentation.Templates.Manage` policy in `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionEditor.razor`
- [X] T070 [US3] Create read-only used template version details page with `Documentation.Templates.View` policy in `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionDetails.razor`
- [X] T071 [US3] Add supported field type controls, option editing, section, help text, required flag, and display order controls in `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionEditor.razor`
- [X] T072 [US3] Add action-level authorization checks for activate/retire/manage operations exposed from template pages in `src/MuseumSystem.Web/Components/Pages/Documentation/Templates.razor` and `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionEditor.razor`
- [X] T073 [US3] Add activate and retire actions with clear zero-active and previous-active-retired messages in `src/MuseumSystem.Web/Components/Pages/Documentation/Templates.razor`
- [X] T074 [US3] Add Documentation template navigation entry respecting permissions in `src/MuseumSystem.Web/Components/Layout/NavMenu.razor`

**Checkpoint**: US3 is independently testable through Template Administration without creating Documentation Records.

## Phase C: Documentation Primary Workflow (US1, US2)

**Goal**: Documentation staff can search by Museum Number, view Feature 001 artifact summary and documentation status, create exactly one primary record from the active category template, save/resume Drafts, and complete documentation as Revision 1 without changing custody.

**Independent Test Criteria**: An artifact delivered to Documentation through Feature 001 can be found by Museum Number, documented with the automatically resolved active template, saved as Draft, resumed, completed as Revision 1, and verified to leave custody/movement unchanged. Create and Draft edit are blocked when the artifact is not available to Documentation.

### Tests for User Stories 1 and 2

- [X] T075 [P] [US1] Add application tests for Museum-number-first `SearchDocumentationArtifact` with artifact summary and documentation status in `tests/MuseumSystem.Application.Tests/Documentation/SearchDocumentationArtifactUseCaseTests.cs`
- [X] T076 [P] [US1] Add application tests for `GetDocumentationWorkspace` action calculation with active template and existing record states in `tests/MuseumSystem.Application.Tests/Documentation/GetDocumentationWorkspaceUseCaseTests.cs`
- [X] T077 [P] [US1] Add application tests for `CreateDocumentationRecord` resolving active template from Artifact Category and enforcing one record per artifact in `tests/MuseumSystem.Application.Tests/Documentation/CreateDocumentationRecordUseCaseTests.cs`
- [X] T078 [P] [US2] Add application tests proving `CreateDocumentationRecord` is blocked unless `CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision)` is true in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationCustodyUseCaseTests.cs`
- [X] T079 [P] [US1] Add application tests proving existing records remain bound to the original template when Artifact Category later changes in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationTemplateBindingUseCaseTests.cs`
- [X] T080 [P] [US1] Add application tests for `GetDocumentationRecordForEdit` returning bound template fields, values, status, and concurrency token in `tests/MuseumSystem.Application.Tests/Documentation/GetDocumentationRecordForEditUseCaseTests.cs`
- [X] T081 [P] [US1] Add application tests for `SaveDocumentationDraft` value validation and no formal revision creation in `tests/MuseumSystem.Application.Tests/Documentation/SaveDocumentationDraftUseCaseTests.cs`
- [X] T082 [P] [US2] Add application tests proving Draft edit is blocked when `CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision)` is false in `tests/MuseumSystem.Application.Tests/Documentation/DraftCustodyUseCaseTests.cs`
- [X] T083 [P] [US1] Add application tests for `CompleteDocumentationRecord` required-field validation and Revision 1 baseline creation in `tests/MuseumSystem.Application.Tests/Documentation/CompleteDocumentationRecordUseCaseTests.cs`
- [X] T084 [P] [US1] Add authorization tests proving the Complete action is allowed only when both `Documentation.Edit` and `Documentation.Complete` are present and the Blazor/action boundary uses the existing ASP.NET Core authorization/policy mechanism in `tests/MuseumSystem.Application.Tests/Documentation/CompleteDocumentationAuthorizationTests.cs`
- [X] T085 [P] [US2] Add application tests proving `CompleteDocumentationRecord` is rejected when a Draft record's Artifact is no longer held by Documentation, no Revision 1 baseline is created, status remains Draft, and no Artifact custody or MovementRecord state changes occur in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationCustodyBoundaryTests.cs`
- [X] T086 [P] [US1] Add lightweight xUnit acceptance tests for search, summary, create, save Draft, resume, and complete page structure in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationPrimaryFlowTests.cs`
- [X] T087 [P] [US2] Add lightweight xUnit acceptance tests for blocked create/Draft edit messaging outside Documentation custody in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationCustodyFlowTests.cs`

### Implementation for User Stories 1 and 2

- [X] T088 [P] [US1] Create documentation record DTO contracts in `src/MuseumSystem.Application/Modules/Documentation/Contracts/DocumentationRecordDtos.cs`
- [X] T089 [P] [US1] Create dynamic form DTO contracts in `src/MuseumSystem.Application/Modules/Documentation/Contracts/DocumentationFormDtos.cs`
- [X] T090 [US1] Implement `SearchDocumentationArtifact` using existing Artifact Registry data and Museum Number in `src/MuseumSystem.Application/Modules/Documentation/SearchDocumentationArtifactUseCase.cs`
- [X] T091 [US1] Implement `GetDocumentationWorkspace` with artifact summary, documentation status, active-template status, and available actions in `src/MuseumSystem.Application/Modules/Documentation/GetDocumentationWorkspaceUseCase.cs`
- [X] T092 [US1] Implement active template resolution by Artifact Category and zero-active blocking in `src/MuseumSystem.Application/Modules/Documentation/DocumentationTemplateResolver.cs`
- [X] T093 [US2] Implement Documentation availability service delegating to `CurrentStateRules.IsHeldBy(artifact, MovementRecipientType.DocumentationDivision)` in `src/MuseumSystem.Application/Modules/Documentation/DocumentationAvailabilityService.cs`
- [X] T094 [US1] Implement `CreateDocumentationRecord` enforcing one record per artifact and original template binding in `src/MuseumSystem.Application/Modules/Documentation/CreateDocumentationRecordUseCase.cs`
- [X] T095 [US1] Implement `GetDocumentationRecordForEdit` in `src/MuseumSystem.Application/Modules/Documentation/GetDocumentationRecordForEditUseCase.cs`
- [X] T096 [US1] Implement `SaveDocumentationDraft` with validation, metadata, audit, and no revision creation in `src/MuseumSystem.Application/Modules/Documentation/SaveDocumentationDraftUseCase.cs`
- [X] T097 [US1] Implement `CompleteDocumentationRecord` with documentation business logic, required-field validation, Revision 1 baseline creation, concurrency handling, completion metadata, audit writes, and explicit custody-boundary preservation; preserve the application contract's declared authorization requirement without adding a Feature-002-specific application authorization context unless repository inspection finds an existing generic application authorization abstraction in `src/MuseumSystem.Application/Modules/Documentation/CompleteDocumentationRecordUseCase.cs`
- [X] T098 [US1] Register Phase C search/workspace/create/edit/save/complete use cases using the explicit scoped registration pattern in `src/MuseumSystem.Application/DependencyInjection.cs`
- [X] T099 [US1] Add Documentation workspace page with `Documentation.View` policy, Museum Number search, and artifact summary in `src/MuseumSystem.Web/Components/Pages/Documentation/Index.razor`
- [X] T100 [US1] Create reusable dynamic form component for Text, MultilineText, Number, Date, Boolean, SingleSelect, and MultiSelect in `src/MuseumSystem.Web/Components/Documentation/DynamicDocumentationForm.razor`
- [X] T101 [US1] Create record edit page with `Documentation.View` policy, Save Draft action, and Complete action in `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`
- [X] T102 [US1] Add action-level authorization checks through the existing ASP.NET Core authorization/policy mechanism for Save Draft requiring `Documentation.Edit` and Complete requiring both `Documentation.Edit` and `Documentation.Complete` in `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`
- [X] T103 [US2] Add clear blocked-action messages for missing active template, out-of-custody create/Draft edit, validation, authorization, and stale state in `src/MuseumSystem.Web/Components/Pages/Documentation/Index.razor` and `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`
- [X] T104 [US1] Add Documentation navigation entry for the Museum-number-first workspace in `src/MuseumSystem.Web/Components/Layout/NavMenu.razor`

**Checkpoint**: US1 and US2 are independently testable as the primary Documentation workflow and custody boundary MVP.

## Phase D: Corrections and Revision History (US4)

**Goal**: Authorized staff can correct a Completed Documentation Record without reopening it or requiring current Documentation custody, every successful correction records a non-empty reason, and history exposes one coherent authoritative revision sequence beginning with Revision 1.

**Independent Test Criteria**: A Completed record can be corrected while the artifact is not in Documentation custody; the record remains Completed; the correction produces Revision 2 with reason, author, timestamp, previous/new values, and changed field summary; history and details reconstruct Revision 1 plus ordered correction revisions using the bound template version.

### Tests for User Story 4

- [ ] T105 [P] [US4] Add application tests rejecting post-completion corrections with missing or empty Reason in `tests/MuseumSystem.Application.Tests/Documentation/CorrectCompletedDocumentationUseCaseTests.cs`
- [ ] T106 [P] [US4] Add application tests proving post-completion corrections do not require current Documentation custody and do not create movement changes in `tests/MuseumSystem.Application.Tests/Documentation/CorrectCompletedDocumentationUseCaseTests.cs`
- [ ] T107 [P] [US4] Add application tests proving first correction creates Revision 2 and later corrections increment Revision 3, Revision 4, and onward in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationRevisionNumberingTests.cs`
- [ ] T108 [P] [US4] Add application tests proving correction revisions persist previous values, new values, changed field summary, reason, author, and timestamp in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationRevisionSnapshotTests.cs`
- [ ] T109 [P] [US4] Add application tests for `GetDocumentationHistory` returning baseline completion as Revision 1 and ordered correction revisions with revision number, non-empty reason, author, timestamp, and changed field summary in `tests/MuseumSystem.Application.Tests/Documentation/GetDocumentationHistoryUseCaseTests.cs`
- [ ] T110 [P] [US4] Add application tests for `GetDocumentationRevisionDetails` returning Revision 1 baseline values with completion author/timestamp and no correction reason, and Revision 2+ previous values, new values, field-level change summary, mandatory non-empty correction reason, correction author, and correction timestamp using bound template labels/options in `tests/MuseumSystem.Application.Tests/Documentation/GetDocumentationRevisionDetailsUseCaseTests.cs`
- [ ] T111 [P] [US4] Add application tests proving revision history uses the original bound template after Artifact Category changes in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationHistoryTemplateBindingTests.cs`
- [ ] T112 [P] [US4] Add PostgreSQL append-only revision persistence tests using the shared fixture in `tests/MuseumSystem.Integration.Tests/Documentation/DocumentationRevisionPersistenceTests.cs`
- [ ] T113 [P] [US4] Add lightweight xUnit acceptance tests for correction and history page source structure in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationHistoryFlowTests.cs`

### Implementation for User Story 4

- [X] T114 [P] [US4] Create correction and revision DTO contracts in `src/MuseumSystem.Application/Modules/Documentation/Contracts/DocumentationRevisionDtos.cs`
- [ ] T115 [US4] Implement field-level change summary generation using bound template metadata in `src/MuseumSystem.Application/Modules/Documentation/DocumentationChangeSummaryService.cs`
- [ ] T116 [US4] Implement `CorrectCompletedDocumentation` with mandatory Reason, value validation, Completed-state preservation, Revision 2+ numbering, metadata, audit, and no custody check in `src/MuseumSystem.Application/Modules/Documentation/CorrectCompletedDocumentationUseCase.cs`
- [ ] T117 [US4] Implement `GetDocumentationHistory` with Revision 1 baseline summary and ordered correction revisions in `src/MuseumSystem.Application/Modules/Documentation/GetDocumentationHistoryUseCase.cs`
- [ ] T118 [US4] Implement `GetDocumentationRevisionDetails` with Revision 1 baseline values, completion author/timestamp, and no correction reason, plus Revision 2+ previous values, new values, field-level summary, mandatory non-empty correction reason, correction author/timestamp, and bound template labels/options in `src/MuseumSystem.Application/Modules/Documentation/GetDocumentationRevisionDetailsUseCase.cs`
- [ ] T119 [US4] Register Phase D correction/history/revision-detail use cases using the explicit scoped registration pattern in `src/MuseumSystem.Application/DependencyInjection.cs`
- [ ] T120 [US4] Create correction page with `Documentation.View` policy and action-level `Documentation.Edit` check in `src/MuseumSystem.Web/Components/Pages/Documentation/CorrectRecord.razor`
- [ ] T121 [US4] Create history page with `Documentation.History.View` policy and Revision 1 plus correction sequence display in `src/MuseumSystem.Web/Components/Pages/Documentation/History.razor`
- [ ] T122 [US4] Create revision details page with `Documentation.History.View` policy and bound-template value comparison display in `src/MuseumSystem.Web/Components/Pages/Documentation/RevisionDetails.razor`
- [ ] T123 [US4] Add correction and history navigation/actions from the record page with existing ASP.NET Core authorization checks in `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`
- [ ] T124 [US4] Add correction and revision-history audit actions in `src/MuseumSystem.Application/Modules/Documentation/DocumentationAuditActions.cs`

**Checkpoint**: US4 is independently testable against a completed record and does not depend on current artifact custody.
## Phase E: Documentation Permissions (US5)

**Goal**: Documentation capabilities use the existing ASP.NET Core permission model consistently across application contracts, role presets, routable Blazor pages, navigation, and action-level operations.

**Independent Test Criteria**: A DocumentationStaff user can view, create, edit, complete, view history, and view templates; only Admin can manage templates by default; Complete requires both `Documentation.Edit` and `Documentation.Complete`; all routable Documentation pages declare known policies and operations with stronger requirements perform action-level checks.

### Tests for User Story 5

- [ ] T125 [P] [US5] Add web acceptance tests proving routable Documentation pages use explicit known policies for Templates, TemplateVersionEditor, TemplateVersionDetails, Index, EditRecord, CorrectRecord, History, and RevisionDetails in `tests/MuseumSystem.Web.AcceptanceTests/Security/DocumentationPermissionMatrixTests.cs`
- [ ] T126 [P] [US5] Add application authorization matrix tests for view, create, edit, complete, history view, template view, and template manage permissions using existing permission policies without duplicating the dedicated Complete dual-permission action test in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationAuthorizationTests.cs`
- [ ] T127 [P] [US5] Add role preset tests proving `DocumentationStaff` has the approved Documentation permissions and Storekeeper, Viewer, RegistryManager, and InventoryOfficer do not receive Documentation permissions by default in `tests/MuseumSystem.Application.Tests/IdentityAccess/PermissionPolicyTests.cs`

### Implementation for User Story 5

- [ ] T128 [US5] Apply explicit `[Authorize(Policy=...)]` attributes to all routable Documentation pages in `src/MuseumSystem.Web/Components/Pages/Documentation/Templates.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionEditor.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionDetails.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/Index.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/CorrectRecord.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/History.razor`, and `src/MuseumSystem.Web/Components/Pages/Documentation/RevisionDetails.razor`
- [ ] T129 [US5] Implement action-level permission checks through the existing ASP.NET Core authorization service where page policy differs from operation permission, including Complete requiring both `Documentation.Edit` and `Documentation.Complete`, in `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/CorrectRecord.razor`, and `src/MuseumSystem.Web/Components/Pages/Documentation/Templates.razor`
- [ ] T130 [US5] Update `src/MuseumSystem.Web/Components/Routes.razor` only as needed to keep existing `AuthorizeRouteView` behavior covering Documentation routes
- [ ] T131 [US5] Verify Documentation navigation entries respect existing permission policies in `src/MuseumSystem.Web/Components/Layout/NavMenu.razor`

**Checkpoint**: US5 is independently testable through permission and role matrix tests plus source-level Blazor authorization checks.

## Phase F: Concurrency and Final Verification

**Goal**: Complete cross-cutting verification for lost-update prevention, quickstart scenarios, and scope boundaries without introducing duplicate PostgreSQL tests or a parallel authorization/custody model.

**Independent Test Criteria**: Stale writes are rejected with reload/review messaging, quickstart scenarios pass, and no Feature 001 or out-of-scope behavior is reimplemented.

### Cross-Cutting Tests and Verification

- [ ] T132 [P] Add application tests rejecting stale Draft saves without updating values in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationConcurrencyUseCaseTests.cs`
- [ ] T133 [P] Add application tests rejecting stale Complete attempts without creating Revision 1 in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationConcurrencyUseCaseTests.cs`
- [ ] T134 [P] Add application tests rejecting stale correction attempts without creating a correction revision in `tests/MuseumSystem.Application.Tests/Documentation/DocumentationConcurrencyUseCaseTests.cs`
- [ ] T135 [P] Add lightweight xUnit acceptance tests for stale-save reload/review messaging in Documentation page source without adding Playwright or bUnit in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationConcurrencyFlowTests.cs`
- [ ] T136 [P] Add lightweight xUnit acceptance tests covering quickstart workflows without adding Playwright or bUnit in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationQuickstartTests.cs`
- [ ] T137 [P] Add source-structure tests proving Feature 002 does not add controllers, APIs, microservices, external document stores, duplicated Feature 001 ownership, or out-of-scope UI controls in `tests/MuseumSystem.Web.AcceptanceTests/Documentation/DocumentationScopeBoundaryTests.cs`

### Cross-Cutting Implementation

- [ ] T138 Implement shared Documentation concurrency handling that maps stale EF writes to existing `UseCaseResult.Conflict` or `UseCaseResult.ConcurrencyConflict` with clear reload/review messages in `src/MuseumSystem.Application/Modules/Documentation/DocumentationConcurrencyHandler.cs`
- [ ] T139 Apply the shared concurrency handler to template, Draft save, completion, and correction use cases without modifying `src/MuseumSystem.Application/Common/UseCaseResult.cs` unless implementation proves a missing capability in `src/MuseumSystem.Application/Modules/Documentation/`
- [ ] T140 Add Blazor stale-state messaging and reload/review affordances to Documentation workflows in `src/MuseumSystem.Web/Components/Pages/Documentation/Index.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/EditRecord.razor`, `src/MuseumSystem.Web/Components/Pages/Documentation/CorrectRecord.razor`, and `src/MuseumSystem.Web/Components/Pages/Documentation/TemplateVersionEditor.razor`
- [ ] T141 Add or verify quickstart guidance that PostgreSQL integration tests may use Docker/test containers where appropriate without making Docker a production deployment requirement in `specs/002-dynamic-artifact-documentation/quickstart.md`
- [ ] T142 Verify Documentation pages reuse existing validation summary and layout conventions and do not duplicate Feature 001 workflows in `src/MuseumSystem.Web/Components/Pages/Documentation/`
- [ ] T143 Run `dotnet build Museum-System.sln` plus Documentation domain, application, integration, and web acceptance test suites, then record any intentionally deferred non-Feature-002 failures in `specs/002-dynamic-artifact-documentation/quickstart.md`
- [ ] T144 Execute the Feature 002 quickstart validation scenarios, record manual UAT checkpoints for SC-001, SC-008, and SC-010 without fabricating automated substitutes, and perform final traceability review from requirements to tasks in `specs/002-dynamic-artifact-documentation/tasks.md`

**Checkpoint**: Feature 002 is implementation-ready when all phases pass their tests, quickstart scenarios are validated, and Feature 001 behavior remains stable.

## Dependencies

- **Phase A** must complete first because it establishes shared domain primitives, permissions, persistence, PostgreSQL fixture infrastructure, and Feature 001 integration decisions.
- **US3 (Phase B)** can start after Phase A and should complete before creating records so active template resolution has a managed source.
- **US1/US2 (Phase C)** depend on Phase A and the active-template capabilities from Phase B.
- **US4 (Phase D)** depends on completed Documentation Records from Phase C.
- **US5 (Phase E)** depends on Phase A permissions and can be finalized after the routable pages/actions from Phases B-D exist.
- **Phase F** depends on the relevant story implementations and closes cross-cutting concurrency, quickstart, and scope verification.

## Parallel Opportunities

- Phase A inspection tasks T001-T006 can be done in parallel, then T007 records the decision.
- Phase A domain tests T008-T014 can be written in parallel before implementing T015-T027.
- Phase A PostgreSQL verification tasks T039-T045 can run in parallel after T036-T038.
- US3 test tasks T046-T056 can run in parallel, then implementation tasks T057-T074 should follow dependencies inside the template workflow.
- US1/US2 test tasks T075-T087 can run in parallel, then implementation tasks T088-T104 should follow search, workspace, create/edit, and UI order.
- US4 test tasks T105-T113 can run in parallel, then implementation tasks T114-T124 should follow correction, history, and UI order.
- US5 test tasks T125-T127 can run in parallel once permissions and planned page files are known, then implementation tasks T128-T131 apply the existing authorization model.
- Phase F verification tasks T132-T137 can run in parallel once the relevant story implementation exists.

## Implementation Strategy

1. Complete Phase A first and keep the solution buildable after each reviewable group.
2. Deliver the MVP through US3 plus US1/US2: manage an active category template, find an artifact by Museum Number, create/save/complete one Documentation Record, and enforce custody boundaries.
3. Add US4 correction/history behavior after completed records exist.
4. Finalize US5 authorization coverage across routes, roles, navigation, and action-level operations.
5. Finish with Phase F concurrency, quickstart validation, and scope-boundary checks.
6. Preserve the approved architecture throughout: modular monolith, .NET 10, Blazor, PostgreSQL, hybrid relational+JSONB persistence, one record per artifact, canonical Feature 001 custody integration, and existing authorization patterns.

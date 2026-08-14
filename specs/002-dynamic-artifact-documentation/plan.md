# Implementation Plan: Dynamic Artifact Documentation

**Branch**: `002-dynamic-artifact-documentation` | **Date**: 2026-08-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-dynamic-artifact-documentation/spec.md`

## Summary

Build a Documentation module inside the existing Museum-System modular monolith. The module lets Documentation Department staff search by Museum Number, read artifact/category/custody context from Feature 001, create or resume the one primary Documentation Record per artifact, complete it, correct Completed records with revision history, and manage category-specific versioned Documentation Templates. The plan extends the current Domain/Application/Infrastructure/Web layering, adds PostgreSQL/EF Core persistence for template definitions, documentation values, and revisions, and reuses existing Identity permissions, audit patterns, artifact registry data, and Storehouse custody/movement state.

No microservices, event bus, external archive, photography, approval workflow, exports, printing, OCR, or AI are introduced.


## Amendment Decisions

These decisions amend the plan without changing the approved modular monolith architecture or feature scope:

- Every successful post-completion correction requires a non-empty staff-facing Reason. The reason is persisted with the historical revision and shown in history/details.
- First successful completion is authoritative Revision 1. The first post-completion correction is Revision 2, then 3, and so on. Persistence may keep the completion baseline on DocumentationRecord instead of inserting a DocumentationRevision row, but application/UI history must expose one coherent sequence beginning with Revision 1.
- CompleteDocumentationRecord requires both `Documentation.Edit` and `Documentation.Complete` because completion accepts and persists field values while changing the Draft status.
- An Active template version may be retired without immediately activating a replacement. A category may temporarily have zero Active versions, and creation of new Documentation Records is blocked with a clear reason while no Active version exists. Activating a new version while another is Active must atomically retire the previous Active version and activate the new one; more than one Active version is never allowed.
- Documentation availability must use the actual stable/canonical holder or recipient representation already implemented by Feature 001. Do not compare display names such as `CurrentHolderName`; implementation tasks must inspect and reuse existing Feature 001 model/use cases rather than invent duplicate custody state.
- PostgreSQL-specific persistence behavior must be verified against PostgreSQL for JSONB mapping, migrations, uniqueness/active-version constraints, foreign keys, and optimistic concurrency. SQLite may be used only where existing generic test patterns benefit from it. PostgreSQL integration testing does not make Docker a production deployment requirement.
- Existing Draft and Completed Documentation Records remain bound to their original DocumentationTemplateVersion if the Artifact Category later changes. Automatic redocumentation or template migration is outside Feature 002.
## Technical Context

**Language/Version**: C# with .NET 10

**Primary Dependencies**: ASP.NET Core, Blazor Web App, ASP.NET Core Identity, Entity Framework Core 10, Npgsql EF Core Provider, PostgreSQL JSONB support where justified

**Storage**: PostgreSQL through the existing `MuseumDbContext` and EF Core migrations

**Testing**: xUnit unit/application/integration tests, PostgreSQL-backed persistence tests for JSONB, migrations, database constraints, foreign keys, and optimistic concurrency, with SQLite only for existing generic patterns, and Blazor-level acceptance tests in `MuseumSystem.Web.AcceptanceTests`

**Target Platform**: Existing Museum-System web deployment target; direct Windows Server execution remains supported, Docker Compose remains optional

**Project Type**: Staff-facing Blazor web application implemented as a modular monolith with Domain, Application, Infrastructure, and Web projects

**Performance Goals**: 95% of Documentation staff can find a Documentation-available artifact by Museum Number and open its dynamic form in under 30 seconds; history view identifies revisions in under 1 minute during acceptance testing

**Constraints**: Preserve Feature 001 ownership of artifact identity, categories, museum numbers, custody, movement, and Storehouse Operations; reuse existing authorization model; prevent stale saves; preserve historical template and documentation meaning; keep used template versions immutable except retirement status; no microservices or parallel authorization infrastructure

**Scale/Scope**: One Documentation module, one primary Documentation Record per artifact, versioned templates per artifact category, supported dynamic fields limited to Text, Multiline Text, Number, Date, Boolean, Single Select, and Multi Select

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Plan Response |
|-----------|--------|---------------|
| Artifact-Centered Digital Identity | PASS | Documentation references existing `ArtifactId` and Museum Number; it does not create or redefine artifacts. |
| Single Source of Truth | PASS | Artifact/category/custody data remains owned by Feature 001; Documentation reads it for decisions and display. |
| Modular Monolith First | PASS | Adds `Documentation` folders under existing Domain/Application/Web/Infrastructure projects, not new services. |
| Staff-Centered Operational Experience | PASS | Museum-number-first workflow, artifact summary, one dynamic form, Save Draft and Complete actions. |
| Integrity Before Convenience | PASS | Template versions are immutable once used; revisions preserve completed-document history; stale saves are rejected. |
| Traceable Custody, Movement, and Location | PASS | Documentation uses custody state for Draft creation/editing but never changes custody or movement. |
| Clear Domain Ownership | PASS | Module boundaries explicitly avoid duplicating Artifact Registry and Storehouse Operations ownership. |
| Controlled Image Stewardship | PASS | No photography or image management is introduced. |
| Security, Permissions, and Audit by Design | PASS | New permission constants/policies extend existing IdentityAccess model; sensitive writes and revisions are audited. |
| Verifiable Legacy Data Migration | PASS | Feature 002 does not migrate legacy data; migrations are additive and preserve Feature 001 tables. |
| Backup and Recovery Readiness | PASS | Additive database migration and historical snapshots support recovery and interpretation. |
| Infrastructure Independence | PASS | Uses existing PostgreSQL/EF Core infrastructure without Docker dependency. |
| Critical Business Rule Testing | PASS | Plan includes unit, application, integration, authorization, persistence, concurrency, and Blazor tests. |
| No Premature Over-Engineering | PASS | Avoids form-builder scripting, formulas, nested forms, microservices, CQRS, event sourcing, or event bus. |
| User-Validated Incremental Phases | PASS | Quickstart validates primary staff workflows and template/history behavior before implementation close. |

No constitution violations. Complexity Tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/002-dynamic-artifact-documentation/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- application-use-cases.md
|   `-- ui-workflows.md
|-- checklists/
|   `-- requirements.md
`-- spec.md
```

### Source Code (repository root)

```text
Museum-System.sln
src/
|-- MuseumSystem.Domain/
|   `-- Modules/
|       |-- ArtifactRegistry/          # Feature 001 owner; read/reference only
|       |-- StorehouseOperations/      # Feature 001 owner; read custody/movement only
|       |-- IdentityAccess/            # existing permission domain concepts
|       `-- Documentation/             # new Feature 002 domain model and rules
|-- MuseumSystem.Application/
|   |-- Common/
|   `-- Modules/
|       |-- ArtifactRegistry/          # existing read use cases reused by Documentation
|       |-- StorehouseOperations/      # existing custody state reused by Documentation
|       |-- IdentityAccess/            # add Documentation permission constants/policies
|       `-- Documentation/             # new use cases and DTO contracts
|-- MuseumSystem.Infrastructure/
|   |-- Persistence/
|   |   |-- MuseumDbContext.cs         # add DbSets
|   |   |-- Configurations/            # add Documentation EF configurations
|   |   `-- Migrations/                # additive Feature 002 migration
|   `-- Audit/                         # reuse existing audit writer
`-- MuseumSystem.Web/
    `-- Components/
        |-- Pages/
        |   `-- Documentation/          # new Blazor pages
        `-- Shared/                     # reuse existing validation/summary patterns

tests/
|-- MuseumSystem.Domain.Tests/
|   `-- Documentation/
|-- MuseumSystem.Application.Tests/
|   `-- Documentation/
|-- MuseumSystem.Integration.Tests/
|   `-- Documentation/
`-- MuseumSystem.Web.AcceptanceTests/
    `-- Documentation/
```

**Structure Decision**: Use the existing four-project modular monolith. Add a `Documentation` module to each layer. Do not add projects, services, API gateways, background workers, external document stores, or a separate authorization subsystem.

## Module Boundaries

### Documentation Module

**Owns**:
- Documentation Template aggregate and versions.
- Template field definitions and supported field type rules.
- Documentation Record lifecycle: Draft and Completed.
- Dynamic documentation values for one primary record per artifact.
- Documentation Revision history for post-completion corrections.
- Documentation-specific authorization boundaries and use cases.
- Documentation-specific audit actions.

**Reads from Feature 001**:
- `Artifact.ArtifactId` for identity and relationships.
- `Artifact.MuseumNumberDisplay` for search/display.
- `Artifact.BasicDescription` or existing artifact display fields for summary.
- `Artifact.CategoryId` and `Artifact.Category.CategoryCode` to resolve templates.
- The actual stable/canonical Feature 001 custody holder or recipient representation to determine whether the artifact is available to Documentation; implementation tasks must inspect existing Feature 001 model/use cases and must not compare display names such as `CurrentHolderName` for custody decisions.
- Existing Storehouse movement state for custody context display.

**Does Not Own**:
- Artifact creation or identity.
- Museum Number generation.
- Artifact Category administration.
- Storehouse locations.
- Artifact custody or movement.
- Delivery/return workflows.
- Photography, images, exports, printing, approvals, notifications, OCR, or AI.

### Artifact Registry Module

Continues to own Artifact, ArtifactCategory, MuseumNumber, artifact search and details. Documentation may call existing read use cases or query through `IMuseumDbContext` for read models, but it must not add write behavior to Artifact Registry objects except through existing Feature 001 use cases.

### Storehouse Operations Module

Continues to own movement/custody state and Storehouse delivery/return. Documentation checks whether an artifact is currently available to Documentation before creating a record or editing a Draft. Completing documentation and correcting Completed documentation must not create movement records or change custody.

### IdentityAccess Module

Continues to own users, roles, permission names, authorization policies, and role presets. Documentation adds permission constants to `PermissionNames.All` and role presets using the same claim-policy mechanism.

## Documentation Domain Design

### Template Aggregate

`DocumentationTemplate` represents the category-level template family. It references one existing `ArtifactCategory` and contains versions. It enforces one active applicable version per category.

Planned lifecycle:
- Template family can exist without an active version while being prepared.
- A version can be Draft before activation.
- A Draft version can be activated when valid.
- Activating one version for a category atomically retires any previous Active version and activates the new version so more than one Active version is never visible.
- Active versions may be retired without immediately activating a replacement; a category may temporarily have zero Active versions, and new Documentation Record creation is blocked with a clear reason until a version is activated.
- Retired versions remain available for records and history.
- Once any Documentation Record uses a version, its field definitions are immutable; only retirement status may change.

### Template Version

`DocumentationTemplateVersion` is the immutable form definition used by records. It stores version number, lifecycle status, activation/retirement metadata, and an ordered list of fields. New records resolve the currently active version for the artifact category at creation time and keep that exact version forever; existing records are never automatically migrated or rebound if the Artifact Category later changes.

### Template Fields

Supported field types are exactly:
- Text
- Multiline Text
- Number
- Date
- Boolean
- Single Select
- Multi Select

Field definition includes stable key, label, field type, required flag, display order, section/group, optional help text, and selectable options when applicable. Field keys are stable inside a version and should be unique within that version. Select options carry stable option keys plus display labels so old records retain meaning even if later versions change options.

### Documentation Record

One primary Documentation Record exists per Artifact. It is associated with exactly one Artifact and the exact Template Version used when the record was created. Its status is Draft or Completed only. Draft saves update current Draft values without creating formal revisions. Completion validates required fields, establishes authoritative Revision 1 as the baseline, records completion metadata, and does not change custody.

### Revision History

Completion is exposed to users as authoritative Revision 1. After completion, every successful correction requires a non-empty staff-facing Reason and creates the next authoritative revision: the first correction is Revision 2, then 3, and so on. Completed records remain Completed; there is no Reopen workflow. Persistence may keep the completion baseline on `DocumentationRecord` instead of inserting a `DocumentationRevision` row, but application and UI history must present one coherent sequence beginning with Revision 1. Correction revision rows therefore use the authoritative sequence number after the baseline and preserve previous content, new content, change summary, reason, author, and timestamp.

## Persistence Design

### Selected Approach: Hybrid Relational Definitions With JSONB Value Snapshots

Use relational tables for template families, template versions, template fields, template field options, documentation records, and documentation revisions. Use JSONB for dynamic value maps and immutable snapshots where the shape varies by template version.

Planned tables:
- `DocumentationTemplates`
- `DocumentationTemplateVersions`
- `DocumentationTemplateFields`
- `DocumentationTemplateFieldOptions`
- `DocumentationRecords`
- `DocumentationRevisions`

Dynamic values are stored as JSONB maps keyed by stable template field key. Template definitions remain relational for validation, uniqueness, ordering, option integrity, and template management queries. Revisions store JSONB snapshots/diffs plus non-empty correction reasons sufficient to reconstruct history.

### Why Hybrid Is Best Here

**Validation**: Relational field and option definitions make required-field checks, supported type checks, field ordering, and option membership explicit. JSONB values can be validated by application/domain rules against the exact template version.

**Historical integrity**: Used template versions are relationally immutable; Documentation Records point to the exact template version; authoritative history begins at Revision 1 on completion; correction revisions store immutable snapshots, reasons, and authorship after the baseline.

**Queryability**: Template administration, active-version lookup, field listing, required field discovery, and permission/history screens remain straightforward. Full dynamic-value analytics are out of scope, so JSONB does not block required workflows.

**Maintainability**: Avoids creating one table per artifact category or a wide sparse table full of nullable columns. Keeps dynamic form complexity contained in the Documentation module.

**Template evolution**: New template versions add new rows; old records remain bound to prior version rows and prior field keys/options.

**EF Core compatibility**: EF Core maps relational aggregates naturally and Npgsql supports JSONB columns for dynamic dictionaries/snapshots. Concurrency tokens follow the existing integer token pattern used by Feature 001.

### Alternatives Considered

| Alternative | Benefits | Rejected Because |
|-------------|----------|------------------|
| Fully relational field values table, one row per value | Strong per-field queryability and constraints | Adds many joins, complex multi-select modeling, and heavier writes for a feature whose required queries are record-centric rather than analytics-centric |
| Fully JSONB templates and values | Very flexible and compact | Weakens template management queryability, active version validation, field ordering, option management, and immutability enforcement |
| Category-specific physical tables | Strong typing per category | Not maintainable as categories evolve; creates schema churn for museum template changes and violates dynamic template goal |
| External document store | Flexible history storage | Adds infrastructure and operational complexity outside the modular monolith/PostgreSQL constraint |

## Concurrency Plan

Use optimistic concurrency consistent with existing Feature 001 patterns. `DocumentationRecord` and editable `DocumentationTemplateVersion` rows get concurrency tokens. Save/complete/correct/template-edit requests include the expected token read by the user. If another user saved first, the later save returns a conflict result, does not write changes, and tells the user to reload/review the latest record before saving again.

Concurrency cases:
- Draft save conflict: reject stale save; no formal revision is created.
- Complete conflict: reject if Draft changed since loaded; user must review latest Draft.
- Post-completion correction conflict: reject stale correction; no new authoritative revision is created until a fresh correction with a non-empty reason is saved against the latest record.
- Template Draft edit conflict: reject stale template edit.
- Active template resolution: new records use the active version at creation time; creation is blocked when the category has no active version; existing Drafts and Completed records keep their original version even if the Artifact Category later changes.

## Authorization Plan

Extend existing permission constants/policies; do not create a parallel authorization system.

Planned permissions:
- `Documentation.View`
- `Documentation.Create`
- `Documentation.Edit`
- `Documentation.Complete`
- `Documentation.History.View`
- `Documentation.Templates.View`
- `Documentation.Templates.Manage`

Role preset recommendations:
- Admin: all Documentation permissions.
- Documentation staff role: view/create/edit/complete/history/template view as appropriate; completing a record requires both `Documentation.Edit` and `Documentation.Complete`.
- Template manager role or Admin/RegistryManager extension: template management.
- Viewer: Documentation.View only if the museum permits documentation visibility.

Application contracts declare the permissions required by each Documentation operation. ASP.NET Core authorization policies and Blazor/action boundaries enforce those permissions using the existing Museum-System authorization model: routable pages use `[Authorize(Policy = ...)]`, and operations requiring permissions stronger or different from the containing page use action-level ASP.NET Core authorization checks. Do not introduce a Feature-002-specific application authorization subsystem; if repository inspection finds an existing generic application authorization abstraction during implementation, it may be reused, but Feature 002 must not invent a parallel authorization architecture. Completion specifically requires both `Documentation.Edit`, because field values are persisted, and `Documentation.Complete`, because the Draft changes to Completed. Sensitive writes produce audit entries using the existing audit writer.

## Blazor UX Plan

Add `Components/Pages/Documentation/` pages that follow existing Blazor conventions: page sections, simple tables/forms, `ValidationSummary`, permission attributes, and existing CSS. Keep the workflow work-focused, not decorative.

Primary staff workflow:
1. Documentation landing/search page opens with Museum Number search focused.
2. Search result shows artifact summary: Museum Number, artifact name/description, category, custody/location context, documentation status.
3. If the artifact is available to Documentation and has no record, show Create Documentation.
4. If a Draft exists, show Resume Draft.
5. If Completed exists, show View Documentation and Correct Documentation actions according to permission.
6. Dynamic form shows grouped fields in template order, with required indicators and help text.
7. Actions are Save Draft and Complete for Drafts; Save Correction for Completed records.
8. History view lists authoritative Revision 1 baseline and later correction revisions with author, timestamp, reason where applicable, and changed content.
9. Template administration provides category template list, version list, Draft version editor, activation, retirement, and read-only used-version view.

UX constraints:
- Do not ask staff to re-enter artifact registry fields.
- Do not ask staff to manually select a template.
- Show clear blocked-action messages for custody, missing template, validation, authorization, and concurrency conflicts.
- Avoid image controls, archive export, approval buttons, print/export actions, or Storehouse return shortcuts inside Documentation.

## Testing Strategy

### Unit Tests

- Template field type validation for all supported types.
- Required field validation before completion.
- Field key uniqueness within a template version.
- Single Select and Multi Select option validation.
- One active template version per category rule.
- Used template version immutability except retirement status.
- Documentation Record one-per-artifact invariant.
- Draft -> Completed state transition.
- Completed remains Completed after correction.
- Draft saves do not create formal revisions.
- Post-completion corrections create revisions.
- Revision comparison/change summary rules.
- Custody availability rule for create and Draft edit.
- Custody not required for Completed correction.

### Application Tests

- Search by Museum Number returns artifact summary plus documentation status.
- Create Documentation resolves active template from artifact category automatically.
- Create fails when no active template exists.
- Create and Draft edit fail when artifact is not available to Documentation.
- Complete validates required values in the use case, while existing ASP.NET Core authorization policy coverage proves the Complete action is denied when the user lacks either `Documentation.Edit` or `Documentation.Complete`.
- Complete succeeds without changing custody or movement records.
- Correct Completed succeeds regardless of current custody only when a non-empty staff-facing Reason is provided and creates the next authoritative revision.
- Template activation affects new records only; activating while another version is Active atomically retires the previous Active version.
- Existing Drafts and Completed records remain bound to original template version, including if the Artifact Category later changes.
- Permissions deny each unauthorized Documentation capability.

### Integration / Persistence Tests

- Database enforces one Documentation Record per Artifact.
- PostgreSQL integration tests verify database enforcement that no more than one active applicable template version exists per category while allowing zero active versions temporarily.
- Used template versions cannot be modified through application use cases.
- PostgreSQL integration tests verify JSONB value payload mappings round-trip supported field values correctly.
- PostgreSQL-backed integration tests verify authoritative Revision 1 baseline plus later correction revisions reconstruct history and include non-empty correction reasons.
- PostgreSQL-backed integration tests verify EF concurrency tokens reject stale Draft save, complete, correction, and template edit.
- Additive migration leaves Feature 001 artifact, movement, location, import, identity, and audit data intact.

### Blazor / Acceptance Tests

- Museum-number-first documentation flow: search, artifact summary, create, save Draft, resume, complete.
- Out-of-custody artifact blocks create/Draft edit but allows authorized Completed correction.
- Missing active template displays clear message and blocks creation.
- Required field errors are visible and actionable.
- Completed correction with a non-empty reason adds the next authoritative revision visible in history.
- Concurrent stale save shows reload/review guidance.
- Template administration creates Draft version, activates it, atomically retires any prior Active version, may retire an Active version without replacement, and shows used versions read-only.
- Documentation pages follow existing RTL/layout conventions and avoid out-of-scope actions.

## Migration and Compatibility

Feature 002 requires one additive EF Core migration that adds Documentation tables, indexes, foreign keys, and JSONB columns. The migration is schema-only; Documentation permissions and role presets are added through the existing `PermissionNames`, `MuseumRolePresets`, and `IdentitySeed` code paths. Existing Feature 001 tables and workflows must not be renamed or behaviorally changed.

Required integration points:
- Add Documentation DbSets to `IMuseumDbContext` and `MuseumDbContext`.
- Add EF configurations under `Persistence/Configurations`.
- Reference existing `Artifacts` and `ArtifactCategories` with restrictive delete behavior; existing Documentation Records are never automatically rebound or migrated when an Artifact Category changes.
- Use the actual stable/canonical Feature 001 holder or recipient representation to determine Documentation availability; implementation tasks must inspect the existing Feature 001 model/use cases and must not compare display names such as `CurrentHolderName`.
- Extend `PermissionNames.All`, role presets, and Identity seed for Documentation permissions.
- Add Blazor navigation entries according to existing navigation conventions.
- Add application use cases and DTO contracts under `Application/Modules/Documentation`.

Compatibility checks:
- Existing Artifact Registry search/details continue to work.
- Existing Storehouse delivery/return/reconciliation/corrections continue to work.
- Existing permission tests are updated to include new permissions.
- Existing migrations remain baseline; new migration is additive.
- Storehouse return continues through Storehouse Operations only.

## Technical Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Dynamic value validation drifts from template definitions | Invalid or historically misleading records | Centralize validation against exact template version in Documentation domain/application rules |
| JSONB values become hard to query later | Reporting limitations | Reporting is out of scope; keep template metadata relational and add future reporting projections only when needed |
| Template activation race creates two active versions | Wrong template chosen for new records | Atomically retire the previous Active version when activating a new one; enforce no-more-than-one-active invariant in use case and PostgreSQL constraint |
| Used template version accidentally mutates | Historical records change meaning | Application rule plus persistence tests; allow only retirement status change once used |
| Completed correction overwrites history | Loss of museum documentation integrity | Require a non-empty reason and next-numbered authoritative revision creation in the same save as the correction |
| Concurrency conflicts frustrate staff | User confusion | Clear conflict message and reload/review workflow; optimistic concurrency matches existing system pattern |
| Documentation module writes custody state by mistake | Storehouse history corruption | Keep custody changes out of Documentation use cases and assert no movement/custody changes in tests |

## Phase Plan

### Phase A - Domain and Persistence Foundation

- Add Documentation domain entities/enums/rules.
- Add EF configurations, DbSets, JSONB mappings, indexes, foreign keys, and migration.
- Add permission constants and seed updates.

### Phase B - Template Management

- Add template create/edit Draft/activate/retire/view use cases.
- Enforce no more than one active version per category, allow zero active versions temporarily, and enforce used-version immutability.
- Add template administration Blazor pages.

### Phase C - Documentation Workflow

- Add Museum Number search/read model with artifact summary and documentation status.
- Add create/resume/save Draft/complete use cases.
- Add dynamic form rendering in Blazor.
- Enforce custody for create and Draft edit.

### Phase D - Revisions, History, and Corrections

- Add Completed correction use case with mandatory non-empty reason and authoritative revision creation.
- Add history read use case and Blazor history view.
- Ensure corrections do not require current custody and do not affect movement.

### Phase E - Documentation Permissions / Authorization

- Complete authorization matrix tests.
- Apply routable page policies and action-level authorization checks through the existing ASP.NET Core authorization model.
- Verify role presets, navigation visibility, and permission boundaries.

### Phase F - Concurrency and Final Verification

- Add stale-save handling across records and templates.
- Add integration/acceptance coverage for edge cases and success criteria.
- Run final quickstart, traceability, and scope-boundary verification.

## Post-Design Constitution Check

PASS. The plan keeps Museum-System as a .NET 10 Blazor modular monolith using PostgreSQL, preserves Feature 001 ownership boundaries, uses existing authorization/audit conventions, avoids excluded capabilities and architectural overreach, and includes test coverage for the critical business rules in the clarified specification.

## Complexity Tracking

No constitution violations or intentional complexity exceptions.



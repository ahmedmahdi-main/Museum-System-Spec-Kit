# Quickstart: Dynamic Artifact Documentation Validation

This guide describes how Feature 002 should be validated after implementation. It assumes Feature 001 is already working and Museum-System can run with its normal .NET 10, Blazor, EF Core, and PostgreSQL setup.

## Prerequisites

- Feature 001 database migration and seed data are applied.
- Existing Artifact Registry and Storehouse Operations flows pass their tests.
- At least one Artifact Category exists.
- At least one Artifact exists in Storehouse custody.
- A user exists with Documentation permissions.
- A user exists with Documentation template management permissions.
- Storehouse delivery to Documentation uses the existing Feature 001 delivery workflow.

## Expected Commands

Build and test commands should remain consistent with the existing solution:

```powershell
dotnet build Museum-System.sln
dotnet test Museum-System.sln
```

Feature-specific test filtering can be added during implementation, for example by namespace or test trait, without changing this plan.

## Latest Automated Validation

Validation date: 2026-08-23

- `dotnet build Museum-System.sln`: Passed, 0 warnings, 0 errors.
- `dotnet test tests/MuseumSystem.Domain.Tests/MuseumSystem.Domain.Tests.csproj`: Passed, 44 passed, 0 failed, 0 skipped.
- `dotnet test tests/MuseumSystem.Application.Tests/MuseumSystem.Application.Tests.csproj`: Passed, 138 passed, 0 failed, 0 skipped.
- `dotnet test tests/MuseumSystem.Web.AcceptanceTests/MuseumSystem.Web.AcceptanceTests.csproj`: Passed, 82 passed, 0 failed, 0 skipped.
- `dotnet test tests/MuseumSystem.Integration.Tests/MuseumSystem.Integration.Tests.csproj`: Passed, 27 passed, 0 failed, 0 skipped.
- `dotnet test Museum-System.sln`: Passed, 291 passed, 0 failed, 0 skipped.
- Intentionally deferred non-Feature-002 failures: none.

## Manual Acceptance Measurements

These checkpoints validate human timing and usability success criteria during guided UAT; do not replace them with fabricated automated tests.

- **SC-001**: Locate an eligible Artifact by Museum Number and open its documentation form.
  - Measurement procedure: During guided museum staff UAT, time each participating Documentation employee from entering the Museum Number search to the documentation form becoming available for an eligible artifact.
  - Required threshold: At least 95% of attempts complete in under 30 seconds.
  - Status: Pending manual museum staff UAT.
- **SC-008**: Identify the Documentation revision sequence/history.
  - Measurement procedure: During guided museum staff UAT, ask users with history permission to open a Completed record history and identify the ordered revision sequence.
  - Required threshold: Each measured attempt identifies the sequence in under 1 minute.
  - Status: Pending manual museum staff UAT.
- **SC-010**: Rate the digital workflow compared with the paper workflow.
  - Measurement procedure: After guided validation of the primary workflow, collect ratings from participating Documentation staff on whether the digital workflow is no more burdensome than the current paper process.
  - Required threshold: At least 90% of participating Documentation staff rate it no more burdensome.
  - Status: Pending manual museum staff UAT.

## Scenario 1: Template Setup for an Artifact Category

1. Sign in as a user with `Documentation.Templates.Manage`.
2. Open Documentation Template Administration.
3. Select an existing Artifact Category.
4. Create a Documentation Template family if none exists.
5. Create a Draft template version.
6. Add fields covering all supported types:
   - Text.
   - Multiline Text.
   - Number.
   - Date.
   - Boolean.
   - Single Select with options.
   - Multi Select with options.
7. Mark at least one field required.
8. Activate the template version.

**Expected result**: The category has no more than one active template version. The activated version is available for new Documentation Records.

## Scenario 2: Create, Save, Resume, and Complete Documentation

1. Use existing Storehouse Operations to deliver an artifact to Documentation.
2. Sign in as Documentation staff.
3. Open Documentation workspace.
4. Search by Museum Number.
5. Confirm artifact summary shows Museum Number, artifact description/name, category, custody/location context, and documentation status.
6. Create Documentation.
7. Confirm the form uses the active template automatically.
8. Enter partial values and Save Draft.
9. Leave and return to the record.
10. Confirm values are preserved.
11. Fill all required fields and Complete.

**Expected result**: The record becomes Completed, remains bound to the original template version, exposes completion as authoritative Revision 1, and does not change artifact custody.

## Scenario 3: Custody Rules

1. Search for an artifact that is not currently available to Documentation.
2. Attempt to create Documentation.
3. Attempt to edit a Draft for an artifact no longer available to Documentation.
4. Open a Completed Documentation Record for an artifact no longer available to Documentation.
5. Save a correction as authorized Documentation staff.

**Expected result**: Create and Draft edit are blocked outside Documentation custody. Completed correction is allowed regardless of custody and creates a revision. No custody movement is created.

## Scenario 4: Template Evolution

1. Complete a Documentation Record using Template v1.
2. Create Template v2 for the same category.
3. Activate Template v2.
4. Open the existing completed record.
5. Create a new Documentation Record for a different artifact in the same category.

**Expected result**: Existing record still uses Template v1. New record uses Template v2. Template v1 is read-only except retirement status after use. If an Active version is retired without replacement, new Documentation Record creation is blocked with a clear reason until another version is activated.

## Scenario 5: Revision History

1. Open a Completed Documentation Record.
2. Correct one or more values.
3. Save correction.
4. Open Documentation History.
5. Inspect the revision.

**Expected result**: Record remains Completed. History shows completion as Revision 1 and the correction as Revision 2 with previous values, new values, changed field summary, non-empty reason, author, and timestamp. History can reconstruct prior documentation.

## Scenario 6: Stale Save Prevention

1. Open the same Draft Documentation Record in two user sessions.
2. Save changes in session A.
3. Attempt to save different changes in session B without reloading.
4. Repeat for a Completed correction and a Draft template version.

**Expected result**: Session B receives a stale-save conflict and must reload/review the latest state. No silent overwrite occurs.

## Scenario 7: Authorization Matrix

Validate each permission independently:
- `Documentation.View` can view documentation but not write.
- `Documentation.Create` can create only when custody/template rules pass.
- `Documentation.Edit` can save Drafts, correct Completed records where allowed, and is also required when completing because completion persists field values.
- `Documentation.Complete` can complete Drafts only together with `Documentation.Edit`.
- `Documentation.History.View` can inspect revisions.
- `Documentation.Templates.View` can view templates.
- `Documentation.Templates.Manage` can create/edit/activate/retire template versions.

**Expected result**: Missing permissions block the corresponding actions with clear messages and no state changes.

## PostgreSQL-Specific Persistence Checks

Validate JSONB mappings, EF migrations, database uniqueness/active-version constraints, foreign keys, and optimistic-concurrency persistence behavior against PostgreSQL. SQLite may be used only where existing generic test patterns benefit from it. PostgreSQL integration tests may use Docker/Testcontainers where appropriate, but Docker/Testcontainers are test infrastructure only and do not make Docker a production deployment requirement.

## Regression Checks for Feature 001

After Feature 002 migration and implementation:
- Artifact Registry search/details still work.
- Artifact creation and category management still work.
- Storehouse delivery and return still work.
- Movement history still records only Storehouse Operations movements.
- Reconciliation and documented corrections still work.
- Existing permission policy tests pass after adding Documentation permissions.

## Category Change Compatibility

After a Documentation Record exists, changing the Artifact Category through Feature 001 must not automatically migrate or rebind the existing Draft or Completed record to another DocumentationTemplateVersion. Automatic redocumentation or template migration is outside Feature 002.

## Out-of-Scope Confirmation

Confirm Documentation screens do not provide:
- Photography or image management.
- Laboratory or conservation workflows.
- Exhibition or loan workflows.
- External archive integration.
- Notifications.
- Approval/rejection workflows.
- PDF/Word export.
- Printing.
- OCR or AI.
- Feature 001 reimplementation controls.

## Phase F Traceability Review

Reviewed on 2026-08-23.

- FR-001 through FR-045 remain traced to Phases A-E and the quickstart scenarios above: template setup, primary documentation, custody separation, template evolution, revision history, and authorization.
- FR-046 is traced to Phase F stale-request application tests, Blazor reload/review acceptance tests, the shared EF concurrency handler, and the existing PostgreSQL optimistic-concurrency race coverage.
- SC-002, SC-003, SC-004, SC-005, SC-006, SC-007, SC-009, and SC-011 have automated validation coverage through domain, application, web acceptance, and PostgreSQL integration tests.
- SC-001, SC-008, and SC-010 are human acceptance measurements and remain pending manual museum staff UAT as recorded above.
- Scope boundaries were rechecked in Phase F source-structure tests: no controllers/API layer, microservice/service-host project, external document storage, media management, approval/export/printing/OCR/AI workflow, or Feature 001 ownership mutation was introduced by Documentation.

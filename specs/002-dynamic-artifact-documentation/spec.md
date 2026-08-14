# Feature Specification: Dynamic Artifact Documentation

**Feature Branch**: `002-dynamic-artifact-documentation`

**Created**: 2026-08-14

**Status**: Draft

**Input**: User description: "Feature 002 - Dynamic Artifact Documentation. Build the Documentation Department feature for Museum-System, integrating with the completed central artifact registry, custody/movement tracking, and Storehouse Operations from Feature 001."

## Clarifications

### Session 2026-08-14

- Q: How many primary Documentation Records may one artifact have in Feature 002? -> A: One primary Documentation Record per artifact, with Draft/Completed status and revisions preserving changes over time.
- Q: Should post-completion corrections require the artifact to be currently available to the Documentation Department? -> A: No, authorized Documentation staff may correct Completed Documentation regardless of current custody.
- Q: After a Documentation Template Version has been used by any Documentation Record, what changes should be allowed to that used version? -> A: Used template versions are read-only except retirement status; any field, label, option, order, or requirement changes require a new version.
- Q: When should Documentation Revisions be created for changes to the one primary Documentation Record? -> A: Draft edits update the Draft without formal revisions; completion creates the first authoritative version; every post-completion change creates a new revision.
- Q: How should the system handle two users trying to save changes to the same Documentation Record at the same time? -> A: Prevent stale saves; if another user saved first, the later user must review the latest record before saving.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Document an artifact by museum number (Priority: P1)

A Documentation Department employee searches for an artifact by Museum Number, confirms the essential artifact details from the central registry, opens the category-specific documentation form, saves work as a Draft, resumes it later, and marks the documentation Completed when finished.

**Why this priority**: This is the core value of the feature and must work before template administration or revision history can deliver operational benefit.

**Independent Test**: Transfer an artifact to Documentation through the existing custody workflow, search for it by Museum Number, create a documentation record using the automatically selected template, save a draft, resume it, and complete it without changing artifact custody.

**Acceptance Scenarios**:

1. **Given** an artifact currently available to the Documentation Department and a category with an active documentation template, **When** a Documentation employee searches by Museum Number, **Then** the system displays the artifact's Museum Number, artifact name, artifact category, current custody/location context, current documentation status, and an option to document it.
2. **Given** the employee opens documentation for the artifact, **When** the category has an active template, **Then** the system automatically presents the form fields from the active template version without requiring the employee to choose a template.
3. **Given** the employee has entered partial documentation, **When** they save the record as Draft, **Then** the draft remains associated with the artifact and the exact template version used to create it.
4. **Given** a saved Draft exists, **When** an authorized Documentation employee resumes it, **Then** the previously entered values are shown and can be updated before saving again or completing the record.
5. **Given** all required fields have acceptable values, **When** the employee marks the Documentation Record Completed, **Then** the record status becomes Completed and the artifact's physical custody remains unchanged.

---

### User Story 2 - Prevent draft documentation outside Documentation custody (Priority: P1)

Documentation staff can view relevant artifact information, but the system blocks creating a Documentation Record or editing a Draft when the artifact is not currently available to the Documentation Department according to the existing custody/movement state. Completed documentation corrections are governed by authorization and revision history rather than current custody.

**Why this priority**: Documentation must respect artifact custody and avoid creating operational records for artifacts outside the department's control.

**Independent Test**: Search for an artifact that is not in Documentation custody and verify that new record creation and Draft editing are blocked while registry details remain protected from redefinition; separately verify that an authorized correction to a Completed record is allowed and creates history regardless of current custody.

**Acceptance Scenarios**:

1. **Given** an artifact is in Storehouse custody, **When** a Documentation employee searches for it, **Then** the system shows the artifact's essential registry information and indicates that new documentation cannot be created and Draft documentation cannot be edited until the artifact is transferred to Documentation.
2. **Given** an artifact has a Completed Documentation Record but is no longer available to Documentation, **When** an authorized Documentation employee corrects the completed record, **Then** the system allows the correction and creates a traceable revision without changing custody.
3. **Given** a Documentation Record is completed, **When** the employee completes it, **Then** the system does not automatically return the artifact to the Storehouse or create any custody movement.

---

### User Story 3 - Manage category documentation templates (Priority: P1)

An authorized template manager creates and maintains versioned Documentation Templates for artifact categories, defines ordered fields and selectable options, activates new versions, retires older versions, and views previous versions without changing historical records.

**Why this priority**: Dynamic documentation depends on category-specific templates, and the museum must evolve documentation requirements without corrupting past work.

**Independent Test**: Create a template version for an artifact category, activate it, document an artifact with it, create and activate a new version, and verify that existing records remain tied to the original version while new records use the new active version.

**Acceptance Scenarios**:

1. **Given** an authorized user creates a Documentation Template for an artifact category, **When** they define fields, order, required status, field types, sections, help text, and selectable options, **Then** the template version can be saved and reviewed.
2. **Given** a category has multiple template versions, **When** an authorized user activates one version, **Then** new Documentation Records for that category use the active version.
3. **Given** a template version has been used by at least one Documentation Record, **When** a user attempts to change any field, label, option, display order, section, help text, or required status, **Then** the system prevents the change and directs the user to create a new version instead.
4. **Given** an older template version is no longer intended for new records, **When** it is retired, **Then** it remains viewable for records that used it and is not selected for new Documentation Records.

---

### User Story 4 - Correct completed documentation with history (Priority: P2)

An authorized Documentation employee corrects or updates a Completed Documentation Record when new information is discovered, and the system preserves a traceable revision history showing prior content, changes, author, and timestamp.

**Why this priority**: Museum documentation must remain correct over time while preserving the historical record of what was documented previously.

**Independent Test**: Complete a Documentation Record, edit it later as an authorized user, and verify that the new current content is visible while the previous completed version remains inspectable in history.

**Acceptance Scenarios**:

1. **Given** a Documentation Record is Completed and available for correction, **When** an authorized employee changes documentation values, **Then** the system creates a new historical revision rather than silently overwriting the previous completed content.
2. **Given** multiple post-completion corrections exist, **When** a user views documentation history, **Then** the system lists each revision in sequence with the author and timestamp.
3. **Given** a user inspects a specific revision, **When** they compare it with another revision, **Then** the system makes it clear what documentation content changed.

---

### User Story 5 - Enforce documentation permissions (Priority: P2)

Museum staff only access documentation and template capabilities that match their authorization, including viewing documentation, creating or editing records, completing records, viewing history, viewing templates, and managing templates.

**Why this priority**: Documentation records and templates are museum authority records and must be protected from unauthorized access or changes.

**Independent Test**: Exercise the documentation workflow with users granted different capabilities and verify that each user can perform only the actions they are authorized to perform.

**Acceptance Scenarios**:

1. **Given** a user has permission to view documentation but not edit it, **When** they open a Documentation Record, **Then** they can inspect permitted content but cannot create, edit, complete, or revise the record.
2. **Given** a user lacks permission to view documentation history, **When** they open a Completed Documentation Record, **Then** history and revision details are not available to them.
3. **Given** a user lacks template management permission, **When** they view a template, **Then** they cannot create versions, activate versions, retire versions, or change fields.
4. **Given** a user can edit documentation but lacks completion permission, or can complete documentation but lacks edit permission, **When** they attempt to Complete a Draft, **Then** the action is blocked because completion requires both `Documentation.Edit` and `Documentation.Complete`.

### Edge Cases

- An artifact Museum Number does not exist in the central registry.
- An artifact exists but is not currently available to the Documentation Department for new documentation or Draft editing.
- An artifact category has no active Documentation Template.
- A Documentation Template has required fields that are left blank during completion.
- A draft was created with a template version that has since been retired.
- A new template version is activated while another employee is editing an existing Draft.
- A Completed Documentation Record is corrected more than once, creating a separate revision for each post-completion change.
- A user attempts to change or delete a template version already used by records.
- A selectable field has options changed in a later template version while old records still reference earlier option meanings.
- Two users attempt to edit the same Draft or Completed Documentation Record at the same time; the first valid save is accepted, and any later stale save is blocked until the user reviews the latest record.
- Documentation is completed while the artifact remains in Documentation custody, then is later returned using Storehouse Operations and later corrected by authorized Documentation staff.
- A user attempts to add images, generate exports, request approval, or perform other out-of-scope actions from Documentation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow authorized Documentation staff to search for artifacts primarily by Museum Number.
- **FR-002**: The system MUST display essential artifact information from the central artifact registry, including Museum Number, artifact name, artifact category, current custody/location context, and current documentation status.
- **FR-003**: The system MUST prevent Documentation staff from creating, redefining, or editing core artifact registry data through this feature.
- **FR-004**: The system MUST verify that an artifact is currently available to the Documentation Department before allowing creation of a Documentation Record or editing of a Draft Documentation Record.
- **FR-005**: The system MUST allow users to view relevant artifact information while blocking new record creation and Draft editing when custody requirements are not met.
- **FR-006**: The system MUST determine the artifact category from the existing artifact record rather than from manual user selection.
- **FR-007**: The system MUST automatically resolve the currently active Documentation Template for the artifact's category when creating a Documentation Record.
- **FR-008**: The system MUST inform the user when no active Documentation Template exists for the artifact category and MUST prevent Documentation Record creation until an active template is available.
- **FR-009**: The system MUST generate a documentation form from the selected Documentation Template version.
- **FR-010**: The dynamic form MUST support these field types: Text, Multiline Text, Number, Date, Boolean, Single Select, and Multi Select.
- **FR-011**: Each template field definition MUST include a stable field key, display label, field type, required or optional status, display order, logical section or group, optional help text, and selectable options when applicable.
- **FR-012**: The system MUST support saving a Documentation Record as Draft without creating formal historical revisions for each Draft save.
- **FR-013**: The system MUST allow authorized Documentation staff to resume and update a Draft Documentation Record.
- **FR-014**: The system MUST allow a Documentation Record to be marked Completed only when all required template fields contain acceptable values, and completion MUST establish the first authoritative version of the Documentation Record.
- **FR-015**: The system MUST use only Draft and Completed as Documentation Record statuses for this feature.
- **FR-016**: The system MUST NOT introduce approval, rejection, pending approval, or reopen workflow states for Documentation Records.
- **FR-017**: Completing documentation MUST NOT alter artifact custody, create a movement, or return the artifact to Storehouse custody.
- **FR-018**: Returning an artifact to the Storehouse MUST remain part of existing Storehouse Operations outside this feature.
- **FR-019**: The system MUST maintain at most one primary Documentation Record per artifact; that record carries Draft or Completed status and preserves changes through revisions over time.
- **FR-020**: The system MUST associate each Documentation Record with exactly one artifact and one exact Documentation Template version.
- **FR-021**: The system MUST preserve metadata for each Documentation Record identifying who created it, when it was created, who last modified it, when it was last modified, who completed it, and when it was completed.
- **FR-022**: Authorized users MUST be able to create a Documentation Template for an artifact category.
- **FR-023**: Authorized users MUST be able to define template fields, reorder fields, mark fields required or optional, select field types, define selectable options, and organize fields into logical sections or groups.
- **FR-024**: Authorized users MUST be able to create a new template version without changing any field definition in a previously used version.
- **FR-025**: Authorized users MUST be able to activate a Documentation Template version for a category.
- **FR-026**: Authorized users MUST be able to retire an older Documentation Template version.
- **FR-027**: Authorized users MUST be able to view existing Documentation Template versions for a category.
- **FR-028**: Documentation Template versions that have been used by Documentation Records MUST be read-only except for retirement status; field keys, labels, options, display order, sections, help text, and required status MUST NOT be changed on the used version.
- **FR-029**: New Documentation Records MUST use the currently active template version for the artifact category at the time the record is created.
- **FR-030**: Existing Documentation Records MUST remain bound to the exact template version under which they were created, even after a newer version is activated.
- **FR-031**: The system MUST allow authorized Documentation staff to correct or update a Completed Documentation Record regardless of the artifact's current custody state.
- **FR-032**: Every post-completion modification MUST create a new traceable historical revision; Draft edits before first completion update the Draft without formal revision history. Revision 1 is the initial completion baseline and has no correction reason because it is not a correction; Revision 2 and later correction revisions MUST include a non-empty staff-facing correction reason.
- **FR-033**: The system MUST preserve enough revision history to answer what the documentation previously contained, what changed, who changed it, why it changed for correction revisions, and when it changed. The correction reason MUST be persisted with the resulting authoritative correction revision and visible in Documentation history and revision details.
- **FR-034**: Authorized users MUST be able to inspect the history of a Documentation Record.
- **FR-035**: Documentation history MUST clearly identify successive revisions with author and timestamp information.
- **FR-036**: The feature MUST provide one coherent Documentation capability and MUST NOT model manual and electronic documentation as separate documentation domains.
- **FR-037**: The feature MUST NOT implement artifact photography, image capture, image upload, image replacement, image deletion, or image management.
- **FR-038**: The feature MUST NOT depend on an unfinished Photography module to complete Documentation workflows.
- **FR-039**: The feature MUST NOT own or reimplement artifact identity, Museum Number generation, artifact category management, Storehouse locations, artifact custody, or artifact movement.
- **FR-040**: The system MUST distinguish Documentation workflow state from physical custody and movement state in all user-facing status and actions.
- **FR-041**: The primary employee workflow MUST minimize duplicate entry by reusing artifact data already maintained in the central artifact registry.
- **FR-042**: The primary employee workflow MUST support the sequence: enter or search Museum Number, view artifact details, open documentation, fill the dynamic form, then Save Draft or Complete.
- **FR-043**: The system MUST distinguish authorization for viewing documentation, creating documentation, editing documentation, completing documentation, viewing documentation history, viewing templates, and managing templates. Save Draft requires `Documentation.Edit`; Correct Completed Documentation requires `Documentation.Edit`; Complete requires both `Documentation.Edit` and `Documentation.Complete`; viewing history requires `Documentation.History.View`; viewing templates requires `Documentation.Templates.View`; managing templates requires `Documentation.Templates.Manage`.
- **FR-044**: The feature MUST NOT include artifact creation, core artifact editing, general artifact category administration, Storehouse location management, custody management, movement management, laboratory operations, conservation or maintenance, exhibition management, loans, external archive integration, notifications, general reporting, PDF or Word export, printing, barcode or QR functionality, OCR, artificial intelligence features, authentication redesign, authorization infrastructure redesign, or general audit infrastructure redesign.
- **FR-045**: The system MUST provide clear user feedback when a requested documentation action is blocked by custody, missing template, required field validation, status, or authorization.
- **FR-046**: The system MUST prevent stale saves to a Documentation Record; when another user has saved changes first, the later user MUST review the latest record before saving their own changes.

### Key Entities *(include if feature involves data)*

- **Documentation Record**: The single primary digital documentation authority record for one artifact, created from one exact Documentation Template version and carrying Draft or Completed status plus creation, modification, completion, and revision metadata.
- **Documentation Template**: A category-specific definition of the documentation form used for new Documentation Records, organized into versioned field sets.
- **Documentation Template Version**: A specific historical version of a Documentation Template that can be active, retired, or retained for historical records; once used by any Documentation Record, its field definitions become read-only and records remain bound to the version used at creation.
- **Template Field Definition**: A single dynamic documentation field with stable key, label, type, required status, display order, section/group, optional help text, and selectable options where relevant.
- **Documentation Revision**: A preserved historical snapshot or change record created for each post-completion modification, identifying prior content, changed content, author, and timestamp; Draft saves before first completion are not formal revisions.
- **Artifact**: The existing central registry item documented by this feature; it supplies Museum Number, name, category, and custody context but is not created or owned by this feature.
- **Artifact Category**: The existing classification used to automatically select the appropriate active Documentation Template.
- **Custody/Movement State**: The existing operational state used to determine whether an artifact is available to Documentation; it remains owned by Feature 001 workflows.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of Documentation employees can locate an artifact that is available to Documentation by Museum Number and open its documentation form in under 30 seconds during acceptance testing.
- **SC-002**: 100% of new Documentation Records created during testing use the active template version for the artifact's category without manual template selection.
- **SC-003**: 100% of attempted Documentation Record creations and Draft edits for artifacts outside Documentation custody are blocked with a clear explanation.
- **SC-004**: Documentation employees can save a Draft, leave the workflow, and resume the same Draft with previously entered values intact in 100% of tested cases.
- **SC-005**: 100% of completion attempts with missing required fields are prevented with field-specific guidance.
- **SC-006**: 100% of Completed Documentation Records tested remain bound to their original template version after a newer template version is activated.
- **SC-007**: 100% of post-completion edits tested are allowed for authorized Documentation staff regardless of current custody and create a traceable revision identifying previous content, changed content, author, and timestamp; 100% of Draft saves before first completion remain resumable without appearing as formal revisions.
- **SC-008**: Users with history permission can identify the sequence of revisions for a Documentation Record in under 1 minute during acceptance testing.
- **SC-009**: 100% of documentation completion actions tested leave artifact custody unchanged.
- **SC-010**: At least 90% of participating Documentation staff rate the primary documentation workflow as no more burdensome than the existing paper-based process after guided user validation.
- **SC-011**: 100% of tested concurrent edit conflicts prevent silent lost updates by blocking stale saves and requiring the later user to review the latest record before saving.

## Assumptions

- Feature 001 provides reliable artifact registry, category, custody/movement, Storehouse Operations, and authorization foundations for this feature to consume.
- Museum Number is the primary search key for Documentation staff, while planning may add secondary search aids if they do not expand scope or duplicate registry ownership.
- Each artifact category can have at most one active Documentation Template version at a time.
- A Draft remains tied to the template version selected when the Draft was first created, even if a new template version becomes active before completion.
- Retired template versions remain available for display and historical interpretation but are not used for new Documentation Records.
- Template changes after records exist are handled by creating new versions rather than mutating prior used versions; retirement status is the only allowed lifecycle change on a used template version.
- Revision history may be represented in whichever durable form planning determines, provided the user-facing history requirements are met and Draft saves before first completion are not presented as formal revisions.
- Existing authorization patterns will be reused; this feature defines required capabilities but does not redesign authentication or authorization infrastructure.
- Documentation staff may view artifact images in the future if permissions allow, but this feature neither manages images nor depends on image availability.
- External archival processes remain outside this feature; the digital Documentation Record is the system's documentation focus for Feature 002.








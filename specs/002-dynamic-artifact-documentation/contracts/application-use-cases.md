# Application Use Case Contracts: Dynamic Artifact Documentation

These contracts describe Application-layer use cases called by the Blazor UI inside the modular monolith. They are not HTTP API contracts and do not introduce controllers or external services.

## Shared Result Shape

Use existing `UseCaseResult` conventions:
- `Succeeded`: success/failure.
- `Messages`: staff-facing outcome messages.
- `ValidationIssues`: field or business rule failures.
- `ConcurrencyConflict`: stale-save conflict indicator where applicable.
- Returned DTO value on success.

All write use cases that change Documentation state should accept the actor identity from the existing audit/identity context and write audit records where sensitive.

## Permissions

- `Documentation.View`: view artifact documentation and status.
- `Documentation.Create`: create a Documentation Record.
- `Documentation.Edit`: save Drafts, correct Completed records, and supply field values during completion.
- `Documentation.Complete`: authorize changing a Draft to Completed; completion also requires `Documentation.Edit`.
- `Documentation.History.View`: view Documentation Revision history.
- `Documentation.Templates.View`: view templates and versions.
- `Documentation.Templates.Manage`: create/edit/activate/retire template versions.

## SearchDocumentationArtifact

**Input**:
- Museum Number query.

**Success**:
- Artifact summary: ArtifactId, Museum Number, artifact name/description, category id/code/name, custody/location context, documentation availability indicator, documentation status, and available actions based on permissions.

**Validation/Failure**:
- Museum Number not found.
- User lacks view permission.

**Authorization**: `Documentation.View` plus existing artifact visibility rules as applicable.

## GetDocumentationWorkspace

**Input**:
- ArtifactId.

**Success**:
- Artifact summary from Feature 001.
- Existing Documentation Record summary if present.
- Active template summary if record does not exist.
- Blocked-action reasons for custody, missing active template, authorization, or status.

**Validation/Failure**:
- Artifact not found.
- Missing active template when no record exists.
- User lacks view permission.

**Authorization**: `Documentation.View`.

## CreateDocumentationRecord

**Input**:
- ArtifactId.

**Success**:
- Creates the single primary Documentation Record in Draft state.
- Resolves active DocumentationTemplateVersion from Artifact Category.
- Initializes dynamic values as empty/default according to fields.
- Returns record id, template version id, field definitions, current values, and concurrency token. If the Artifact Category later changes, this record remains bound to the originally resolved template version.

**Validation/Failure**:
- Artifact not found.
- Artifact already has a Documentation Record.
- Artifact not currently available to Documentation.
- No active template for Artifact Category, including when the previous Active version was retired without replacement.
- User lacks create permission.

**Authorization**: `Documentation.Create`.

## GetDocumentationRecordForEdit

**Input**:
- DocumentationRecordId or ArtifactId.

**Success**:
- Artifact summary.
- Bound template version and fields.
- Current values.
- Status.
- Concurrency token.
- Available actions.

**Validation/Failure**:
- Record not found.
- User lacks view/edit permission for requested mode.

**Authorization**: `Documentation.View`; edit actions require `Documentation.Edit`.

## SaveDocumentationDraft

**Input**:
- DocumentationRecordId.
- Expected concurrency token.
- Dynamic field values keyed by field key.

**Success**:
- Updates Draft values and last modified metadata.
- Does not create DocumentationRevision.
- Returns updated concurrency token.

**Validation/Failure**:
- Record not found.
- Record is not Draft.
- Artifact not currently available to Documentation.
- Values fail field type or option validation.
- Stale concurrency token; user must reload/review latest record.
- User lacks edit permission.

**Authorization**: `Documentation.Edit`.

## CompleteDocumentationRecord

**Input**:
- DocumentationRecordId.
- Expected concurrency token.
- Dynamic field values keyed by field key.

**Success**:
- Validates required fields.
- Updates values.
- Changes status from Draft to Completed.
- Creates authoritative Revision 1 baseline snapshot, which may be stored on the DocumentationRecord but must be exposed in history as Revision 1.
- Sets completed metadata.
- Does not create custody movement or return the artifact.
- Returns completed summary and updated concurrency token.

**Validation/Failure**:
- Record not found.
- Record is not Draft.
- Artifact not currently available to Documentation.
- Required fields missing.
- Values fail field type or option validation.
- Stale concurrency token; user must reload/review latest record.
- User lacks either edit or complete permission.

**Authorization**: `Documentation.Edit` and `Documentation.Complete`.

## CorrectCompletedDocumentation

**Input**:
- DocumentationRecordId.
- Expected concurrency token.
- Corrected dynamic field values keyed by field key.
- Non-empty staff-facing correction Reason.

**Success**:
- Keeps record status Completed.
- Does not require current Documentation custody.
- Validates values against the original bound template version.
- Creates one DocumentationRevision with the next authoritative revision number, previous values, new values, change summary, non-empty Reason, author, and timestamp. The first correction is Revision 2 because completion is Revision 1.
- Updates current values and last modified metadata.
- Does not create custody movement.
- Returns updated record summary, authoritative revision number, and concurrency token.

**Validation/Failure**:
- Record not found.
- Record is not Completed.
- Values fail field type or option validation.
- Correction Reason is empty.
- Stale concurrency token; user must reload/review latest record.
- User lacks edit permission.

**Authorization**: `Documentation.Edit`.

## GetDocumentationHistory

**Input**:
- DocumentationRecordId.

**Success**:
- Baseline completion summary as Revision 1.
- Ordered correction revisions with revision number, non-empty correction reason, author, timestamp, and changed field summary.

**Validation/Failure**:
- Record not found.
- User lacks history permission.

**Authorization**: `Documentation.History.View`.

## GetDocumentationRevisionDetails

**Input**:
- DocumentationRecordId.
- RevisionNumber.

**Success**:
- Bound template version labels/options.
- Previous values.
- New values.
- Field-level change summary.
- Non-empty correction reason.
- Author and timestamp.

**Validation/Failure**:
- Record or revision not found.
- User lacks history permission.

**Authorization**: `Documentation.History.View`.

## ListDocumentationTemplates

**Input**:
- Optional ArtifactCategory filter.

**Success**:
- Template families by category with latest version, active version, status counts, and available actions.

**Authorization**: `Documentation.Templates.View`.

## CreateDocumentationTemplate

**Input**:
- ArtifactCategoryId.
- Template name and optional description.

**Success**:
- Creates template family for existing Artifact Category.
- Does not create or edit Artifact Category.

**Validation/Failure**:
- Category not found.
- Template already exists for category.
- User lacks manage permission.

**Authorization**: `Documentation.Templates.Manage`.

## CreateTemplateVersionDraft

**Input**:
- DocumentationTemplateId.
- Optional source version to copy.

**Success**:
- Creates next Draft version for editing.
- Copies fields/options from source version when requested.

**Validation/Failure**:
- Template not found.
- User lacks manage permission.

**Authorization**: `Documentation.Templates.Manage`.

## SaveTemplateVersionDraft

**Input**:
- DocumentationTemplateVersionId.
- Expected concurrency token.
- Field definitions and options.

**Success**:
- Updates Draft version fields/options.
- Returns updated concurrency token.

**Validation/Failure**:
- Version not found.
- Version is not Draft.
- Version has been used by any Documentation Record.
- Invalid field type, duplicate field key, duplicate option key, missing select options, invalid display order.
- Stale concurrency token.
- User lacks manage permission.

**Authorization**: `Documentation.Templates.Manage`.

## ActivateTemplateVersion

**Input**:
- DocumentationTemplateVersionId.
- Expected concurrency token.

**Success**:
- Validates Draft version fields/options.
- Activates selected version for its category.
- Atomically retires any previous Active version for that category and activates the new version, ensuring no more than one Active version exists.
- New Documentation Records use this version.
- Existing records remain bound to their original version.

**Validation/Failure**:
- Version not found.
- Version not valid for activation.
- Stale concurrency token.
- User lacks manage permission.

**Authorization**: `Documentation.Templates.Manage`.

## RetireTemplateVersion

**Input**:
- DocumentationTemplateVersionId.
- Expected concurrency token.

**Success**:
- Marks version Retired, including when it is the current Active version.
- Preserves historical visibility.
- Does not alter field definitions.
- May leave the category with zero Active versions temporarily; new Documentation Record creation is blocked until another version is activated.

**Validation/Failure**:
- Version not found.
- Version already retired.
- Stale concurrency token.
- User lacks manage permission.

**Authorization**: `Documentation.Templates.Manage`.

## ViewTemplateVersion

**Input**:
- DocumentationTemplateVersionId.

**Success**:
- Template version metadata, fields, options, status, used/read-only indicator, and activation/retirement history.

**Authorization**: `Documentation.Templates.View`.







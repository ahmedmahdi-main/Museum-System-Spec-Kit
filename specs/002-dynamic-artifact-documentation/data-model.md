# Data Model: Dynamic Artifact Documentation

## Overview

Feature 002 adds a Documentation module that references Feature 001 artifacts and categories. Documentation owns templates, template versions, field definitions, field options, the one primary Documentation Record per Artifact, dynamic values, and post-completion revisions. Artifact identity, Museum Number generation, Artifact Category management, custody, movement, and Storehouse return remain owned by Feature 001.

The selected persistence model is hybrid:
- Relational rows for template families, versions, fields, options, records, revisions, indexes, and lifecycle metadata.
- JSONB value maps for dynamic documentation values and revision snapshots keyed by stable template field keys.

## Entities

### DocumentationTemplate

**Purpose**: Category-level template family for Documentation forms.

**Fields**:
- `DocumentationTemplateId`: stable internal identifier.
- `ArtifactCategoryId`: reference to existing `ArtifactCategory` owned by Artifact Registry.
- `Name`: staff-facing template name.
- `Description`: optional staff-facing description.
- `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy`: audit metadata.

**Relationships**:
- Belongs to one existing Artifact Category.
- Has many DocumentationTemplateVersions.

**Rules**:
- A category may have at most one DocumentationTemplate family for Feature 002 unless future requirements explicitly allow multiple families.
- Template creation does not create, edit, or redefine Artifact Categories.
- Template family deletion is not a normal staff workflow once versions exist.

### DocumentationTemplateVersion

**Purpose**: Specific version of a template used to generate forms and bind Documentation Records.

**Fields**:
- `DocumentationTemplateVersionId`: stable internal identifier.
- `DocumentationTemplateId`: parent template family.
- `VersionNumber`: monotonically increasing number within the template family.
- `Status`: Draft, Active, or Retired.
- `ActivatedAt`, `ActivatedBy`: set when the version becomes active.
- `RetiredAt`, `RetiredBy`: set when retired.
- `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy`: metadata.
- `ConcurrencyToken`: optimistic concurrency token for Draft editing and lifecycle changes.

**Relationships**:
- Belongs to DocumentationTemplate.
- Has many DocumentationTemplateFields.
- Is referenced by DocumentationRecords that were created from it.

**Rules**:
- No more than one Active DocumentationTemplateVersion may apply to an Artifact Category at a time.
- New Documentation Records use the currently Active version for the artifact's category at creation time; if no Active version exists, creation is blocked with a clear reason.
- Existing Documentation Records remain bound to their original version and are never automatically migrated or rebound if the Artifact Category later changes.
- Once used by any Documentation Record, field definitions on the version become read-only.
- Used versions may change only retirement status.
- An Active version may be Retired without immediately activating a replacement, leaving the category with zero Active versions temporarily; Retired versions remain available for historical display and validation of existing records.
- Draft versions cannot be used to create Documentation Records.

### DocumentationTemplateField

**Purpose**: A single field definition within a template version.

**Fields**:
- `DocumentationTemplateFieldId`: stable internal identifier.
- `DocumentationTemplateVersionId`: parent version.
- `FieldKey`: stable key unique within the template version.
- `Label`: staff-facing label.
- `FieldType`: Text, MultilineText, Number, Date, Boolean, SingleSelect, MultiSelect.
- `IsRequired`: required/optional flag.
- `DisplayOrder`: order within section and form.
- `Section`: logical group/section name.
- `HelpText`: optional staff-facing guidance.

**Relationships**:
- Belongs to DocumentationTemplateVersion.
- Has options when FieldType is SingleSelect or MultiSelect.

**Rules**:
- `FieldKey` is required and unique within the version.
- `FieldKey` remains stable for that version.
- Select fields require at least one option before activation.
- Non-select fields must not require selectable options.
- Display order must be deterministic within a version.
- Field definitions on a used version cannot be edited.

### DocumentationTemplateFieldOption

**Purpose**: Stable selectable option for Single Select and Multi Select fields.

**Fields**:
- `DocumentationTemplateFieldOptionId`: stable internal identifier.
- `DocumentationTemplateFieldId`: parent field.
- `OptionKey`: stable option key unique within the field.
- `Label`: staff-facing option label.
- `DisplayOrder`: order in option list.

**Rules**:
- Option keys remain stable inside the field.
- Used version options cannot be changed, removed, or reordered.
- Later template versions may define different options without changing old records.

### DocumentationRecord

**Purpose**: The single primary digital documentation authority record for one Artifact.

**Fields**:
- `DocumentationRecordId`: stable internal identifier.
- `ArtifactId`: reference to existing Artifact.
- `DocumentationTemplateVersionId`: exact template version used at record creation.
- `Status`: Draft or Completed.
- `Values`: JSONB map from `FieldKey` to typed value representation.
- `CompletedBaselineValues`: JSONB snapshot of values at first completion, exposed in application/UI history as authoritative Revision 1.
- `CreatedAt`, `CreatedBy`: creation metadata.
- `LastModifiedAt`, `LastModifiedBy`: latest save/correction metadata.
- `CompletedAt`, `CompletedBy`: completion metadata.
- `ConcurrencyToken`: optimistic concurrency token for stale-save detection.

**Relationships**:
- Belongs to one Artifact.
- Belongs to one DocumentationTemplateVersion.
- Has many DocumentationRevisions after completion.

**Rules**:
- At most one DocumentationRecord may exist per Artifact.
- Record creation requires artifact availability to Documentation custody state.
- Draft editing requires artifact availability to Documentation custody state.
- Completion validates required fields and establishes first authoritative baseline.
- Completion does not change Artifact custody or create MovementRecord rows.
- Post-completion correction does not require current Documentation custody.
- Completed records remain Completed after correction; no Reopen status exists.
- Stale saves are rejected when `ConcurrencyToken` no longer matches the user's expected value.

### DocumentationRevision

**Purpose**: Historical record of each successful post-completion correction.

**Fields**:
- `DocumentationRevisionId`: stable internal identifier.
- `DocumentationRecordId`: parent record.
- `RevisionNumber`: authoritative sequence number for the record; correction rows start at 2 because first completion is exposed as Revision 1.
- `TemplateVersionId`: template version used to interpret values.
- `PreviousValues`: JSONB snapshot before the correction.
- `NewValues`: JSONB snapshot after the correction.
- `ChangeSummary`: JSONB or structured summary identifying changed field keys and previous/new values.
- `Reason`: non-empty staff-facing correction reason required for every successful post-completion correction.
- `CreatedAt`, `CreatedBy`: revision author/timestamp.

**Relationships**:
- Belongs to DocumentationRecord.
- References the same DocumentationTemplateVersion as the record.

**Rules**:
- Draft saves before first completion do not create DocumentationRevisions.
- First completion establishes authoritative Revision 1; persistence may store this baseline on DocumentationRecord instead of inserting a DocumentationRevision row, but history must expose Revision 1 coherently.
- Every post-completion correction creates exactly one new correction revision in the same successful save, with the next authoritative revision number starting at 2 and a non-empty staff-facing Reason.
- Revision history must reconstruct previous documentation content and identify who changed what, why, and when.
- Revisions are append-only in normal staff flows.

## Value Representation

`Values`, `CompletedBaselineValues`, `PreviousValues`, and `NewValues` use JSONB maps keyed by `FieldKey`.

Recommended logical value shapes:
- Text, MultilineText: string or null.
- Number: numeric value or null.
- Date: ISO date string or null.
- Boolean: true/false or null.
- SingleSelect: selected option key or null.
- MultiSelect: array of selected option keys.

Validation is performed against the exact DocumentationTemplateVersion associated with the record:
- Required fields must be non-empty before completion.
- Number fields must contain numeric values.
- Date fields must contain valid dates.
- SingleSelect values must match one option key from the field.
- MultiSelect values must match zero or more option keys from the field.
- Unknown field keys are rejected or ignored only by an explicit validation rule; default is reject to preserve correctness.

## State Diagrams

### Documentation Record

```text
Draft
  -> SaveDraft -> Draft
  -> Complete -> Completed, exposed as Revision 1

Completed
  -> CorrectCompleted with non-empty Reason -> Completed with next authoritative DocumentationRevision
```

No PendingApproval, Approved, Rejected, or Reopen states exist in Feature 002.

### Template Version

```text
Draft
  -> Activate -> Active

Active
  -> Retire -> Retired, possibly leaving zero Active versions temporarily

Draft
  -> Discard/Delete only if unused and allowed by template management rules
```

Used versions are immutable except retirement status.

## Integrity Constraints

- Unique DocumentationRecord per `ArtifactId`.
- Foreign key from DocumentationRecord to existing Artifact with restrictive delete behavior.
- Foreign key from DocumentationTemplate to existing ArtifactCategory with restrictive delete behavior.
- Unique DocumentationTemplate per ArtifactCategory for Feature 002.
- Unique VersionNumber within a DocumentationTemplate.
- No more than one Active DocumentationTemplateVersion per ArtifactCategory at a time; zero Active versions is allowed temporarily and blocks new Documentation Record creation.
- Unique FieldKey within a DocumentationTemplateVersion.
- Unique OptionKey within a DocumentationTemplateField.
- DocumentationRecord `Status` limited to Draft and Completed.
- DocumentationTemplateVersion `Status` limited to Draft, Active, and Retired.
- ConcurrencyToken on DocumentationRecord and DocumentationTemplateVersion.
- Correction revision rows are append-only through application workflows and use authoritative revision numbers after Revision 1 baseline.

## Custody Availability Rule

An Artifact is available to Documentation when Feature 001 current state indicates it is out of storage and held by the Documentation division/department according to the actual stable/canonical Storehouse holder or recipient representation already implemented by Feature 001. The Documentation module should centralize this check in an application/domain rule, should inspect and reuse existing Feature 001 model/use cases, must not compare display names such as `CurrentHolderName`, and should not introduce new custody fields.

Effects:
- Required for creating a DocumentationRecord.
- Required for editing a Draft DocumentationRecord.
- Not required for correcting a Completed DocumentationRecord.
- Never changes custody by itself.


## Category Change Rule

If Feature 001 later changes an Artifact Category after a DocumentationRecord exists, the existing Draft or Completed DocumentationRecord remains bound to its original DocumentationTemplateVersion. Feature 002 must not automatically migrate values, rebind the record to another template version, or create an automatic redocumentation workflow.
## Audit Events

Use the existing audit writer for sensitive write actions:
- `Documentation.Template.Create`
- `Documentation.TemplateVersion.SaveDraft`
- `Documentation.TemplateVersion.Activate`
- `Documentation.TemplateVersion.Retire`
- `Documentation.Record.Create`
- `Documentation.Record.SaveDraft`
- `Documentation.Record.Complete`
- `Documentation.Record.CorrectCompleted`

Audit entries should identify module `Documentation`, entity name, entity id, summary, and change summary.


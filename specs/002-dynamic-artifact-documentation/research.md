# Research: Dynamic Artifact Documentation

## Decision: Keep Documentation as a module in the existing modular monolith

**Rationale**: The feature is tightly coupled to Artifact Registry identity, Artifact Category, Museum Number display, custody state, Storehouse delivery/return boundaries, Identity permissions, and audit. Keeping it in the current Domain/Application/Infrastructure/Web structure preserves single deployment, one database, existing authorization, and direct transactional consistency.

**Alternatives considered**:
- Microservice: rejected because it duplicates artifact/custody ownership, adds integration complexity, and violates the explicit architecture constraint.
- Separate application: rejected because staff workflows need artifact summary, custody checks, and documentation in one low-navigation experience.
- Shared library only: rejected because Feature 002 includes persistence, UI, permissions, and use cases, not only domain helpers.

## Decision: Documentation reads Feature 001 state but does not own or mutate it

**Rationale**: Feature 001 is the source of truth for Artifact, ArtifactCategory, MuseumNumber, custody, movement, and Storehouse Operations. Documentation needs that state to decide whether creation/Draft editing is allowed and to display artifact context, but Documentation completion/correction must not transfer custody. Availability must use the actual stable/canonical holder or recipient representation already implemented by Feature 001, not display names such as `CurrentHolderName`.

**Repository finding: Feature 001 canonical Documentation custody representation**: Feature 001 currently represents Documentation delivery with `MovementRecipientType.DocumentationDivision`. When an artifact is delivered to an internal holder, `Artifact.CurrentStatus` becomes `OutOfStorage` and `Artifact.CurrentHolderType` is populated from `recipientType.ToString()`. Feature 002 custody availability therefore delegates holder interpretation to Feature 001 through the read-only helper `CurrentStateRules.IsHeldBy(Artifact, MovementRecipientType)`. Documentation must not compare `CurrentHolderName` or other display names for custody or authorization decisions.

**Alternatives considered**:
- Duplicate custody fields in Documentation: rejected because it creates stale state and ownership ambiguity.
- Comparing staff-facing holder display names: rejected because display names are not stable business identifiers for custody decisions.
- Create Documentation-specific movement records: rejected because Storehouse Operations owns movements and returns.
- Store only Museum Number text in Documentation: rejected because it weakens referential integrity and category/template resolution.

## Decision: Use a Documentation Template family with versioned template aggregate

**Rationale**: A template family per Artifact Category keeps museum documentation requirements organized by category while version rows preserve change history. At most one active applicable version per category gives deterministic template selection for new records, while allowing a category to temporarily have zero active versions during administration. Draft, Active, and Retired version states support preparation, use, temporary unavailability, and historical retention.

**Alternatives considered**:
- One mutable template per category: rejected because historical records would change meaning.
- Employee-selected templates: rejected because the spec requires automatic selection from Artifact Category.
- Independent template per record only: rejected because it removes manageable category-level templates.
- Requiring immediate replacement when retiring Active versions: rejected because the business decision allows zero active versions temporarily and blocks new record creation with a clear reason.

## Decision: Used template versions are immutable except retirement status

**Rationale**: Once any Documentation Record uses a template version, field keys, labels, options, order, section/group, help text, and required status must remain stable so existing records remain interpretable. Retirement status is a lifecycle flag that controls future selection without altering historical meaning.

**Alternatives considered**:
- Allow typo-only edits: rejected because even label changes can alter interpretation in museum authority records.
- Allow edits and copy old definitions into records: rejected because it increases complexity and risks inconsistency when displaying historical forms.
- Allow free edits: rejected because it contradicts historical integrity requirements.

## Decision: Maintain exactly one primary Documentation Record per Artifact

**Rationale**: The clarified specification defines one evolving authority record per artifact. Draft, Completed, and post-completion corrections operate on that record, while revisions preserve authoritative history. A uniqueness constraint on `ArtifactId` supports this invariant.

**Alternatives considered**:
- Multiple records per artifact: rejected because it complicates current documentation status and authority record semantics.
- Multiple completed records with one active: rejected because it introduces lifecycle and selection rules not requested.
- Separate correction records as primary records: rejected because corrections should be revisions of the same record.

## Decision: Draft saves update current values without formal revisions

**Rationale**: Drafts are working documents. The museum needs resumable Draft work but does not require formal historical trace for every draft save. Completion is authoritative Revision 1 in the user-facing history sequence.

**Alternatives considered**:
- Revision for every Draft save: rejected as overly heavy for paper-like staff workflows and not required by the spec.
- No Draft persistence: rejected because Draft/resume is required.
- Draft history visible as revisions: rejected because it blurs working state with authoritative documentation history.

## Decision: Every post-completion correction creates a revision

**Rationale**: Completed documentation is an authority record. Corrections must never silently overwrite prior content. Creating the next authoritative revision for every successful post-completion correction preserves what changed, previous content, new content, non-empty staff-facing reason, author, and timestamp. The first correction is Revision 2 because completion is Revision 1.

**Alternatives considered**:
- Optional correction reason: rejected because a non-empty staff-facing Reason is a business rule for every successful post-completion correction.
- Change notes only: rejected because they may not reconstruct prior content.
- Reopen workflow: rejected because the spec explicitly excludes it.
- New primary record per correction: rejected because one primary record per artifact is required.

## Decision: Post-completion corrections do not require current Documentation custody

**Rationale**: The clarified specification separates historical documentation correction from physical custody. Authorized Documentation staff may correct completed records when new information is found, even if the artifact has moved. The correction must not change custody.

**Alternatives considered**:
- Require custody for all edits: rejected by clarification and would block legitimate historical corrections.
- Allow Draft creation outside custody: rejected because new documentation work remains tied to Department availability.
- Allow corrections to create movement requests: rejected because movement belongs to Storehouse Operations.

## Decision: Use optimistic concurrency and reject stale saves

**Rationale**: The existing system already uses concurrency tokens for sensitive artifact state changes. Applying the same pattern to Documentation Records and editable template versions prevents silent lost updates and keeps behavior consistent for staff and tests.

**Alternatives considered**:
- Last save wins: rejected because it silently loses staff work.
- Hard edit locks: rejected as more operationally brittle and unnecessary for this workflow.
- Merge concurrent dynamic form changes automatically: rejected because it risks combining conflicting museum authority values without human review.

## Decision: Hybrid relational plus JSONB persistence

**Rationale**: Template metadata and version lifecycle need relational constraints, ordering, option management, active-version lookup, and immutability checks. Dynamic values vary by category/template version, making JSONB value maps a better fit for record values and revision snapshots. This balances validation, historical integrity, queryability, maintainability, template evolution, and EF Core/Npgsql compatibility. JSONB mappings, migrations, active-version constraints, foreign keys, and optimistic concurrency must be verified against PostgreSQL; SQLite is only acceptable for existing generic test patterns.

**Alternatives considered**:
- Fully relational value rows: good queryability but excessive joins and complexity for record-centric workflows.
- Fully JSONB templates and values: flexible but weak for template management, active version enforcement, and validation.
- Category-specific tables: strongly typed but creates schema churn and does not support dynamic template evolution well.
- External document store: flexible but adds infrastructure and violates the PostgreSQL/modular monolith constraint.


## Decision: Existing records never rebind when Artifact Category changes

**Rationale**: A Documentation Record remains bound to the exact DocumentationTemplateVersion selected at creation. If the Artifact Category later changes in Feature 001, existing Draft and Completed records keep their original template version. Automatic redocumentation or template migration is outside Feature 002.

**Alternatives considered**:
- Automatic rebinding to the new category template: rejected because it can change historical meaning and corrupt in-progress Drafts.
- Background migration of old values to a new template: rejected because redocumentation/migration is out of scope.
- Creating a second primary record for the new category: rejected because one primary Documentation Record per Artifact is required.
## Decision: Extend existing permission model with Documentation permissions

**Rationale**: Feature 001 uses permission constants, role presets, and ASP.NET Core policies. Extending that list avoids a parallel authorization model and keeps Blazor `[Authorize]`, application tests, and Identity seed behavior consistent. Completing documentation requires both `Documentation.Edit` and `Documentation.Complete` because the operation accepts and persists field values while changing status.

**Alternatives considered**:
- Hard-coded role checks in Documentation pages: rejected because existing system uses permission policies.
- New documentation-specific auth subsystem: rejected as over-engineering and a governance violation.
- Only Admin access: rejected because the spec distinguishes multiple documentation capabilities.

## Decision: Blazor UX follows existing staff workflow conventions

**Rationale**: Existing screens use Blazor pages, page sections, simple forms/tables, validation summaries, and permission attributes. Documentation should feel like part of Museum-System and minimize navigation: Museum Number search, artifact summary, dynamic form, Save Draft/Complete, history, and template administration.

**Alternatives considered**:
- Separate form-builder experience: rejected because advanced form-builder functionality is out of scope.
- Template selection by staff: rejected because the system must resolve by Artifact Category.
- Adding image/export/approval controls: rejected because those capabilities are explicitly out of scope.




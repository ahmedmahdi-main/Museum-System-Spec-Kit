# UI Workflow Contracts: Dynamic Artifact Documentation

These workflows describe Blazor staff-facing behavior. They follow the existing page-based Museum-System UI conventions and avoid implementation details such as component class names or database access.

## Navigation Entry

Add a Documentation navigation area visible to users with relevant Documentation permissions. It should lead to the museum-number-first Documentation workspace. Template administration should be separate and visible only to users with template permissions.

## Workflow 1: Find Artifact for Documentation

**Actor**: Documentation staff with `Documentation.View`.

**Steps**:
1. User opens Documentation workspace.
2. Museum Number search is the primary input.
3. User enters/scans Museum Number and searches.
4. Page displays artifact summary:
   - Museum Number.
   - Artifact name/description.
   - Artifact Category.
   - Current custody/location context.
   - Current documentation status.
5. Page displays available Documentation actions based on custody, template availability, status, and permissions.

**Expected blocked states**:
- Museum Number not found.
- User lacks permission.
- Artifact not currently available to Documentation for create/Draft edit.
- No active template for category.

## Workflow 2: Create and Save Draft Documentation

**Actor**: Documentation staff with `Documentation.Create` and `Documentation.Edit`.

**Preconditions**:
- Artifact exists.
- Artifact is currently available to Documentation.
- Artifact has no primary Documentation Record.
- Artifact category has one active template version.

**Steps**:
1. User selects Create Documentation from artifact summary.
2. System automatically resolves template by artifact category.
3. Dynamic form renders fields by section and display order.
4. User enters values.
5. User selects Save Draft.
6. Page confirms Draft saved and keeps values available for later resume.

**Expected blocked states**:
- Missing active template.
- Artifact no longer available to Documentation.
- Stale save because another user saved first.
- User lacks create/edit permission.

## Workflow 3: Resume Draft and Complete

**Actor**: Documentation staff with both `Documentation.Edit` and `Documentation.Complete`.

**Preconditions**:
- Draft Documentation Record exists.
- Artifact remains currently available to Documentation.

**Steps**:
1. User searches by Museum Number or opens Draft from status list.
2. Page displays artifact summary and dynamic form with saved values.
3. User edits values.
4. User selects Complete.
5. System validates all required fields.
6. On success, status becomes Completed and the first authoritative baseline is exposed as Revision 1.
7. Page shows Completed status and does not offer Storehouse return as a Documentation action.

**Expected blocked states**:
- Required fields missing or invalid.
- Artifact no longer available to Documentation.
- Stale complete because another user saved first.
- User lacks either edit or complete permission.

## Workflow 4: Correct Completed Documentation

**Actor**: Documentation staff with `Documentation.Edit`.

**Preconditions**:
- Documentation Record is Completed.
- User is authorized to edit documentation.

**Steps**:
1. User opens Completed Documentation Record.
2. User selects Correct Documentation.
3. Page displays current values using original bound template version.
4. User changes values.
5. User enters a non-empty staff-facing Reason and saves correction.
6. System creates the next authoritative revision and keeps the record Completed.
7. Page confirms correction and exposes updated history to permitted users.

**Important behavior**:
- Current artifact custody is not required for the correction.
- Correction must not change custody or create movement records.
- Stale correction must be rejected with reload/review guidance.

## Workflow 5: View Documentation History

**Actor**: User with `Documentation.History.View`.

**Steps**:
1. User opens a Completed Documentation Record.
2. User opens History.
3. Page lists the completion baseline as Revision 1 and correction revisions beginning at Revision 2 in chronological order.
4. Each correction revision shows revision number, non-empty reason, author, timestamp, and changed field summary.
5. User opens a revision to inspect previous and new values using template labels/options from the bound template version.

**Expected blocked states**:
- User lacks history permission.
- Revision not found.

## Workflow 6: Manage Documentation Templates

**Actor**: Template manager with `Documentation.Templates.Manage`.

**Steps**:
1. User opens Template Administration.
2. User selects an existing Artifact Category.
3. User creates template family if none exists.
4. User creates or copies a Draft version.
5. User defines fields: key, label, type, required flag, order, section, help text, and options where applicable.
6. User saves Draft version.
7. User activates Draft version after validation.
8. User views old versions and retires versions no longer intended for new records, including retiring an Active version without immediately activating a replacement.

**Important behavior**:
- Used versions are displayed read-only except retirement status.
- Activating a new version atomically retires any prior Active version and does not rewrite existing records.
- No more than one active version applies per category; zero active versions may exist temporarily, and new Documentation Record creation is blocked with a clear reason.

## Workflow 7: Artifact Category Changes After Documentation

**Actor**: Documentation staff with `Documentation.View`.

**Expected behavior**:
- If Feature 001 changes an Artifact Category after a Documentation Record exists, the Documentation Record remains bound to its original DocumentationTemplateVersion.
- The UI must not automatically migrate, rebind, or redocument the existing Draft or Completed record.
- Automatic redocumentation/template migration is outside Feature 002.

## Workflow 8: Stale Save Conflict

**Actor**: Any authorized editor.

**Steps**:
1. User A and User B open the same record or template version.
2. User A saves successfully.
3. User B attempts to save with stale state.
4. System rejects User B's save.
5. Page tells User B to reload/review the latest record before saving again.
6. No silent overwrite occurs.

## Out-of-Scope UI Controls

Documentation pages must not add controls for:
- Artifact creation.
- Core artifact editing.
- Artifact category administration.
- Storehouse return or custody transfer.
- Photography or image management.
- Approval/rejection workflows.
- Notifications.
- PDF/Word export.
- Printing.
- Barcode/QR.
- OCR or AI.


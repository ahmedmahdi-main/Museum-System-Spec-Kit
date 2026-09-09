# Feature 003 UI Review - T126

**Checkpoint**: T126 only
**Date**: 2026-09-08
**Branch**: `003-artifact-photography-image-stewardship`
**Starting HEAD**: `b4fa88df0915fe719c6c0974edce50ae41b9f76a`
**Mode**: Design Review mode using `.agents/skills/frontend-design-review/SKILL.md`
**Evidence method**: Source/static review against `docs/design-system.md`, the Feature 003 spec/plan/quickstart/tasks, the Photography Razor surfaces, artifact integration surfaces, and centralized CSS.
**Runtime inspection**: Not performed. Rendered browser/device inspection was not available in this review; no Figma, Storybook, keyboard-test, contrast-measurement, mobile-device, or WCAG-conformance verification is claimed. Findings are based on source, centralized styles, existing acceptance coverage, and design-system evidence.

## Materials Reviewed

- `.agents/skills/frontend-design-review/SKILL.md`
- `.agents/skills/frontend-design-review/references/review-output-format.md`
- `.agents/skills/frontend-design-review/references/quick-checklist.md`
- `.agents/skills/frontend-design-review/references/review-type-modifiers.md`
- `docs/design-system.md`
- `specs/003-artifact-photography-image-stewardship/spec.md`
- `specs/003-artifact-photography-image-stewardship/plan.md`
- `specs/003-artifact-photography-image-stewardship/quickstart.md`
- `specs/003-artifact-photography-image-stewardship/tasks.md`
- `src/MuseumSystem.Web/Components/Pages/Photography/Gallery.razor`
- `src/MuseumSystem.Web/Components/Pages/Photography/Upload.razor`
- `src/MuseumSystem.Web/Components/Pages/Photography/Requests.razor`
- `src/MuseumSystem.Web/Components/Photography/PhotographyGalleryToolbar.razor`
- `src/MuseumSystem.Web/Components/Photography/PhotographyImageDeletionDialog.razor`
- `src/MuseumSystem.Web/Components/Photography/PhotographyImageManagementPanel.razor`
- `src/MuseumSystem.Web/Components/Photography/PhotographyRequestPanel.razor`
- `src/MuseumSystem.Web/Components/Photography/PhotographyUploadResults.razor`
- `src/MuseumSystem.Web/Components/Layout/NavMenu.razor`
- `src/MuseumSystem.Web/Components/Pages/Artifacts/Details.razor`
- `src/MuseumSystem.Web/Components/Pages/Artifacts/Search.razor`
- `src/MuseumSystem.Web/wwwroot/app.css`

No Photography-specific `.razor.css` files were present. Photography CSS is centralized in `app.css`.

## Verdict

**Executive verdict: Needs Work, non-blocking.**

The Photography UI is broadly aligned with the museum register language: Arabic-first copy, RTL-aware shared primitives, permission-gated entry points, artifact identity context, opaque image endpoints, semantic tables, and conservative action hierarchy. No blocking issue was found, and no production code was changed for this checkpoint.

The remaining issues are follow-up design/UX refinements: one major workflow-friction issue in the request fulfillment loop and two minor accessibility/RTL refinements.

## Pillar Assessment

| Pillar | Status | Summary |
| --- | --- | --- |
| Frictionless Insight to Action | Needs Attention / Yellow | Core upload, gallery, request, metadata, primary-image, and deletion flows are visible and organized, but request fulfillment still forces a detour through the generic upload page when no eligible set exists. |
| Quality Craft | Needs Attention / Yellow | UI uses the centralized museum tokens/classes and avoids local competing systems. Minor mixed-direction identifier/date handling and live-region consistency should be tightened. |
| Trustworthy Building | Pass / Green | Destructive actions are explicit, irreversible deletion is confirmed, unavailable media states are visible, concurrency/permission/storage failures are translated into staff-facing messages, and raw MinIO/storage internals are not exposed in reviewed markup. |

## Design-System Compliance

- Uses `page-header`, `page-section`, `section-header`, `section-body`, `artifact-state`, `summary-grid`, `summary-item`, `table-wrap`, `data-table`, `badge-status`, `status-message`, `warning-message`, `form-grid`, `form-control`, `form-actions`, `btn-primary`, `btn-secondary`, `btn-danger`, `btn-quiet`, `compact`, and `ref` where applicable across the reviewed surfaces.
- Uses centralized CSS variables from `app.css` for colors, spacing, radius, typography, focus states, badges, forms, tables, and Photography media layouts.
- No inline `style=` blocks, component-local `<style>` blocks, or Photography-specific `.razor.css` files were found.
- Feature-specific classes such as `photography-gallery-layout`, `file-picker`, `media-thumb`, `dialog-panel`, `primary-image-link`, and `photography-strip` are currently centralized in `app.css` and use existing tokens. Whether any of these should become shared primitives is deferred to T127.
- The review did not penalize the museum UI for intentionally avoiding gradients, glassmorphism, decorative heritage motifs, marketing dashboards, large motion, or generic SaaS aesthetics; those are forbidden or discouraged by `docs/design-system.md`.

## Arabic / RTL Review

- Staff-facing page titles, headings, actions, empty states, status labels, request labels, upload-result labels, table headers, and deletion warnings are Arabic-first in the reviewed Photography surfaces.
- Request, upload, and gallery terminology consistently uses museum-staff Arabic around طلبات التصوير, رفع صور القطعة, معرض الصور, الصورة الرئيسية, الإلغاء, الإكمال, and حذف الصورة.
- `body` is RTL in centralized CSS, logical CSS properties are used for important directional styling, and table/form text alignment uses `text-align: start`.
- Museum numbers and file names receive `.ref`, `<code>`, or explicit `dir="ltr"` handling in several important places.
- Minor gap: some dynamic user IDs and dates in Photography panels are not consistently isolated as LTR. See `T126-UI-002`.

## Workflow Review

- Upload flow: search artifact, review central artifact context, provide purpose/date/photographer, choose multiple JPEG/PNG files, see local previews and file-level upload results.
- Gallery flow: view artifact identity, custody/location/status context, select image thumbnails, inspect preview/metadata, see unavailable media states.
- Metadata and Primary Image flow: management tools are separated from view-only gallery, current primary state is explicit, and primary changes are secondary to image review.
- Deletion flow: uploader grace and privileged deletion are separated, destructive buttons use danger styling, confirmation text is explicit, privileged reason is required, and recovery/finalization failures remain staff-facing without storage details.
- Request flow: create request from artifact search, filter/list requests, view selected request context, cancel with confirmation, complete with eligible same-artifact/same-purpose set.
- Artifact selection, file selection, deletion confirmation, and privileged deletion reason are treated as necessary museum/safety steps, not click-count waste.
- Major gap: completing a request when no eligible set exists sends staff to the generic upload page without carrying request/artifact context. See `T126-UI-001`.

## Accessibility / Responsive Evidence

- Semantic controls are present for buttons, links, forms, `InputFile`, `InputText`, `InputSelect`, `InputTextArea`, and `InputCheckbox` in the reviewed source.
- Labels are present for form inputs, table headers are present for register views, and status/warning messages use text in addition to badge color.
- Images use operational `alt` text where the image conveys artifact content; decorative local previews use empty `alt`.
- Centralized CSS includes focus-visible treatment, `table-wrap` overflow handling, responsive gallery/file-picker breakpoints, and reduced-motion handling.
- This review does not claim completed keyboard testing, contrast measurement, mobile-device testing, or WCAG AA verification.

## Trustworthy / Error-State Evidence

- Upload partial failures are exposed through file-level results and operation summaries.
- Image or storage unavailability is represented with visible media-unavailable states.
- Validation rejection, authorization denial, and concurrency conflict paths are translated into staff-facing Arabic messages in the reviewed components.
- Deletion recovery/finalization failures are described as internal follow-up states without showing raw provider details.
- Reviewed UI does not expose MinIO endpoint, bucket names, object keys, credentials, or stack traces to staff.
- AI disclaimer is not applicable to Feature 003 because the reviewed Photography UI does not generate AI content.

## Findings

### Blocking

None found.

### Major

#### T126-UI-001 - Request fulfillment requires a redundant upload detour

**Severity**: Major
**Pillar/category**: Frictionless Insight to Action
**Paths**: `src/MuseumSystem.Web/Components/Photography/PhotographyRequestPanel.razor`; `src/MuseumSystem.Web/Components/Pages/Photography/Upload.razor`

**Evidence**: `PhotographyRequestPanel.razor` shows the no-eligible-set state and links to `/photography/upload`; `Upload.razor` starts with a fresh artifact search and states that it creates a new Photography Set only.

**Impact**: A photographer fulfilling a Pending request must leave the request panel, re-search the same artifact, create/upload the set, then return to requests and complete the request. This adds avoidable clicks and weakens continuity between the request purpose/artifact and the fulfilling upload.

**Recommendation**: In a later UI task, preserve request/artifact/purpose context when launching upload from a Pending request, or provide a request-aware upload entry point that returns staff to completion once eligible images exist.

### Minor

#### T126-UI-002 - Mixed-direction technical values are not isolated everywhere

**Severity**: Minor
**Pillar/category**: Quality Craft / Arabic RTL
**Paths**: `src/MuseumSystem.Web/Components/Pages/Photography/Gallery.razor`; `src/MuseumSystem.Web/Components/Pages/Photography/Requests.razor`; `src/MuseumSystem.Web/Components/Photography/PhotographyRequestPanel.razor`

**Evidence**: `Gallery.razor` displays photographer IDs and dates in plain `<strong>` values while file name and dimensions use `dir="ltr"`; `Requests.razor` and `PhotographyRequestPanel.razor` display requested-by, photographer, and eligible-set date/user text without a consistent `.ref`, `<code>`, or `dir="ltr"` wrapper.

**Impact**: Latin-like user IDs, date strings, or generated identifiers can read awkwardly inside Arabic RTL sentences/tables, especially with punctuation or hyphens.

**Recommendation**: Apply the design-system LTR isolation rule consistently to dates, user IDs, and filename-like values that are operational identifiers.

#### T126-UI-003 - Warning live-region semantics are inconsistent in request workflows

**Severity**: Minor
**Pillar/category**: Quality Craft / Accessibility
**Paths**: `src/MuseumSystem.Web/Components/Pages/Photography/Requests.razor`; `src/MuseumSystem.Web/Components/Photography/PhotographyRequestPanel.razor`; `src/MuseumSystem.Web/Components/Photography/PhotographyImageDeletionDialog.razor`; `src/MuseumSystem.Web/Components/Photography/PhotographyImageManagementPanel.razor`

**Evidence**: `PhotographyImageDeletionDialog.razor` and `PhotographyImageManagementPanel.razor` distinguish warning messages with `role="alert"`/assertive live handling, while `Requests.razor` and `PhotographyRequestPanel.razor` render warning-class failures through `role="status"` only.

**Impact**: Assistive technology may announce request failures less promptly than deletion/metadata failures even though the messages can block completion or cancellation.

**Recommendation**: Align request workflow warnings with the same alert/assertive pattern already used by deletion and image management messages.

## Issue Counts

- Blocking: 0; IDs: none
- Major: 1; IDs: `T126-UI-001`
- Minor: 2; IDs: `T126-UI-002`, `T126-UI-003`

## Deferred To T127

T126 did not perform a repo-wide component-gap inventory and did not mark T127 complete. The only T127-facing observation is that `file-picker`, `dialog-panel`, `media-thumb`, `primary-image-link`, and similar centralized helpers may be candidates for shared primitives if future UI work reuses them outside Photography.

## Scope Confirmation

- Production UI code reviewed only; no production code changed.
- T127 through T135 remain untouched.
- This document is the T126 review record.
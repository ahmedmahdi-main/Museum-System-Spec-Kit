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

# T127 — Repository-Wide Frontend Design-System Verification

## T127 Metadata

- **Task**: T127 repository-wide Web/UI verification against `.agents/skills/frontend-design/SKILL.md` and `docs/design-system.md`.
- **Baseline commit**: `b48c03074edd7a3ee6ce61b5f88dd84842da1891`
- **Branch**: `003-artifact-photography-image-stewardship`
- **Evidence method**: Static/source review of authored Web UI files under `src/MuseumSystem.Web`, central CSS, scoped layout CSS, the reconnect UI script, the T126 review record, and broad source scans for inline styles, embedded style blocks, hard-coded colors, physical directional CSS, typography overrides, duplicate local systems, and repeated interaction patterns.
- **Runtime visual inspection**: No.
- **Limitations**: No rendered browser, keyboard traversal, device viewport, contrast measurement, Figma, Storybook, screen-reader, or WCAG-conformance inspection is claimed. Bootstrap vendor files and generated `obj` scoped CSS were discovered but not treated as authored project design-system source.

## Coverage Inventory

Authored UI source reviewed: 38 Razor files, 3 `.razor.css` files, 2 authored CSS files, and 1 authored UI JavaScript file. The discovered Bootstrap CSS/JS files are third-party UI dependencies, and generated `obj` scoped CSS files mirror authored scoped CSS.

| Area | Razor files reviewed | CSS/JS reviewed | Result |
| --- | ---: | --- | --- |
| Shell/Layout | 3 | 3 `.razor.css`; 1 `.razor.js` | Generally aligned; one reconnect-modal CSS deviation recorded as `T127-DS-001`. |
| Shared | 1 | 0 | Aligned; shared validation summary uses centralized status messaging. |
| Home/System | 6 | `wwwroot/app.css`; `wwwroot/fonts/fonts.css` | Aligned; shell, routing, system pages, core tokens, focus, responsive, and reduced-motion foundations accounted for. |
| Artifacts | 4 | 0 | Aligned; artifact identity, museum number, status, tables, forms, empty states, and primary-image entry point use central primitives. |
| Storehouse | 5 | 0 | Aligned; delivery, return, reconciliation, locations, and correction workflows use register tables, compact forms, action bars, and status badges. |
| Documentation | 9 | 0 | Aligned; artifact documentation, templates, revisions, dynamic fields, summaries, tables, and conflict messages use shared classes. |
| Photography | 8 | 0 | Aligned with T126 limitations; T126 findings `T126-UI-001`, `T126-UI-002`, and `T126-UI-003` remain preserved and are not renumbered. |
| Imports | 1 | 0 | Aligned; import file picker, validation actions, results table, and feedback use central primitives. |
| Admin | 1 | 0 | Aligned; audit trail uses page header, page section, status message, table wrapper, and data table. |

## frontend-design Alignment

- **Subject grounding**: The UI is grounded in museum registration, artifact custody, documentation, photography stewardship, storehouse movement, imports, and audit workflows rather than generic dashboard composition.
- **Museum-specific identity**: The project-specific design direction is calm, precise, trustworthy, Iraqi museum institutional, RTL-native, operational, and register-oriented. This is a valid intentional identity for the generic `frontend-design` guidance; no marketing hero, decorative motion, gradient, or experimental visual language is required.
- **Information structure**: Screens generally open with `page-header`, then proceed through `page-section`, `sub-section`, `summary-grid`, `artifact-state`, `table-wrap`, and `data-table` structures that encode actual staff workflows.
- **Typography**: `app.css` keeps the UI on the centralized font stack and uses display/mono treatments for headings and operational references. No feature page introduces an independent font family or inappropriate Arabic letter-spacing.
- **Restraint**: The navy/bronze/neutral system is preserved. No authored Razor file contains inline styles or embedded style blocks, and no feature creates a competing decorative palette.
- **Responsive/focus/reduced motion evidence**: Central CSS provides focus-visible rules, table overflow wrappers, responsive grid behavior, and reduced-motion handling. The reconnect modal scoped CSS also includes reduced-motion handling.
- **Copy consistency**: Action labels and empty/error states are operational and Arabic-first, with safe next steps rather than implementation jargon.
- **Failure and empty states**: `status-message`, `warning-message`, `empty-state`, table empty rows, and workflow-specific explanations are reused across areas. T126's warning live-region concern remains a preserved Photography finding.

## Design-System Compliance Matrix

| Area | Identity/tokens | Shared primitives | RTL | Forms/tables | Accessibility source evidence | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Shell/Layout | Uses central shell/nav tokens; scoped layout CSS delegates to `app.css` | Nav, skip link, reconnect modal | RTL shell direction preserved; reconnect animation has physical `left` values | Not form/table heavy | Skip link, dialog semantics, reduced motion | Needs attention for `T127-DS-001`; non-blocking. |
| Shared | Uses central status classes | `ValidationSummary` | Neutral | Validation feedback | `role="alert"` for validation summary | Pass. |
| Home/System | Uses central tokens and fonts | Page header/section, index rows, status/error UI | RTL-native | Home lookup and system actions use central buttons | Error page actions and Blazor error UI present | Pass. |
| Artifacts | Museum number and artifact identity are prominent | `artifact-state`, `summary-grid`, `data-table`, `empty-state`, `ref`, shared buttons | RTL-native with reference treatment | Forms and artifact register tables use shared classes | Labels, status text with badges, table overflow | Pass. |
| Storehouse | Operational custody/location identity retained | `page-section`, `form-grid`, `data-table`, `badge-status`, `action-bar` | RTL-native | Compact movement forms and preview tables | Eligibility text plus badges, clear disabled actions | Pass. |
| Documentation | Documentation records stay artifact-centered | `summary-grid`, `data-table`, `status-message`, `warning-message`, dynamic form component | RTL-native | Template/editor forms and revision tables use shared classes | Conflict reload actions, validation/help text | Pass. |
| Photography | Photography surfaces follow central museum primitives | `artifact-state`, `file-picker`, `media-thumb`, `dialog-panel`, `summary-grid`, `data-table`, `badge-status` | RTL-native with T126 identifier isolation gaps preserved | Upload/request/gallery forms and tables use shared classes | Alt text, visible media-unavailable states, no raw storage internals | Pass with preserved T126 follow-ups. |
| Imports | Uses central file picker and register table | `file-picker`, `form-grid`, `data-table`, `status-message` | RTL-native | Import workflow uses shared buttons/forms/table | File input label and status text | Pass. |
| Admin | Quiet register-style audit view | `data-table`, `table-wrap`, `status-message` | RTL-native | Audit table uses shared table system | Empty state text and semantic table | Pass. |

## Repository-Wide Deviations

- **T127-DS-001 - Reconnect modal scoped CSS retains local sizing/positioning values**
  - **Severity/significance**: Minor / Needs attention; non-blocking.
  - **Affected area**: Shell/Layout reconnect UI.
  - **Paths**: `src/MuseumSystem.Web/Components/Layout/ReconnectModal.razor.css`
  - **Evidence**: The scoped CSS defines local dialog width/margins, token fallbacks, button padding `7px 18px`, animation dimensions `80px`, and physical `left` positions inside the reconnect animation.
  - **Why this deviates from `docs/design-system.md`**: The design system asks new UI to rely on shared tokens/primitives, token-scale spacing, and logical direction-aware properties where direction matters. This scoped reconnect styling still keeps local sizing/positioning details outside the ordinary centralized `app.css` primitive set.
  - **Classification**: Pre-existing cross-project Shell/Layout design-system deviation; not Feature 003-specific; non-blocking.
  - **Impact**: The modal still uses project colors, typography, dialog semantics, and reduced-motion handling, but it is less centralized than ordinary page/dialog styling and can drift from future shell token changes.
  - **Recommendation**: When reconnect UI is next edited, migrate remaining spacing/sizing to token values and prefer transform-based animation or logical positioning where practical.

## Centralized Component Gap Analysis

| ID/Candidate | Type | Evidence | Usage sites | Classification | Recommendation |
| --- | --- | --- | ---: | --- | --- |
| `file-picker` | CSS primitive | `src/MuseumSystem.Web/wwwroot/app.css`; `src/MuseumSystem.Web/Components/Pages/Imports/ExcelImport.razor`; `src/MuseumSystem.Web/Components/Pages/Photography/Upload.razor` | 2 independent modules | ALREADY CENTRALIZED | Keep the CSS primitive. A Razor wrapper is not required because Excel import and image upload have different accept/multiple/result behavior. |
| `dialog-panel` | CSS primitive / helper pattern | `src/MuseumSystem.Web/wwwroot/app.css`; `src/MuseumSystem.Web/Components/Photography/PhotographyImageDeletionDialog.razor` | 1 feature workflow | KEEP FEATURE-SPECIFIC | Keep current Photography deletion behavior local until another module implements the same modal/destructive interaction. |
| `media-thumb` | CSS primitive | `src/MuseumSystem.Web/wwwroot/app.css`; `src/MuseumSystem.Web/Components/Pages/Photography/Upload.razor`; `src/MuseumSystem.Web/Components/Photography/PhotographyUploadResults.razor` | 1 module | KEEP FEATURE-SPECIFIC | Keep as Photography media presentation; do not promote without another media-heavy module. |
| `primary-image-link` | CSS primitive | `src/MuseumSystem.Web/wwwroot/app.css`; `src/MuseumSystem.Web/Components/Pages/Artifacts/Search.razor` | 1 cross-feature integration point | KEEP FEATURE-SPECIFIC | Keep as a narrow artifact-search integration affordance for Photography primary images. |
| Artifact search/picker | Razor component candidate | `Documentation/Index.razor`; `Photography/Upload.razor`; `Photography/Requests.razor`; `Artifacts/Search.razor` | 3 modules | RECOMMENDED / FUTURE REUSE | Consider a shared picker only if future work needs the same search/select contract and artifact context summary across modules; current flows differ enough that it is not required. |
| Artifact identity summary / state strip | CSS primitive / helper pattern | `app.css`; `Artifacts/Details.razor`; `Documentation/Index.razor`; `Photography/Gallery.razor`; `Photography/Upload.razor`; `Photography/Requests.razor`; `PhotographyRequestPanel.razor` | 3 modules | ALREADY CENTRALIZED | Continue using `artifact-state`, `summary-grid`, `summary-item`, and `ref`; no Razor abstraction required now. |
| Destructive/confirmation dialog | Razor component candidate | `PhotographyImageDeletionDialog.razor`; `PhotographyRequestPanel.razor`; disable/remove actions in Categories, Locations, Documentation templates | Multiple action contexts | RECOMMENDED / FUTURE REUSE | Do not centralize yet. Existing interactions differ between modal deletion, inline request cancellation, and simple disable/remove actions. |
| Validation/status feedback | CSS primitive / Razor helper | `Shared/ValidationSummary.razor`; many pages using `status-message`/`warning-message`; T126-UI-003 in Photography request warnings | Many modules | ALREADY CENTRALIZED | Keep central message classes and shared validation summary. Future live-region alignment may address T126-UI-003, but no required new component is proven. |
| Museum-number/reference rendering | CSS primitive / helper pattern | `app.css`; `Artifacts/*`; `Documentation/*`; `Photography/*`; `Storehouse/*` | Many modules | ALREADY CENTRALIZED | Continue using `.ref` for museum numbers and operational identifiers. A Razor wrapper may become useful if LTR/date/user-id drift expands, but current evidence supports the CSS primitive. |
| Search/filter toolbar | CSS primitive | `app.css`; `Artifacts/Search.razor`; `Photography/Upload.razor`; `Photography/Requests.razor`; `PhotographyGalleryToolbar.razor` | 2 modules | ALREADY CENTRALIZED | Keep `register-toolbar` and `search-row` as central primitives. |
| Metadata summary grid | CSS primitive | `app.css`; Documentation, Photography, Artifact detail pages | 3 modules | ALREADY CENTRALIZED | Continue using `summary-grid`/`summary-item`. |
| File upload results / media results | Razor component candidate | `PhotographyUploadResults.razor`; `Imports/ExcelImport.razor` | 2 modules | KEEP FEATURE-SPECIFIC | Results differ by binary/media semantics versus spreadsheet validation; no required shared component. |

## Required Centralized Gaps

No required centralized component gaps were identified.

T127 found reusable patterns and future opportunities, but no candidate met the threshold for required centralization with concrete cross-module drift, duplicated accessibility-critical behavior, or incompatible reimplementation of an existing central primitive.

## T126 Candidate Resolution

- `file-picker`: **ALREADY CENTRALIZED** as a CSS primitive in `app.css`, with independent use in Imports and Photography. No required Razor component gap.
- `dialog-panel`: **KEEP FEATURE-SPECIFIC** for current behavior. The class is centralized in CSS, but only Photography deletion currently uses this modal pattern.
- `media-thumb`: **KEEP FEATURE-SPECIFIC** because current usage is Photography image upload/result presentation.
- `primary-image-link`: **KEEP FEATURE-SPECIFIC** because current usage is a narrow Artifact Search to Photography Gallery affordance.

## T127 Conclusion

Repository-wide Web/UI verification completed for the authored UI source under `src/MuseumSystem.Web`. The Web UI generally follows the `frontend-design` guidance as interpreted through the museum-specific design-system authority: it is subject-grounded, register-oriented, RTL-native, operational, restrained, and consistent with the centralized token/primitives model.

Design-system deviation count: 1 (`T127-DS-001`).

Required centralization gap count: 0.

Production corrections are recommended only as future authorized work: preserve T126 findings, consider future artifact-picker reuse if workflows converge, and normalize reconnect modal CSS during future shell maintenance. No production code changed. No tests changed. T128 and later tasks remain untouched.
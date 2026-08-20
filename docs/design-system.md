# Museum Design System

This document is the project-specific visual authority for Museum-System / نظام مخزن المتحف. Before any frontend implementation, read the `frontend-design` skill and this document. Existing design tokens and shared components must be reused; do not introduce a new feature-level visual language.

## Design Philosophy

Museum-System is an institutional operational system for registering artifacts, managing museum numbers, storage, custody, movement, documentation, laboratory/conservation work, exhibition, returns, users, permissions, and audit logs.

The interface must feel like a modern digital museum register for an Iraqi museum institution: calm, precise, trustworthy, RTL-native, and fast for repeated staff workflows. It must not feel like a generic SaaS dashboard, analytics template, e-commerce admin, or decorative heritage-themed website.

Use visual identity through hierarchy, typography, disciplined spacing, tables, and museum-specific information architecture. Do not rely on ornamental motifs, large decorative icons, gradients, glass effects, or colorful dashboard cards.

## Official Identity

Primary navy: `#183450`
Museum bronze/gold accent: `#938046`
Neutral background: `#ECECEC`
Primary Arabic typeface: `Noto Kufi Arabic`

The bronze accent is a signature mark, not a broad background color. Use it for active navigation indicators, museum number treatment, important borders, selected states, and subtle emphasis.

## Token Source

The canonical frontend tokens live in `src/MuseumSystem.Web/wwwroot/app.css` under `:root`.

Token groups include:

- Colors: `--museum-primary`, `--museum-accent`, `--museum-background`, `--museum-surface`, border tokens, text tokens, and restrained semantic colors.
- Typography: `--font-sans`, `--font-display`, `--font-mono`, type sizes, line heights, and weights.
- Spacing: `--space-1` through `--space-9`.
- Radius: `--radius-1`, `--radius-2`, `--radius-3`.
- Elevation: `--shadow-1`, `--shadow-2`.
- Borders and focus: `--border-width`, `--focus-ring`.

Compatibility aliases such as `--lapis`, `--brass`, `--paper`, and `--well` exist only to keep existing markup stable. New work should prefer the `--museum-*` tokens.

## Typography

Use `Noto Kufi Arabic` as the authoritative UI font, with local fallbacks defined in `--font-sans`. Keep weights deliberate:

- Page titles: 700
- Section headings: 600
- Important values and labels: 500 or 600
- Body, tables, help text: 400

Do not make entire pages bold. Do not add negative letter spacing. Avoid letter spacing on Arabic text; museum numbers may use slight monospace tracking because they are Latin-like codes.

## Layout And Spacing

Use full-width operational sections with constrained content. Prefer clear register-like structure over floating marketing cards.

Use shared classes:

- `page-header` for page identity and context.
- `page-section` for a major work area.
- `sub-section` for a distinct area inside a page section.
- `card-panel` only for a compact repeated or conditional panel, not for nesting page sections.
- `summary-grid` and `summary-item` for compact metadata summaries.
- `artifact-state` and `artifact-state-item` for current artifact status, custody, and location.

Spacing must come from the token scale. Avoid ad hoc values such as 13px padding or 11px radius in new code.

## Navigation And Shell

The sidebar is the strongest brand surface and uses `--museum-primary`. Active state uses a restrained bronze indicator, not a gold-filled row. Navigation must remain RTL-native and predictable.

The header area should stay functional. Do not overload it with decorative status widgets or unused global actions.

## Buttons And Actions

Use existing shared button classes:

- `btn-primary` for the main action.
- `btn-secondary` for supporting actions.
- `btn-danger` for destructive actions when needed.
- `btn-quiet` for low-emphasis shell actions.
- `compact` for dense table actions.

Do not create duplicate button systems such as `MuseumButton`, `FancyButton`, or page-specific button classes. Add a variant to the shared system only when the behavior is reusable.

## Forms

Use `form-grid`, `search-row`, `form-actions`, `span-all`, and `form-control`. Forms should be compact, labeled, and grouped around actual workflows. Avoid one giant card per field, excessive vertical gaps, or unnecessary modal editing.

Validation and help text must be readable and specific. Required state and disabled state must not rely on color alone.

## Tables

Tables are first-class components because staff work from register-like data. Use `table-wrap` around wide tables and `data-table` for the table.

Tables should be compact, readable, RTL-aligned, and easy to scan. Use strong headers, subtle row separators, and restrained hover. Avoid large colored buttons in every row unless operational speed requires it.

Artifact register columns should prioritize museum number, artifact identity, category, status, location/custody, last movement where available, and actions.

## Status Badges And Messages

Use `badge-status` with semantic modifiers:

- `badge-active`: valid/active/in storage.
- `badge-draft`: draft/pending/out of storage or needs attention.
- `badge-completed`: completed/informational.
- `badge-retired`: inactive/retired.

Use `status-message` for neutral feedback and `warning-message` for cautionary or blocked workflow states. Keep semantic colors restrained and consistent.

## Artifact Detail Pattern

The artifact is the center of the system. Artifact detail screens should feel like one complete institutional record, not isolated module fragments.

For artifact records, surface these facts near the top:

- Museum number.
- Current status.
- Current physical location.
- Current custody holder.
- Movement history or last movement where available.

Use `artifact-state` for current status, location, and custody. Do not bury these facts deep in secondary metadata.

## RTL Rules

Arabic RTL is native. Use logical properties such as `inset-inline-start`, `border-inline-start`, and `margin-inline-start` when direction matters. Keep table, form, breadcrumb, action, drawer, dialog, and pagination flow RTL-correct.

Use `direction: ltr` plus `unicode-bidi: isolate` only for codes, GUIDs, dates, file names, and museum-number-like identifiers that need left-to-right reading.

## Responsive Rules

Desktop workflows are primary, but pages must not collapse badly on smaller screens. Preserve critical operational information. Tables may scroll horizontally inside `table-wrap`; do not hide key custody/status/location columns merely for visual neatness.

Check long Arabic labels and button text at narrow widths. Text must not overlap controls or adjacent content.

## Accessibility

Maintain sufficient contrast, clear focus states, semantic markup, keyboard-accessible controls, readable validation, disabled states, and usable touch/click targets. Do not communicate status by color alone; pair badge text with color.

Respect `prefers-reduced-motion`. Motion should be minimal and only help orientation or state change.

## Component Reuse Rules

Before adding UI, ask:

1. Does a shared component or class already exist?
2. Does the design system already define this visual behavior?
3. Is this reusable?
4. Would a new variant fit better than a new component?
5. Could this introduce a competing visual pattern?
6. Which current screens might be affected?

If a missing primitive is genuinely reusable, add it to the shared design system first, document the intended use, then consume it from the feature page.

## Forbidden Patterns

Do not introduce arbitrary primary colors, page-specific palettes, gradients, glassmorphism, neon colors, large decorative icons, excessive shadows, exaggerated radius, pill-shaped everything, nested UI cards, marketing hero sections, decorative heritage motifs, or fake data to make screens look full.

Do not create feature-level CSS that redefines typography, buttons, cards, tables, forms, or status badges. Do not bypass shared components because local CSS is faster.

## Correct Usage Examples

Use a page header:

```razor
<section class="page-header">
    <p class="eyebrow">سجل القطع</p>
    <h1>البحث عن القطع</h1>
    <p>ابحث برقم المتحف أو الوصف أو الفئة.</p>
</section>
```

Use a register table:

```razor
<div class="table-wrap">
    <table class="data-table">
        <thead>
            <tr>
                <th>رقم المتحف</th>
                <th>الحالة</th>
                <th>الموقع الحالي</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td><a class="ref" href="/artifacts/@id">@museumNumber</a></td>
                <td><span class="badge-status badge-active">داخل المخزن</span></td>
                <td>@locationName</td>
            </tr>
        </tbody>
    </table>
</div>
```

Use an artifact state strip:

```razor
<div class="artifact-state" aria-label="الحالة التشغيلية الحالية للقطعة">
    <div class="artifact-state-item critical">
        <span>الحالة الحالية</span>
        <strong><span class="badge-status badge-active">داخل المخزن</span></strong>
    </div>
    <div class="artifact-state-item critical">
        <span>الموقع الحالي</span>
        <strong>@locationName</strong>
    </div>
    <div class="artifact-state-item">
        <span>الحيازة الحالية</span>
        <strong>@holderName</strong>
    </div>
</div>
```

# ADR-0015: Angular + Angular Material front-end

**Status:** Accepted (revised during Phase 0 — originally "Angular + PrimeNG")

## Context
The team works with Angular. We need a rich component set (form fields, selects, date pickers, tables, trees,
dialogs), solid RTL support, accessibility, and permissive licensing (ADR-0006).

PrimeNG was the original choice. During Phase 0 we found that PrimeNG is now distributed under the commercial
**PrimeUI License** — including the latest patch releases of the 19.x, 20.x, 21.x and 22.x lines — and shows an
"Invalid PrimeUI License" notice without a key. The free community tier is limited to organizations with less than
USD 1M revenue and fewer than 5 developers, which government entities do not meet. Only older, frozen releases remain
MIT, and they receive no fixes. PrimeNG therefore fails ADR-0006.

## Decision
- One Angular workspace with `internal`, `external`, `field` and `shared` projects.
- **Angular Material + CDK** (MIT, maintained by the Angular team) for components, with a Material 3 theme
  (`projects/shared/src/styles/dc-theme.scss`).
- RTL through the CDK `dir` directive on the app shell, driven by the current language (Arabic default).
- Icons: Material Symbols (Apache-2.0); font: IBM Plex Sans Arabic (OFL). Both bundled from npm — no CDN.
- Charts: Apache ECharts (Phase 2). Workflow canvas: an MIT library (Phase 3, license re-checked at that time).
- A shared schema-driven form renderer (`DynamicForm`) built on Angular Material.

## Consequences
- No licensing exposure for customers; components follow Angular's release and LTS cycle.
- Material's visual language is more opinionated than PrimeNG's; entity branding is done through theme tokens.
- Some advanced widgets PrimeNG offered out of the box (e.g. tree tables) are built on the CDK when needed.
- The field app is a PWA (offline via service worker + IndexedDB) — Phase 3.

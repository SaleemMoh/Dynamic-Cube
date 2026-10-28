# ADR-0015: Angular + PrimeNG front-end

**Status:** Accepted

## Context
The team works with Angular. We need a rich component set (data tables, trees, dual lists,
forms), RTL support and permissive licensing.

## Decision
One Angular workspace with `internal`, `external`, `field` and `shared` projects; PrimeNG
(MIT) components; ECharts for charts; a shared schema-driven form renderer.

## Consequences
- Shared design system and form renderer across all apps.
- The field app is a PWA (offline via service worker + IndexedDB).

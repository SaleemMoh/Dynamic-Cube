# ADR-0016: DGA design system for all user interfaces

**Status:** Accepted

## Context
Saudi government digital services must follow the Digital Government Authority (DGA) design system:
government identification bar, IBM Plex Sans Arabic, the DGA green palette, accessible focus states,
standard header, footer, forms, tables and dialogs. Dynamic Cube is built on Angular Material
([ADR-0015](0015-angular-material.md)) and Keycloak login pages.

## Decision
- Keep Angular Material and apply the DGA design through its theming API: the DGA palette, shapes and
  component sizes are set as Material tokens (`web/projects/shared/src/styles/dc-theme.scss`); DGA patterns
  that are not Material components (government bar, header, footer, badges, labels above fields, tables,
  focus ring) live in `dc-dga.scss` and the shared layout components.
- Every app uses the shared shell: government bar (Saudi flag, official-website statement, "how to verify"),
  white 72px header, green footer. Footer wording is UI texts (`footer.copyright`, `footer.owner`) so each
  entity sets it in Apps Control.
- Keycloak login pages use the `dynamic-cube` theme (`deploy/keycloak/themes`), which extends Keycloak's own
  `keycloak.v2` theme with DGA styles, bundled fonts and the government bar. No Keycloak templates are
  copied, so upgrades stay simple.
- Fonts and the flag are bundled (no CDN), for on-prem sites without internet.

## Consequences
- Entities rebrand only through tokens and UI texts; the DGA structure stays.
- The entity logo and the DGA registration badge are added per installation (assets are not shipped).

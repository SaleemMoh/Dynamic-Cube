# ADR-0012: Configuration change sets with maker-checker

**Status:** Accepted

## Context
Government customers require governed changes: create → review → approve → publish, with a
full change log (also visible in the portal content management proposal).

## Decision
Simple configuration (Class/Domain, LOVs, settings, menus, roles, grants, UI texts, portal
content) is edited through change sets (`cfg.change_set`, `cfg.change_item` with before/after).
Maker-checker policy is configurable per area. Publishing applies a change set atomically and
audits it. Versioned configuration (ADR-0008) uses the same approval flow on publish.

## Consequences
- Uniform governance and audit across all configuration.
- Admin UX must show pending changes and previews.

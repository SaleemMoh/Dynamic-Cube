# Dynamic Cube — Documentation

Read in order:

1. [Vision & Scope](01-vision-and-scope.md)
2. [Architecture](02-architecture.md)
3. [Domain Model](03-domain-model.md)
4. [Apps Control](04-apps-control.md)
5. [Integration Manager](05-integration-manager.md)
6. [Reference Service: Classification Certificate](06-reference-service-classification.md)
7. [Roadmap](07-roadmap.md)

Supporting material:

- [Architecture Decision Records](adr/README.md)
- [Glossary](glossary.md)

## Conventions

- **Everything business-specific is configuration.** Lookups, statuses, actions, routes, fees,
  templates, connectors, org structure, rules, content. Code provides engines and building
  blocks only.
- **Bilingual by default.** Every user-facing label has Arabic and English values
  (`name_ar` / `name_en`). Arabic is the primary language and layouts are RTL-first.
- **Stable codes.** Configuration is referenced by business `code`, never by database id, so
  configuration can move between environments (dev → test → prod) and survive upgrades.
- **Open items** are marked with **⚑ Open** in each document.

# ADR-0017: Module configuration through one kernel framework

**Status:** Accepted

## Context
Apps Control introduced code-keyed configuration rows, payload appliers, maker-checker change sets, an
approval policy per area and a change-set inbox. The Integration Manager, the Party Registry and the
modules after them (services, workflow, documents) all have configuration of their own. Each module
owns its schema ([ADR-0001](0001-modular-monolith.md), [ADR-0013](0013-apps-control-api-contract.md)),
but entities should review and approve every configuration change in one place, under one policy.

## Decision
- The configuration framework lives in the platform kernel: `ConfigEntity` (code, bilingual name,
  ownership, audit columns), `ConfigPayload`, `ConfigApplier<…>` (validation against pending items of the
  same change set, SYSTEM rows read-only, before/after snapshots) and `ChangeJson`.
- Each module keeps its tables and appliers and registers them with `IConfigApplier`; the change-set
  tables (`cfg`) are shared. `IChangeSetWorkflow` routes a change set to the module that owns its area,
  so the Apps Control inbox, admin API, package import and approval-policy screen work for every module.
- Modules declare their approval areas (`AddChangeArea`) and contribute their resources, menus and the
  privileges of the built-in roles (`AddSystemCatalog`); the Apps Control seeder applies them.
- Privilege checks go through the kernel's `IAccessChecker` (answered by Apps Control), so modules do
  not reference Apps Control.

## Consequences
- A new module gets maker-checker, audit, inbox, approval policy and generic admin screens by writing
  appliers and field definitions only.
- A change set covers one area, hence one module; a configuration package spanning modules becomes one
  draft per area.
- References across modules (e.g. a context source naming an integration operation) are stored by code
  and checked when used, not when published.

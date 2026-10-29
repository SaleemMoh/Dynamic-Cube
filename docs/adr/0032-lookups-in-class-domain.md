# ADR-0032: Lookup-like configuration lives in Class/Domain

**Status:** Accepted. Extends [04-apps-control §4.8](../04-apps-control.md) and [ADR-0031](0031-entities-business-apps-levels.md).

## Context
Class/Domain is the platform's place for lists of values: a class with typed attributes (one of them `JSON`) and its
values. Over time several modules added their own small tables for lists that are nothing more than a code, names, an
order, an active flag and a few fields: service categories, party relationship, credential and sanction types. Other
lists were only written in code: the workflow step types the designer offers, who works on a task, how tasks are
dispatched, task action kinds, join modes, request outcomes, document validity modes, notification channels, visit
modes and outcome kinds, service archetypes and answer types. Administrators could not rename, reorder or hide them,
and every new list meant a new table, applier, migration and screen.

## Decision
- **Modules contribute classes.** `SystemCatalogContribution.Classes` (kernel `CatalogClass`) declares a module's
  classes with their attributes and starting values. The Apps Control seeder keeps the classes and their attributes as
  the product defines them (SYSTEM owner) and adds missing starting values, which then belong to the entity: they are
  renamed, reordered, deactivated or extended in change sets like any other value.
- **Moved into Class/Domain** (rows copied by the migrations, old tables dropped):

  | Was | Class | Attributes |
  |---|---|---|
  | `svc.category` | `SVC_CATEGORY` | `ICON`, `DESCRIPTION_AR`, `DESCRIPTION_EN` |
  | `party.relationship_type` | `PARTY_RELATIONSHIP_TYPE` | `CAN_ACT`, `EXTERNAL_CODES` (JSON `[{provider, code}]`) |
  | `party.credential_type` | `PARTY_CREDENTIAL_TYPE` | `PARTY_KIND`, `ISSUER_AR`, `ISSUER_EN`, `HAS_EXPIRY` |
  | `party.sanction_type` | `PARTY_SANCTION_TYPE` | `BLOCKS_APPLICATIONS`, `DEFAULT_DAYS` |

  Records keep the value's **code** (`relationship.relationship_code`, `credential.credential_type_code`,
  `sanction.sanction_type_code`, `service.category_code`) instead of a key to a module table.
- **Lists that were only in code are classes now:** `WF_STEP_TYPE` (with `ICON`, `HINT_AR`, `HINT_EN`),
  `WF_ASSIGNEE_TYPE`, `WF_DISPATCH_MODE`, `WF_ACTION_KIND`, `WF_JOIN_MODE`, `WF_END_OUTCOME`, `WF_UNIT_SOURCE`,
  `DOC_VALIDITY_MODE`, `COM_CHANNEL`, `VIS_MODE`, `VIS_OUTCOME_KIND`, `SVC_ARCHETYPE`, `SVC_QUESTION_TYPE`. Screens
  take their names from the class (`refDomains` on a field, falling back to the built-in options), and the workflow
  designer takes its palette, names, icons and hints from `WF_STEP_TYPE`; a step type made inactive is no longer
  offered. What each code does stays in the engine: renaming, reordering and hiding work, a new code needs the code
  that runs it.
- **Screens and packages are unchanged.** The change types `svc.category`, `party.relationship-type`,
  `party.credential-type` and `party.sanction-type` remain, served by `DomainBackedApplier` in Apps Control: the
  payload's own fields are the class attributes (`descriptionAr` ↔ `DESCRIPTION_AR`), and the rows are domain values
  of the class. Their change-set area is now `apps.lookups` (Classes & domains); a package import splits its items by
  area as before.
- **Module rules stay with the module.** `IDomainRules` (kernel) lets a module validate the values of its classes and
  say what still uses a value, which then cannot be deleted: a category with services, a relationship type with
  relationships (and a provider code mapped to two types), credential and sanction types in use. Rules also fill
  defaults (`HAS_EXPIRY` and `BLOCKS_APPLICATIONS` are true unless set).
- **Reading values.** Modules read values through `ILookupDirectory` (kernel; Apps Control implements it, cached per
  request): `ValuesAsync(class)` and `FindAsync(class, code)`, with typed attribute helpers.
- **Kept as tables**, because the runtime depends on their structure and integrity (explained in the module ADRs):
  document types and templates (numbering, Liquid templates, issued documents point to them), message templates and
  notification rules (Liquid, rule engine), payment providers (secrets, callbacks, bills point to them), visit types
  (forms, outcomes, visits point to them), the question bank (options and validation, copied into each published
  service version), profile fields and context sources (integration bindings).

## Consequences
- One place to see and maintain the installation's lists; adding a list of values needs no table or migration.
- A service may name a category published in another change set; the category is not checked to exist at publish
  time (the portal shows only categories it finds).
- Deleting a value used by records is refused; deactivating it hides it from new choices while existing records keep
  showing its name.
- Values of runtime-coded lists (step types, channels, modes…) can be renamed, reordered and hidden, not extended
  with behaviour; values of data lists (categories, party types) can be added freely.

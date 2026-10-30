# Architecture Decision Records

Each record captures one significant decision: context, decision, consequences.
Status values: Proposed · Accepted · Superseded.

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith with in-process module calls | Accepted |
| [0002](0002-postgresql-jsonb-generated-views.md) | PostgreSQL with JSONB payloads, promoted columns and generated views | Accepted |
| [0003](0003-custom-workflow-engine.md) | Custom PostgreSQL-backed workflow engine | Accepted |
| [0004](0004-separate-api-hosts-per-zone.md) | Separate API hosts for internal and external zones | Accepted |
| [0005](0005-keycloak-authn-app-authz.md) | Keycloak for authentication, Apps Control for authorization | Accepted |
| [0006](0006-permissive-licenses-only.md) | Permissive open-source licenses only | Accepted |
| [0007](0007-lov-generated-queries-and-data-scopes.md) | LOV as generated queries with grants applied as data scopes | Accepted |
| [0008](0008-versioned-configuration-and-pinning.md) | Versioned configuration; instances pinned to their version | Accepted |
| [0009](0009-outbox-no-message-broker.md) | Transactional outbox, no message broker | Accepted |
| [0010](0010-jsonlogic-expressions.md) | JSONLogic as the shared expression format | Accepted |
| [0011](0011-html-to-pdf-gotenberg.md) | HTML → PDF rendering with Gotenberg | Accepted |
| [0012](0012-maker-checker-change-sets.md) | Configuration change sets with maker-checker | Accepted |
| [0013](0013-apps-control-api-contract.md) | Apps Control owns its schema and a versioned API | Accepted |
| [0014](0014-pluggable-decision-engine.md) | Decision engine behind a platform contract | Accepted |
| [0015](0015-angular-material.md) | Angular + Angular Material front-end | Accepted (revised) |
| [0016](0016-dga-design-system.md) | DGA design system for all user interfaces | Accepted |
| [0017](0017-module-configuration-in-the-kernel.md) | Module configuration through one kernel framework | Accepted |
| [0018](0018-internal-system-look.md) | A gold working look for the internal system | Accepted |
| [0019](0019-workflow-runtime.md) | How the workflow engine runs: service-owned workflows, tokens, instance locks, returns, deadlines | Accepted (revised 1f) |
| [0020](0020-documents-decisions-communication.md) | Issued documents, decision rules and notifications | Accepted |
| [0021](0021-follow-up-services.md) | Follow-up services act on issued documents: availability windows, requests bound to a document, amendments by the workflow | Accepted |
| [0022](0022-payments.md) | Payments as a module: bills through the Integration Manager, signed provider callbacks, a workflow step that waits for payment | Accepted |
| [0023](0023-workflow-designer.md) | The Workflow Designer edits the same definition the engine runs: canvas, checks as you draw, visual rules, test run, next version through change sets | Accepted |
| [0024](0024-service-builder.md) | The Service Builder edits the version definition; form layouts (tabs, accordion, columns, widths) join the runtime first | Accepted |
| [0025](0025-portal-content.md) | Portal content as a module of pages and questions (Markdown, anonymous reads); the home page stays a setting with a structured editor; the notification rule builder edits rules as stored | Accepted |
| [0026](0026-look-accounts-json-import.md) | Branding drives colours and sign-in pages; accounts are maintained through the identity provider (managed or federated); JSON import on every screen | Accepted |
| [0027](0027-configuration-packages.md) | Configuration packages: export by code from any screen, UPSERT import, unchanged rows skipped, field-by-field review | Accepted |
| [0028](0028-portal-dga-layout.md) | The portal follows the DGA layout of entity portals (title bands, tabs, cards, filter panels, service page anatomy); page feedback and service ratings are stored | Accepted |
| [0029](0029-field-visits.md) | Field visits as a module: a workflow step that waits for the outcome, an offline-first field app with an outbox, external inspection bodies through the Integration Manager | Accepted |
| [0030](0030-org-structure-routing.md) | Tasks routed through the organization structure: a unit fixed, chosen by a rule or taken from a previous actor; levels up, a position in the unit, sub-units, climbing when empty | Accepted |
| [0031](0031-entities-business-apps-levels.md) | Several entities and business apps; every configuration row is system, entity or app level; staff work within their entity and apps; app filters on lists | Accepted |
| [0032](0032-lookups-in-class-domain.md) | Lookup-like configuration in Class/Domain: modules contribute classes; categories and party types moved there; workflow, document, notification, visit and service option lists became classes; runtime-heavy configuration stays in tables | Accepted |
| [0033](0033-forms-not-json.md) | Structured forms instead of JSON entry (with a JSON view), rich text email bodies, employees of an org unit | Accepted |
| [0034](0034-file-questions.md) | File-upload questions: files uploaded to the draft and checked against the question, answers hold file ids, a file required for a given answer through required-when | Accepted |
| [0035](0035-users-and-establishment-links.md) | Users and accounts: the identity provider owns credentials (managed, federated or none), Apps Control owns access; applicants are linked to establishments by the registry integration or by staff, also before the first sign-in | Accepted |
| [0036](0036-organizations-companies-branches-apps.md) | Organizations, companies, branches and apps (Dynamic Cube apps and connected apps); one Access tab per user with roles, branch limits, personal privileges and department default roles | Accepted |

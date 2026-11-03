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
| [0037](0037-archetypes-question-fill-integration-steps.md) | Archetypes declared with a default service template and workflow; questions filled from the party or an integration, optionally locked; service-level questions; integration steps in the service form and the workflow | Accepted |
| [0038](0038-integration-results-draft-refresh-autosave-workflow-position.md) | Integration step result card (status, message, chosen values, try again); drafts read their integrations again and show what changed; autosave; the request's place drawn on its workflow | Accepted |
| [0039](0039-lov-visual-query-builder.md) | Lists of values built in a visual query builder (source, join, map, filter, sort, access) over registered tables and classes; conditions on the user's data; role grants edited in the builder, saved on the roles | Accepted |
| [0040](0040-applicant-side-panel.md) | The applicant's side panel, configured per service version: an assistant (step greetings, notes with their rule, questions and answers, typed questions matched by words), the readiness check, the request summary and the live result (the workflow's decision step on the answers on screen, not recorded, on a scale with a card per item) | Accepted |
| [0041](0041-operation-designer-openapi-import.md) | Integration operations designed visually (path placeholders as inputs, body preview, try the unsaved operation, pick outputs from the response tree) and imported from OpenAPI 3.x / Swagger 2.0 descriptions as a reviewed configuration package | Accepted |
| [0042](0042-public-api-endpoint-builder.md) | Public API for partner systems: read-only endpoints configured as change sets (read data over registered tables and classes, or read-only system functions such as a request's status in its workflow), API clients with client-credentials sign-in, their companies and branches, rate limits, a call log and an OpenAPI description each | Accepted |
| [0043](0043-nafath.md) | Nafath: applicants sign in to the portal through Keycloak with a sign-in bridge to Nafath's web sign-in, and confirm a request in their Nafath app before submitting it (per service version); signed answers checked with Nafath's keys, credentials in the secret store, a mock Nafath for development | Accepted |
| [0044](0044-soap-operations-wsdl-import.md) | SOAP operations in the operation designer: envelope built from the inputs (1.1/1.2), the XML response read as JSON for outputs, envelopes and code maps, faults as outcomes, WS-Security UsernameToken (text or digest) and basic authentication, WSDL 1.1 import, a mock Yakeen-like SOAP service | Accepted |
| [0045](0045-report-builder.md) | Report builder: reports over registered tables and classes built in the visual query builder (grouped columns with totals, a totals row, tiles, bar/line/pie chart, filters and saved filters), each a screen roles grant, Excel/CSV export, scheduled email delivery with each recipient's own access; data permissions and the viewer's company and branches always apply | Accepted |
| [0046](0046-sub-requests.md) | Sub-requests: prerequisites per service version (a request or a document, with a start link), bundled services submitted with a request (required or ticked), and a workflow `SUB_REQUEST` step that opens a request and waits for it or carries on; linked requests shown on both request pages | Accepted |
| [0047](0047-dashboards-and-drill-down.md) | Dashboards and drill-down: shared dashboards (change sets, a screen each) and personal ones (pinned from reports), tiles, charts and short tables of reports with dashboard filters that fill the reports' filters; a grouped row, chart mark, tile or totals row opens the rows behind it, within the viewer's access | Accepted |
| [0048](0048-reusable-service-building-blocks.md) | Reusable service building blocks: repeating rows with files per row, a firm's record lists (typed or imported), packages with features and a capped formula fee, staff inputs on tasks with decisions worked out again, one request with several items each decided on its own, acting for a firm on an authority checked with the provider, automation rules on tasks, a calculator page, integration evidence on the request page | Accepted |
| [0049](0049-logic-builder.md) | Logic Builder: every JSONLogic rule written through a form — fields from a grouped menu, comparisons by type, values chosen from options, classes or lists of values, nested groups, conditions on rows, calculations (arithmetic, if/then, sum or count of rows) — said in words, tried with sample values, the JSON behind *Advanced*; used everywhere a rule is written | Accepted |

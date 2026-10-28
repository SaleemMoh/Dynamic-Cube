# 7. Roadmap

Built as **vertical slices**: each phase delivers working end-to-end behaviour, driven by the
Classification Certificate reference service. No phase builds a designer before the runtime it
configures exists.

## Phase 0 — Foundations ✅

Delivered:

- Repository and solution layout; central package management; analyzers as errors; `dotnet format` and ESLint
  checks; CI (build, format, tests incl. PostgreSQL integration tests, migration drift check, container image builds).
- Platform kernel: module DbContext base, snake_case PostgreSQL conventions, audit log interceptor, transactional
  outbox (leased, retries with backoff, dead-lettering), configuration change sets with maker-checker, file storage
  abstraction, bilingual text, JSONLogic evaluation — identical in .NET and Angular, verified by shared cases.
- `Internal.Api` / `External.Api` (health, OpenAPI, Keycloak JWT per realm), `Worker` (outbox), `Migrator`.
- Keycloak realms (internal / external) with development users; external tokens carry `national_id`
  (stand-in for Nafath until the identity provider is connected).
- Angular 22 workspace (`internal`, `external`, `field`, `shared`) on Angular Material: Arabic-first RTL shell,
  language switching, Material 3 theme, bundled fonts/icons, schema-driven `DynamicForm` with JSONLogic rules.
- Container images for every process, Nginx SPA image with API reverse proxy, Docker Compose for local / small sites.

Carried into Phase 1:

- Front-end login (OIDC + PKCE) against the realms; data-protection key persistence.
- Helm chart and N-tier install scripts (`deploy/helm`, `deploy/ntier`).

## Phase 1 — Core runtime + Apps Control (first slice: new classification request)

- **Apps Control**: installation, apps, entity, org unit types & org structure, positions,
  users, roles, privileges, menus, Class/Domain with typed attributes and generated views,
  LOVs with grants and data-scope bindings, settings, UI texts, maker-checker.
- **Party Registry**: individuals, establishments, delegation, profile data, external
  credentials, projects.
- **Integration Manager**: connections, OAuth2 client-credentials, envelope profiles,
  operations, mappings, response-code messages, call logs; MOMRAH gateway connectors needed by
  the slice.
- **Services**: service definition & versions, composable forms with question bank, wizard,
  drafts, service levels, fees (bracketed), eligibility.
- **Workflow engine**: human tasks, **parallel split/join**, distribution by manager,
  maker-checker, return for correction, SLA timers on the working calendar.
- **Action Center**: inboxes, distribution, task view, actions, timeline.
- **Decisions**: decision-step contract; plug in the supplied engine.
- **Documents**: multi-line certificate, versions, QR verification, HTML→PDF.
- **Communication**: email/SMS, templates, event notifications.
- **External system**: login, acting context, service identifier, apply, track, respond.
- Configuration for this phase may be seeded / edited through admin screens; full visual
  builders come later.

## Phase 2 — Document-driven services and payments

- Amendment services: renewal, extension, add/remove sector, upgrade, update data, appeal.
- **Availability rules** (status + date windows) and certificate page with available / locked
  services.
- Payments: SADAD / Tahseel bills and callbacks; service-level fees end to end.
- Scheduled document-date alerts; draft expiry.
- Sanctions and document status actions.
- Classification calculator (public, stateless).
- Dashboards v1: requests, SLA, certificates by region/sector/grade, draft abandonment.

## Phase 3 — Builders and content

- Visual **Form & Wizard Builder**, **Workflow Designer** (canvas), notification rule builder.
- **Portal Content Manager**.
- Visits: internal field app (offline PWA), external inspection integration, manual entry.
- Sub-requests (prerequisite / bundled / spawned).
- Configuration export/import packages with diff preview.

## Phase 4 — Extended capabilities

- Report builder; pixel-perfect reports.
- API Endpoint Builder; public Apps Control API for other products.
- Push notifications, chat; AI assistant integration point.
- Advanced scripting / plugin SDK.

## Cross-phase quality gates

- Golden test cases for the decision engine per sector (reproducible results) before
  publishing a rule set.
- Integration contract tests against staging.
- Accessibility (WCAG 2.1 AA), RTL review, performance and security testing per release.

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

- ~~Front-end login (OIDC + PKCE) against the realms; data-protection key persistence.~~ Done in 1a.
- Helm chart and N-tier install scripts (`deploy/helm`, `deploy/ntier`).

## Phase 1 — Core runtime + Apps Control (first slice: new classification request)

Delivered in slices:

| Slice | Content | Status |
|---|---|---|
| **1a** | Apps Control + sign-in | ✅ |
| **1b** | Party Registry + Integration Manager | ✅ |
| **1c** | Services: definitions, forms, wizard, drafts, fees, eligibility | ✅ |
| **1d** | Workflow engine + Action Center | ✅ |
| **1e** | Documents, communication, decision contract, external applicant journey | ✅ |
| **1f** | Service-owned workflows: every service has its own workflow; none is published without one | ✅ |
| **1g** | E-services portal look: service catalog home and service card page | ✅ |

**1a delivered:** the `AppsControl` module (all entities of [§4](04-apps-control.md) except
delegations, which come with the Action Center), change appliers with code-keyed payloads,
maker-checker publishing, typed class attributes with generated views, the LOV engine (class, data
object and restricted-SQL sources) with grants and data scopes, account provisioning and bootstrap
roles, the `/api/apps-control/v1` API (public subset on the external host), SYSTEM seed data in the
migrator; OIDC + PKCE sign-in in all three SPAs with runtime `config.json`; the internal system's
Apps Control screens (menu from Apps Control, change-set inbox with before/after review, classes and
values with generated forms, generic editors for the other types); LOV-backed fields in
`DynamicForm`; data-protection keys in PostgreSQL.

**1b delivered:** the configuration framework moved to the kernel so every module's configuration
goes through the same change sets, inbox and approval policy ([ADR-0017](adr/0017-module-configuration-in-the-kernel.md));
modules contribute their resources, menus and built-in role privileges to Apps Control. The
`Integration` module ([§5.6](05-integration-manager.md#56-implementation-1b)): providers, connections
(OAuth2 client credentials, basic, API key; credentials only from the secret store), response
envelopes, operations with parameters, outputs, response-code maps and masking; the gateway with
token cache, timeouts, retries for idempotent calls, circuit breaker and result cache; the masked call
log; test console. The `Parties` module ([§3.1](03-domain-model.md#31-party-registry)): individuals
registered on first portal sign-in, establishments, relationships with service scope and validity,
acting contexts from configurable context sources (MCI by ID number, relation codes translated),
`X-Acting-Party` validation, profile fields refreshed from integrations, credentials, projects and
sanctions. Configuration packages (`deploy/config-packages`) imported as draft change sets; the MCI
package; a development mock of the MoMRAH gateway. Screens: integration and party configuration,
test console, call log, party list and detail, the portal's acting-context selector.
Still open from 1b: LOVs sourced from integrations, SOAP operations and OpenAPI import, NIC and the
other connectors of [§5.5](05-integration-manager.md#55-initial-connectors-for-the-reference-service)
(configuration only, added as packages when their credentials are available), call-log retention job.

**1c delivered:** the `Services` module ([§3.2](03-domain-model.md#32-service-definition)): services,
the question bank and immutable numbered versions with levels, wizard steps, composable sections,
eligibility and validation rules and fees; requests pinned to their version with submitter and
beneficiary, drafts prefilled from the party's data and saved at every step, draft expiry and the
new-version policy, eligibility (who may apply, delegation scope, sanctions, rules), fee calculation
with VAT, server-side validation mirroring the wizard, submission with yearly numbers and a timeline.
Screens: the portal's catalog, service page, wizard and *My requests* (acting for the chosen party);
internal services, question bank, versions with JSON authoring and wizard preview, and the request
viewer. Reference package `classification-new.json` (the classification certificate's levels, fees,
sectors and pledge). The dynamic form gained rule context across steps, limits and shared questions.
Still open from 1c: attachments, payment (SADAD), availability rules and document-based services,
sub-requests, service links and the visual service builder (JSON authoring with preview for now).

**1d delivered:** the `Workflow` module ([§3.5](03-domain-model.md#35-workflow),
[ADR-0019](adr/0019-workflow-runtime.md)): workflows and immutable versions as JSON graphs with start, human
task, parallel split, join (all / any / count), gateway (JSONLogic) and end nodes; assignment to a role (within an
org unit or position), a user or the previous actor, by queue, distribution or least-loaded; maker-checker between
steps; complete, approve, reject and return actions with comments the applicant sees; deadlines per service level
and step on the working calendar (installation weekend and holidays), warnings, breaches and escalation by a
worker job; delegations. The kernel gained a cross-module unit of work, the staff directory and the working
calendar. Services: a version names its workflow, submission starts it, the request's status follows it, and a
request returned for correction is edited and resubmitted. The Action Center ([§3.4](03-domain-model.md#34-action-center)):
inboxes (mine, queue, to distribute, escalated) with counts and search, the task page (request answers, claim,
release, assign, actions, internal notes, steps and timeline), supervision and out-of-office delegation; workflow
configuration with a live graph; the portal's returned-request banner and history. Package
`classification-workflow.json`: the classification roles and staff, the workflow of
[§6.7](06-reference-service-classification.md#67-processing-workflow) and version 2 of the service using it.
Configuration packages are reviewed before import (every problem listed, nothing kept) and a documented template
can be downloaded.
Still open from 1d: integration, decision, payment, visit, document, notification and timer nodes; forward,
hold and third-party inquiry actions; attachments on actions; editable fields per step; relative routing targets
(applicant's region, parent unit's manager); data scopes on inboxes; the visual Workflow Builder.

**1e delivered** ([ADR-0020](adr/0020-documents-decisions-communication.md)): the `Documents` module
([§3.8](03-domain-model.md#38-issued-document-registry)): document types and HTML/Liquid print templates; issuing
with numbers, verification codes, immutable versions and amendments; PDF through Gotenberg; suspend, reinstate and
cancel with a reason; expiry and reminder days by a worker job; anonymous verification. The `Decisions` module
([§3.7](03-domain-model.md#37-decisions-calculation--classification)): the kernel decision contract, versioned rule
sets, the built-in `RULES` engine (JSONLogic variables, per-item results, trace), the evaluation log, a try console
and public calculators. Workflow DECISION and DOCUMENT steps with instance facts. The `Communication` module
([§3.9](03-domain-model.md#39-communication)): events raised by requests, documents and tasks; Liquid message
templates per channel; notification rules with conditions and recipients; in-app, email (SMTP) and SMS (an
integration operation) through the outbox; the message log; notification bells in both apps. Screens: the document
registry, print template editor with live preview, rule-set versions with try console, message template editor
previewed on an event's sample, message log; in the portal *My documents*, documents on the request page and the
public verification page. Package `classification-certificate.json`: the certificate, the `CLS_GRADING` rules
(§6.5, simplified), `CLASSIFICATION/2` (grade after the analyses, certificate after approval) and the §6.10
messages. Development compose runs Gotenberg and Mailpit.
Still open from 1e: the financial axis and adjusters of the grading, the public calculator page, availability of
follow-up services by document dates (Phase 2), push notifications, schedule-based rules (draft expiry), email
attachments.

**1f delivered — service-owned workflows** ([ADR-0019](adr/0019-workflow-runtime.md), revised). The workflow is
part of the service:

- **One service, its own workflow.** `wf.workflow` carries a `serviceCode`; a service has exactly one workflow and
  a workflow cannot move to another service or be deleted while its service exists. Workflows are not shared; to
  reuse one, copy its definition into a new version of the other service's workflow. The services list opens a
  service's workflow versions directly, and the workflows list shows the service of each (workflows are not created
  there).
- **A default workflow for every service.** Publishing a new service creates its workflow in the same transaction
  (`IChangeSetPublishHook`; change-set publishing now runs in a cross-module unit of work) with version 1,
  *بدون مراجعة / No review*: *Start → Completed (approved)*. A submitted request completes at once; documents and
  notifications still follow. Review steps come as a new workflow version, and requests in progress keep theirs.
- **No publishing without a workflow.** A service version is refused when the change set is validated if its
  service has no published workflow version (unless the service itself is created in the same change set, which
  provisions the default). Versions no longer name a workflow (`definition.workflow` is rejected; the field stays
  readable on old versions), and a request always starts its service's workflow.
- **Existing configuration.** The migrator gives every service without a workflow the default one, linking the
  workflow a published version named where there was one (on an existing installation `CLASSIFICATION` becomes the
  workflow of `CLASSIFICATION_NEW`). On a new installation the service's workflow is `CLASSIFICATION_NEW`;
  `classification-workflow.json` adds its version 2 (no service version any more) and
  `classification-certificate.json` its version 3.
Still open from 1f: a *Workflow* tab inside the service screen and copying a workflow between services (both come
with the Service Builder and Workflow Designer of Phase 3).

**1g delivered — the e-services portal look**, after the reference design (registry portal home and service card
page, DGA components). Browsing needs no sign-in; starting a service does.

- **Home:** the header has *Home*, *Services* (a menu of the categories), *My requests* and *My documents* when
  signed in, search, language and the user with the acting context. A hero banner shows the portal's title and
  purpose over the configured image (or a green pattern), with a search box, a category filter and voice input
  where the browser offers it. Category tabs (*All* and one per category) filter the services, shown as cards in a
  carousel (icon, name, short description, audience and category tags, an arrow to the card) with *View all*. The
  **statistics tiles** count listed services, submitted, in-progress and completed requests, or show fixed values;
  then the last-updated date and the DGA footer. Floating help buttons offer contact (phone, email, contact page)
  and accessibility tools (larger text, high contrast, remembered on the device). Signed-in applicants also see
  who they apply for. *All services* searches and filters by category, and tells a signed-in applicant which
  services the acting party cannot use and why.
- **Service card page:** breadcrumb (home › category › service), the name and description, **Start service**
  (sign-in first, then the eligibility check of the acting party), tabs for **Steps** (numbered), **Terms of use**,
  **Required documents** and **Service levels** (duration and cost, or the applicant's own fee table when signed
  in), and a side card with the **target audience** (or the party kinds), **service duration** (working days from the
  levels), **channels**, **cost** (from the fees: *Free*, an amount, a range or *depends on the request*, VAT
  included) and the **FAQ link, phone and email**.
- **Configuration.** `svc.category` (Services configuration → *Service categories*: names, description, icon,
  order) groups services; a service's category must exist, and categories in use cannot be deleted (existing
  category codes became categories). The card fields are `definition.card` of a service version (audience,
  steps, terms, required documents, channels, FAQ link, phone, email; bilingual, versioned and checked on publish).
  The public setting `PORTAL_HOME` in Apps Control holds the hero title and text, banner image, statistics tiles and
  help links. Anonymous endpoints: `GET /api/services/v1/portal/catalog`, `/portal/services/{code}` and
  `/portal/statistics`. Package `portal-services.json`: the setting, three categories and four sample services.
Still open from 1g: a structured card editor (until the Service Builder of Phase 3 the card is part of the version's
JSON), uploading the banner image, statistics from other modules (e.g. certificates issued), FAQ pages.

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
  maker-checker, return for correction, SLA timers on the working calendar. Each service owns its
  workflow, starting from a default one that needs no approval (1f).
- **Action Center**: inboxes, distribution, task view, actions, timeline.
- **Decisions**: decision-step contract; plug in the supplied engine.
- **Documents**: multi-line certificate, versions, QR verification, HTML→PDF.
- **Communication**: email/SMS, templates, event notifications.
- **External system**: login, acting context, service identifier, apply, track, respond; the portal home and
  service card page of the reference design (1g).
- Configuration for this phase may be seeded / edited through admin screens; full visual
  builders come later.

## Phase 2 — Document-driven services and payments

- Amendment services: renewal, extension, add/remove sector, upgrade, update data, appeal.
- **Availability rules** (status + date windows) and certificate page with available / locked
  services.
- Payments: SADAD / Tahseel bills and callbacks; service-level fees end to end.
- ~~Scheduled document-date alerts~~ (done in 1e); draft expiry.
- Sanctions and document status actions.
- Classification calculator (public, stateless).
- Dashboards v1: requests, SLA, certificates by region/sector/grade, draft abandonment.

## Phase 3 — Builders and content

Visual designers over the runtime delivered in Phase 1; what they produce is the same versioned configuration,
saved as change sets with maker-checker, so everything stays reviewable and can be exported as packages.

- **Workflow Designer** (canvas): drag nodes (human task, parallel split / join, gateway, decision, document,
  notification, integration, timer, end) from a palette, connect them, edit each node in a side panel
  (assignment, actions, deadlines per service level, display status, conditions in a JSONLogic rule editor),
  validation as you draw (unreachable nodes, missing ends, unclosed splits), test run with sample data, and
  publish as a new version of the service's workflow. Replaces hand-written JSON.
- **Service & Form Builder**: one designer per service, in tabs:
  - **Form** — steps and sections built from the question bank or new questions by drag and drop; field
    properties (type, required, limits, LOV, prefill from the party, show/require when, cross-field checks);
    **layout selection** per form and per section: wizard (one step per page), single page, tabbed sections or
    accordion, with 1, 2 or 3 columns and field width (full / half / third); live preview in Arabic and English,
    desktop and mobile.
  - **Service Card** — what the portal's service page shows: name, description, category and tags, icon, target
    audience, duration, channels, cost (from the fees), steps, terms of use, FAQ link, phone and email, with a
    preview of the card and the page.
  - **Service rules** — eligibility (who may apply, active CR, sanctions), availability windows (e.g. renewal
    within 90 days of expiry), levels and fees (bracketed, VAT), draft policy.
  - **Required documents** — the attachments asked for (type, when required, formats and size, one or several),
    shown on the card and requested in the form.
  - **Output** — what the service produces: the document type issued (data and line mapping), the decision
    rules it uses, the notifications it sends.
  - **Related services** — prerequisite, follow-up and alternative services shown on the card and offered
    after a result (e.g. renewal, extension, appeal).
  - **Workflow** — the service's own workflow, opened in the Workflow Designer.
- Notification rule builder (event, condition, recipients, channels) instead of JSON.
- **Portal Content Manager**: home banner, categories, statistics tiles, FAQs, help and contact links, pages.
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

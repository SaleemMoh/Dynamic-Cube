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
  provisions the default). Versions no longer name a workflow, and a request always starts its service's workflow.
  The field stays readable on old versions. A version that still names a workflow is accepted, and publishing adds
  that workflow's latest published version to the service's own workflow (see ADR-0019).
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

Delivered in slices:

| Slice | Content | Status |
|---|---|---|
| **2a** | Follow-up services on issued documents: availability windows, requests bound to a document, renewal and extension | ✅ |
| **2b** | Payments: bills (SADAD / Tahseel through the Integration Manager), payment callbacks, waiting for payment, receipts | ✅ |
| 2c | Sanctions: progressive penalties by offence, bans by kind of service, document status actions from sanctions, committee review | Postponed |
| 2d | Remaining classification follow-ups: add / remove sector, modify / upgrade grade, update certificate data, appeal; draft expiry job | Postponed |
| 2e | Classification calculator (public, stateless) and dashboards v1 | Postponed |

2c, 2d and 2e are postponed until after the Phase 3 builders; nothing in Phase 3 depends on them.

**2a delivered** ([ADR-0021](adr/0021-follow-up-services.md)):

- **Services that act on a document.** A service may name a document type (`documentType`); each of its requests is
  made for one of the beneficiary's documents of that type (`sourceDocument`). The version's **availability** says
  which document statuses it accepts (ACTIVE by default; EXPIRED, SUSPENDED) and a window of days around the issue
  or expiry date (e.g. renewal from 90 days before expiry until expiry). One request per document and service can be
  open at a time; the window and status are checked again on submission.
- **The document in rules.** Eligibility, validations, fees and prefill see `document` (number, status, issue and
  expiry dates, data, lines); so does the workflow.
- **Amending the document.** A workflow document step with `target: SOURCE` amends the request's document (same
  number, a new version); data fields given replace theirs, the rest and the lines stay unless given. Its
  `validity` moves the expiry: KEEP, RESET (a new period from today), RENEW (a new period from the current expiry, so
  renewing early loses no days) or EXTEND (N months); an expired document renewed or extended is active again.
- **Screens.** The portal's document page lists the follow-up services of the document: available with *Start*,
  locked with the dates of their window and the reason, or the open request to continue. A follow-up service's page
  lists the applicant's documents to choose from; its card says which document it needs. Requests show the
  document they are for, in both apps. The service form has *Document the service acts on*.
- **Packages.** `classification-renewal.json` (renewal: ACTIVE certificates, the 90 days before expiry; extension:
  ACTIVE or EXPIRED, from 15 days before until 90 days after expiry; both prefill the certificate number and expiry and
  ask for a declaration) and `classification-renewal-workflow.json` (version 2 of their workflows: renew the
  certificate for 24 months from its expiry; extend it by 3 months).

Still open from 2a: fees on follow-ups (a payment step in their workflows, possible since 2b), sanctions banning
renewals (2c), choosing the document when an applicant holds several from the service's wizard (today from the
service page or the document page), and a review step before renewals (a new workflow version).

**2b delivered** ([ADR-0022](adr/0022-payments.md)):

- **Payments module.** Bills with the platform's number and the provider's (SADAD) number, lines with VAT, a due
  date and a history; providers as configuration (`pay.provider`): the Integration Manager operation that registers
  the bill, the biller code, days to pay, payment page, instructions, the signing secret's name and where
  notifications carry their values. A provider without an operation is manual (cashier).
- **Signed payment notifications** on the external host (`POST /api/payments/v1/callbacks/{provider}`, HMAC-SHA256
  over the body in `X-Signature`): the amount must match; repeats change nothing; late payments are recorded.
  Unpaid bills expire by a worker job.
- **Workflow PAYMENT step.** Bills the request's fees (or a JSONLogic amount) and waits: the request is *awaiting
  payment* and its deadline stands still; paid, it continues; expired or cancelled, it takes the step's `expired`
  way (e.g. *closed — not paid*). Nothing to pay: it passes on.
- **Screens.** The request page shows the bill (amount, SADAD number, biller code, due date, how to pay, *Pay now*)
  and, once paid, the receipt; staff get *Payments → Bills* (search, bill page with history, record a payment,
  cancel) and *Payment providers*; staff request pages show the bills too.
- **Packages.** `payments-sadad.json` (the SADAD bill operation on the MOMRAH gateway, the default provider `SADAD`
  and three notifications) and `classification-payment.json` (`CLASSIFICATION_NEW/4`: Premium and Diamond fees paid
  before the request manager's review; an unpaid bill closes the request). The mock gateway has SADAD's API and a
  payment page that sends the signed notification.

Still open from 2b: refunds and partial payments, reconciliation reports, a fee step on follow-up services.

- Amendment services: renewal, extension (2a); add/remove sector, upgrade, update data, appeal (2d).
- ~~**Availability rules** (status + date windows) and certificate page with available / locked services~~ (2a).
- ~~Payments: SADAD / Tahseel bills and callbacks; service-level fees end to end~~ (2b).
- ~~Scheduled document-date alerts~~ (done in 1e); draft expiry (2d).
- Sanctions and document status actions (2c).
- Classification calculator (public, stateless) (2e).
- Dashboards v1: requests, SLA, certificates by region/sector/grade, draft abandonment (2e).

## Phase 3 — Builders and content

Visual designers over the runtime delivered in Phase 1; what they produce is the same versioned configuration,
saved as change sets with maker-checker, so everything stays reviewable and can be exported as packages.

Delivered in slices. Each designer covers what the runtime can run today; node types or rules without a runtime
(notification, integration and timer steps) are added to the designer together with their runtime.

| Slice | Content | Status |
|---|---|---|
| **3a** | Workflow Designer: canvas, palette, connections, side panel per step, visual JSONLogic rule editor, checks as you draw, test run, publish as a new version | ✅ |
| **3b** | Service & Form Builder: form (steps, sections, question bank, layouts, live preview), service card, rules, required documents, output, related services | ✅ |
| **3c** | Notification rule builder; Portal Content Manager (banner, categories, tiles, FAQs, help links, pages) | ✅ |
| **3d** | Configuration packages: export from any screen, import with a diff preview | ✅ |
| **3e** | Visits: field app (offline PWA), external inspection integration, manual entry | ✅ |
| 3f | Sub-requests: prerequisite, bundled, spawned | Next |

**3a delivered** ([ADR-0023](adr/0023-workflow-designer.md)):

- **Canvas.** *Action Center → Workflow designer* (or *Open in the designer* on any version) opens a workflow's latest
  version, or an earlier one, and prepares the next. Steps come from a palette (drag or click); dragging from the
  handles under a step connects it: next, a task's actions, a split's branches, a gateway's conditions and its
  otherwise, a payment's paid and not-paid ways. Steps move freely (snapped to a grid) or are arranged
  automatically; undo and redo, delete, zoom.
- **Side panel.** Per step: code (renaming updates every reference), names, who works on a task (role, employee or
  whoever completed an earlier step; queue, distribution or automatic), separation of duties, displayed status,
  deadlines per service level and escalation, actions with their targets, returns and where they resume, branches,
  join mode, gateway routes in order, the rule set and inputs of a decision, the document type, target, validity,
  data and lines of a document step, the provider, amount, VAT and days of a payment, an end's outcome.
- **Rules without JSON.** A shared visual JSONLogic editor (all / any of comparisons; a field, text or number as a
  value) with the service's questions, levels, tasks' actions, decision results and bill statuses suggested;
  anything else is edited as JSON, checked as it is typed.
- **Checks.** The server's structural checks run as you draw, listed with the step they concern and counted on the
  canvas; *Check on the server* runs the full publish validation without saving.
- **Test run.** Sample answers, a service level and fees; gateways are evaluated as on the server, splits and joins
  behave as in the engine, and you choose each task's action and whether the bill is paid, with the current steps
  highlighted.
- **Saving.** The version goes into a change set as `WORKFLOW/N+1` with where its steps were drawn
  (`definition.layout`); the JSON view shows exactly what is saved and can be edited.

Still open from 3a: comments on steps, copy and paste, a minimap for large workflows, and rule-set results in the
test run.

**3b delivered** ([ADR-0024](adr/0024-service-builder.md)):

- **Form layouts in the runtime.** A form step shows its sections one below the other, as tabs or as an accordion;
  a section has 1–3 columns and a question a width (full, half or a third of the row). Forms narrower than 600px show
  one column; on submit the tab or panel with the first problem opens. The portal wizard, previews and staff screens
  all use it.
- **Service Builder** (*Services → Service builder*, or *Open in the service builder* on a service or a version): opens
  a service's latest version, or an earlier one, and prepares the next.
  - *Form* — steps (form, level, fees, review, declaration), sections and bank questions placed by drag and drop
    (or a click into the selected section), reordered and moved by dragging; the inspector edits codes, titles,
    layout, required / read-only, and show / require conditions with the visual rule editor. New bank questions and
    edits go through the bank's editor.
  - *Rules & fees* — levels with durations, eligibility, cross-field checks, fees (fixed, formula, bracket table, VAT,
    levels, condition) and, for document services, the availability window.
  - *Service card* — audience, channels, contacts, steps, required documents and terms, with a preview of the portal
    page.
  - *Workflow* — what the service's workflow does (review tasks, documents issued, rules, payments, outcomes) with the
    way to the Workflow Designer.
  - *Preview* — the applicant's wizard as the version is now, on a desktop or a phone; *JSON*; checks as you edit,
    the server's check, undo / redo, saving as the next version.
- Fix: published service versions stored before the card, availability or layout fields existed can be retired.

Still open from 3b: uploading required documents (attachments), related and prerequisite services, a single-page
mode of the wizard, and choosing the preview's language independently of the app.

**3c delivered** ([ADR-0025](adr/0025-portal-content.md)):

- **Notification rule builder.** In *Notifications → Notification rules*, a rule is built in three parts:
  - *When*: the event, grouped by module, and an optional condition in the visual rule editor over the event's
    facts.
  - *Who*: the applicant, named roles or employees, or the role or employee the event names.
  - *What*: the template, only the channels it has wording for, and the language.

  The message is previewed on the event's sample facts for each chosen channel.
- **Content module** (schema `cms`): portal pages (Markdown in Arabic and English, linked from the footer) and
  frequently asked questions (topic, optional service, order). Both are configured through change sets and read
  anonymously by the portal.
- **Content Manager** (*Portal content*):
  - *Portal home page*: hero wording and banner, statistics tiles (counted or fixed, icons, order) and help links,
    with a preview. It is saved as the `PORTAL_HOME` setting.
  - *FAQs* and *Pages*: written with a side-by-side preview that uses the portal's own renderer.
- **Portal:**
  - `/faq`: questions grouped by topic, with search.
  - `/faq?service=CODE`: one service's questions, linked from its service page.
  - `/pages/:slug`: a page.
  - The footer links pages and questions.
- Package `portal-content.json`: about, terms of use and privacy pages, and ten questions.

Still open from 3c: images and attachments in pages (they need a file runtime), categories edited from the Content
Manager (they stay in Services), scheduled publishing, and server-side search across questions.

**Improvements after 3c** ([ADR-0026](adr/0026-look-accounts-json-import.md)):

- **Branding:** colours for the internal system and the portal, a footer image and a sign-in image, applied by every
  app and by the sign-in pages.
- **Internal sign-in page:** follows the internal design guide.
- **Accounts:** employees' sign-in accounts are created, activated and deactivated, and given passwords from Apps
  Control. When accounts come from Active Directory, LDAP, Entra ID or Nafath, only activation and sign-out are
  offered.
- **JSON import and export:** on every configuration screen and on the branding page, with "Copy all problems" in the
  review.
- **Other screens:**
  - An icon picker for icon fields.
  - The organization structure as a tree.
  - A redesigned Action Center.
  - A steadier side menu: one current item, and it no longer moves when a page opens.
  - The Dashboard as the first entry, and denser screens.

**3d delivered** ([ADR-0027](adr/0027-configuration-packages.md)):

- **Export** from every configuration table (ticked rows or the whole type), from the classes, menus, roles,
  branding and organization screens, and from *Change sets → Export configuration* (whole types, grouped by area).
  - *Include related configuration* adds what the rows refer to, the values of classes and the versions of
    services, workflows and rule sets.
  - Product rows are left out unless asked for.
  - Types come in apply order.
- **Packages** use `UPSERT`: create where missing, update where present. Up to 2000 items.
- **Import review:**
  - Counts of new, changed, deleted, unchanged and failing items.
  - A field-by-field comparison of each changed row with the row as it is.
  - Unchanged items are hidden, and the import skips them. Re-importing an export drafts nothing.
- Change-set details show nested changes the same way.
- **Fix:** a development Keycloak that re-imports its realm no longer breaks sign-in into Apps Control.
  - Realm users have fixed IDs.
  - Development re-links accounts by username.
  - Production refuses the sign-in with 403 instead of failing with 500.

**Portal aligned with the DGA entity-portal layout** ([ADR-0028](adr/0028-portal-dga-layout.md)):

- **Title bands:** on the home, services, requests, documents, FAQ and content pages, over the entity's hero image.
- **Shell:** a filled current section in the header, "En" / "ع", the last-modified line in Riyadh time, and
  underlined footer links with the entity logo in white.
- **Home:** search with a dark button (its menu searches one category), underline category tabs, DGA cards with
  tags, a carousel paged by views with dots, and centred statistics.
- **Service page:**
  - The start button beside the title, underline tabs and a bordered side card with contacts.
  - Related services on a light green band.
  - Comments and suggestions, a rating out of five stars, and "Was this page helpful?".
- **Requests and documents:** filter panels (status and search) over portal tables.
- **Request form:** the wizard beside the entity's contact card, with 48px fields.
- **Branding:** the entity's contact details (phone, email, location, map link, social accounts) and the portal hero
  image.
- **Visitor feedback:** answers and ratings for the content team (*Portal content → Visitor feedback*).

Still open from 3d: following references inside structured definitions (questions in a service version, roles in a
workflow graph), carrying branding images in a package, and comparing two environments directly.

**3e delivered** ([ADR-0029](adr/0029-field-visits.md)):

- **Visit types** (*Field visits → Visit types*): carried out in the field app, by an external inspection body, or
  recorded by staff. Each type has its outcomes (pass, fail, other), the fields the inspector fills in, the evidence
  required (photos, position, signature), the inspectors' role and the days to complete. External types name the
  booking operation, the inputs as rules, the result operation polled by the worker, the result paths, an outcome
  map and the signing secret of notifications.
- **Workflow `VISIT` step:**
  - In the designer: drag from *visited*, *on an outcome* and *cancelled*.
  - The site address and position come from rules, and instructions and days are set on the step.
  - The outcome and the recorded values are available to later rules as `visits.<step>`.
  - The test run lets you pick an outcome or cancel the visit.
  - Requests show *Awaiting site visit*; the deadline keeps running.
- **Visits register** (*Field visits → Visits*):
  - Search and filters (status, type, overdue).
  - Schedule and assign to an inspector of the type's role, least busy first.
  - Record a result by hand, cancel with a reason (the request takes its cancelled way), or check an external
    result now.
  - The result page shows the recorded values, photos, the signature and the inspector's position.
- **Field app** (`http://localhost:4202`, installable):
  - The inspector's visits stay on the phone (IndexedDB) and the app opens offline (service worker).
  - Start a visit, then directions, outcome, the type's fields, camera photos (reduced on the phone), GPS position
    and the representative's signature.
  - A draft is kept as the report is filled in.
  - Reports go to an outbox, are sent when the connection returns, and are recorded once even if sent twice.
- **External inspections:** the development gateway's inspection body (CIS) books inspections, answers results
  after 30 seconds and offers a page that reports a result through a signed notification.
- **Requests** show their visits in the portal (date, site, outcome) and on the internal request page.
- **Package** `site-visits.json`:
  - Two visit types: a site inspection by the entity's inspectors, and a safety inspection by the CIS.
  - The CIS operations, the roles *Field inspector* and *Visits supervisor*, and the users `inspector1` and
    `supervisor`.
  - New workflow versions: *Site visit request* gets an inspector's visit, *Temporary event permit* a safety
    inspection.
  - Four messages with their rules: to schedule, scheduled, assigned, done.

Still open from 3e: automatic assignment and inspectors' calendars and routes, follow-up visits created from a
result, push notifications to the field app, and offline maps.

Scope of the slices:

- **Workflow Designer** (canvas): drag nodes (human task, parallel split / join, gateway, decision, document,
  payment, end; notification, integration and timer steps once the engine runs them) from a palette, connect them, edit each node in a side panel
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

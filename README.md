# Dynamic Cube

A highly configurable, on-premises platform for building and running government e-services:
applications, workflows, integrations, notifications, issued documents and reporting, all
defined as configuration rather than code.

Think of it as a simpler, government-focused on-prem alternative to Dynamics 365 / Pega:
one installation per government entity (ministry, authority or company), deployable on
Kubernetes or on classic N-tier servers.

## Status

**Phase 0 (foundations)**, **Phase 1a (Apps Control + sign-in)**, **Phase 1b (Integration Manager + Party
Registry)**, **Phase 1c (Services)**, **Phase 1d (Workflow + Action Center)** and **Phase 1e (documents, decisions,
notifications)** are in place: platform kernel, API hosts, worker, migrator, Angular workspace, Keycloak realms,
containers and CI; the Apps Control module (org structure, users, roles and privileges, menus, Class/Domain with
typed attributes, LOVs with grants and data scopes, settings, UI texts); the Integration Manager (government APIs
connected by configuration, test console, masked call log) and the Party Registry (individuals, establishments,
acting contexts from MCI, profile data, credentials, projects, sanctions); Services (versioned service definitions
with a question bank, levels, fees and eligibility; the applicant's wizard with drafts and submission); workflows
(parallel reviews with maker-checker, distribution, returns to the applicant, deadlines on the working calendar) and
the employees' Action Center; issued documents with PDF printing and public QR verification, versioned decision rules
with a test console, and notifications (in-app, email, SMS) driven by rules over events. Every module's
configuration is edited through the same maker-checker change sets. Phase 2 has started: follow-up services on issued
documents (renewal, extension) and payments (SADAD bills, signed payment notifications, a workflow step that waits
for payment). Phase 3 (builders) has started with the Workflow Designer: workflows drawn on a canvas, conditions
without JSON, checks as you draw and a test run. See the [roadmap](docs/07-roadmap.md) for what comes next.

## Repository layout

```
src/
  Hosts/Internal.Api   Internal API (employees, builders, Apps Control) — internal network only
  Hosts/External.Api   External API (applicants) — internet-facing
  Hosts/Worker         Outbox delivery, notifications, integrations, timers
  Modules/AppsControl  Configuration & user management (schema apps): API, change appliers, LOV engine, access
  Modules/Integration  Integration Manager (schema integ): connections, operations, gateway, call log
  Modules/Parties      Party Registry (schema party): parties, relationships, acting contexts, profile, sanctions
  Modules/Services     Services (schema svc): services, question bank, versions, requests, eligibility, fees
  Modules/Workflow     Workflows (schema wf): definitions, engine, tasks, Action Center, deadlines
  Modules/Documents    Issued documents (schema doc): types, print templates, numbers, versions, PDF, verification
  Modules/Decisions    Decision rules (schema dec): rule sets, versions, built-in RULES engine, evaluation log
  Modules/Communication Notifications (schema com): message templates, rules, email/SMS/in-app, message log
  Modules/Payments     Payments (schema pay): providers, bills, signed payment notifications, receipts, expiry
  Shared/Platform      Kernel: persistence base, audit, outbox, change sets, files, JSONLogic, i18n
  Shared/Hosting       Shared host setup: logging, telemetry, health, Keycloak JWT
  Tools/Migrator       Applies database migrations (run before upgrades)
  Tools/MockGateway    Development stand-in for the MoMRAH API gateway (MCI operations, SADAD bills and payment page) — never deployed
tests/
  Platform.Tests       Unit tests
  Platform.IntegrationTests  PostgreSQL integration tests
  AppsControl.Tests    Apps Control integration and API tests (PostgreSQL)
  Integration.Tests    Gateway behaviour against a fake provider, configuration and API tests
  Parties.Tests        Acting contexts, registry operations, portal API tests
  Services.Tests       Versions, drafts, validation, fees, eligibility, portal API and package tests
  Workflow.Tests       Engine, Action Center, SLA, decision and document steps, the classification workflow
  Documents.Tests      Issuing, amendments, printing, status actions, reminders, verification
  Decisions.Tests      Rules engine, versions, evaluation log, calculators
  Communication.Tests  Rules, dispatch, delivery and retries, notifications, a real SMTP check (optional)
  Payments.Tests       Bills, provider registration, signed notifications over HTTP, expiry, staff actions
  shared/ModuleTests   Test database, fake provider and MoMRAH samples shared by the module test projects
  Hosts.Tests          API host smoke tests
  shared/              Cases shared by the .NET and Angular JSONLogic evaluators
web/                   Angular workspace: internal, external, field apps + shared library
deploy/docker          Dockerfiles, Nginx template, docker-compose.yml
deploy/keycloak        Realm definitions (internal + external) for development
deploy/config-packages Ready-made configuration to import as change sets (e.g. the MCI connectors)
deploy/sample-data     Sample configuration of reference projects (e.g. the radiation registry's lookups, roles, facilities)
docs/                  Architecture, domain model, ADRs
```

## Getting started

### Option A — everything in containers

Requires Docker.

```bash
docker compose -f deploy/docker/docker-compose.yml up --build
```

| URL | What |
|---|---|
| http://localhost:4200 | Internal system |
| http://localhost:4201 | E-services portal (external) |
| http://localhost:4202 | Field app |
| http://localhost:8080 | Keycloak (admin / admin) |
| http://localhost:5080/health/ready | Internal API readiness |
| http://localhost:5090 | Mock MoMRAH gateway (development only) |
| http://localhost:8025 | Mailpit — every email the platform sends in development |
| http://localhost:4201/verify | Public document verification (the QR code on each document opens it) |

Gotenberg (PDF printing) runs with the rest; add `--profile full` to also start ClamAV (upload scanning).

### Accounts and connections (development)

| What | Where | Sign-in |
|---|---|---|
| Internal system | http://localhost:4200 | `admin` (all Apps Control rights), `maker` (drafts changes), `checker` (approves); classification staff `manager`, `analyst1`, `analyst2`, `auditor1`, `director` (roles from the workflow package) — password `Dev@12345` |
| E-services portal | http://localhost:4201 | `1000000001`, `2000000002` (applicants) — password `Dev@12345`; browsing needs no sign-in |
| Field app | http://localhost:4202 | Same accounts as the internal system |
| Keycloak admin console | http://localhost:8080/admin | `admin` / `admin` (`KEYCLOAK_ADMIN_PASSWORD`) |
| PostgreSQL | `localhost:5432`, database `dynamic_cube` | `dynamic_cube` / `dynamic_cube` (`POSTGRES_PASSWORD`) |

To open the database in DataGrip (or DBeaver, pgAdmin): *New → Data Source → PostgreSQL*, host `localhost`, port
`5432`, database `dynamic_cube`, user `dynamic_cube`, password `dynamic_cube`. Tables are grouped by schema:
`apps` (Apps Control), `integ` (Integration Manager), `party` (Party Registry), `svc` (Services), `wf` (workflows and
tasks), `doc` (issued documents), `dec` (decision rules and evaluations), `com` (notifications and the message log),
`pay` (payment providers and bills), `cfg` (change sets),
`audit` (audit log), `platform` (outbox, keys).

The **field app** is the inspectors' app for site visits (offline-capable, phone/tablet). It is a shell for now;
visit forms, offline sync and inspection reports arrive with visits in Phase 3.

These are development values only; change them in `deploy/docker/.env` (see `.env.example`) for anything else.

### Trying the integrations and acting contexts

1. Sign in to the internal system as `maker`, open **Apps Control → Change sets → Import package** and choose
   `deploy/config-packages/momrah-mci.json`. Submit the two drafts it creates; sign in as `checker` and approve them.
2. In Docker Compose, edit the connection `MOMRAH_GATEWAY` (**Integrations → Connections**) and set the base URL
   to `http://mock-gateway:8080` (with Option B it already points at `http://localhost:5090`).
3. **Integrations → Test console**: run `MCI_CR_BY_ID_NUMBER` with `1000000001`; the call appears in the call log
   with the ID number masked.
4. Sign in to the portal as `1000000001`: the home page lists the establishments this person may act for (owner
   and partner; the board seat and the expired CR are left out). The chosen one is sent with every API call.
5. **Party registry → Parties** shows the establishments registered from MCI; *Refresh data* fills their profile
   from `MCI_CR_INFO`.

6. Import `deploy/config-packages/classification-new.json` the same way and approve it. Publishing the service
   also creates its workflow with a first version that needs no review, so a request submitted now is approved at
   once (**Services → Services** links each service to its workflow versions). In the portal, acting for
   the establishment, open **Services → City service providers classification**: choose a level, sectors (the
   sections follow the choice; shared questions are asked once), staff, projects, governance and financial data,
   see the fee, review, accept the pledge and submit. The request appears under **My requests** with its number and
   in the internal system under **Services → Requests**.
7. Import `deploy/config-packages/classification-workflow.json` (the import shows every item and any problem
   before anything is created) and approve its two change sets. It adds the classification roles and staff and version 2 of the service's
   workflow (see **Action Center → Workflow versions** for its graph). Submit a new
   classification request in the portal, then in the internal system:
   - `manager`: **Action Center → My tasks → Available**, claim *Review and distribute*, choose *Send for analysis*;
     under *To distribute* assign governance to `analyst2`, projects and financial to `analyst1`.
   - `analyst2`: open the governance analysis and *Ask the applicant for corrections* with a reason. The portal shows
     the reason; the applicant edits and resubmits, and the task comes back to `analyst2`, who sends it for audit.
     `analyst2` also audits governance, but not their own analysis: it goes to `auditor1`.
   - `analyst1` sends both analyses for audit; `auditor1` claims and confirms the three audits; `director` approves.
     The portal shows *Approved* with the request's history; the internal request page shows its workflow.
8. Import `deploy/config-packages/classification-certificate.json` and approve its five change sets (party fields,
   certificate, grading rules, workflow version 3, messages). In **Party registry → Parties**, give the establishment
   an `EMAIL` (and `MOBILE`) so it receives email. Submit another classification request and process it as in 7:
   after the three audits the grade is calculated (the task page shows the grade per sector under *Results of
   automated steps*); when `director` approves, the certificate `CC-YYYY-000001` is issued (a second approval for the
   same establishment amends it: same number, new version). Then:
   - the portal's bell, **My documents** and the request page show the certificate; *Download PDF* prints it;
   - its QR code opens `http://localhost:4201/verify/<code>` for anyone, signed in or not;
   - the emails are in Mailpit (http://localhost:8025); SMS are recorded as skipped until an SMS operation is
     configured (`Communication:Sms:Operation`);
   - in the internal system: **Documents → Issued documents** (preview, PDF, suspend / reinstate / cancel),
     **Documents → Print templates** (edit with live preview), **Decisions → Rule-set versions** (try a version on
     an input, see the evaluations), **Notifications → Message log** and the bell (staff are told about tasks
     assigned to them).
9. Import `deploy/config-packages/portal-services.json` and approve its two change sets (portal setting, services).
   Open http://localhost:4201 without signing in: the home page has the search with categories, the category tabs,
   the services in a carousel and the statistics tiles. Open a service: its card page shows the steps, terms,
   required documents, duration, channels and cost; **Start service** asks you to sign in and then starts a draft.
   Categories are edited under **Services → Service categories**, the home page under **Apps Control → Settings**
   (`PORTAL_HOME`) and a service's card in its version (`definition.card`).
10. Import `deploy/config-packages/classification-renewal.json` and then `classification-renewal-workflow.json`, and
   approve them. In the portal, acting for the establishment, open **My documents** and the certificate: under
   *Services on this document* renewal and extension are locked, with the dates their windows open (renewal from 90
   days before expiry). When a window is open, *Start service* makes a request for that certificate (its number and
   expiry are filled in); on approval the certificate keeps its number and gets a new version and expiry date.
11. Import `deploy/config-packages/payments-sadad.json` and then `classification-payment.json`, and approve them. In
   Docker Compose set the provider's payment page (**Payments → Payment providers**, `SADAD`) to
   `http://localhost:5090/sadad/pay/{billNumber}` if the mock gateway is published elsewhere. Submit a *Premium*
   classification request: it is *Awaiting payment* and its page shows the bill (amount, SADAD number, biller code,
   due date). *Pay now* opens the mock SADAD page; paying there sends a signed notification to the external API, and
   the request moves on to the request manager's review with the receipt on its page. In the internal system,
   **Payments → Bills** lists the bills; a bill's page shows its history and lets staff record a payment made
   elsewhere or cancel the bill (the request then closes as *not paid*). Unpaid bills expire after 7 days.
12. Sign in to the internal system as `maker` and open **Action Center → Workflow designer** (or *Open in the
   designer* on a row of **Workflow versions**). Choose a service's workflow: its latest version is drawn and the
   designer prepares the next one. Add steps from the palette, connect them by dragging from the dots under a step,
   select a step or an arrow to edit it in the side panel (a gateway's conditions are picked from the service's
   questions and levels), and watch the problems list as you go. **Test run** tries it with sample answers (you
   choose each task's action and whether the bill is paid); **Check on the server** runs the publish checks;
   **Add to draft** saves it as the next version, which `checker` approves like any other change.

The gateway credentials come from the secret store (`Secrets__MOMRAH_GATEWAY__clientId` / `__clientSecret`), never
from the database; development values are in `appsettings.Development.json` and the compose file.

### Option B — local development

Requires .NET 10 SDK, Node.js 24 LTS and Docker (for PostgreSQL and Keycloak only). Start the parts **in this
order**, each in its own terminal, from the repository root:

```bash
# 1. Database, identity provider, PDF printer and mail catcher
#    (PostgreSQL :5432, Keycloak :8080 with the dev realms, Gotenberg :3000, Mailpit :1025 / UI :8025)
docker compose -f deploy/docker/docker-compose.yml up -d postgres keycloak gotenberg mailpit

# 2. Create / upgrade the database (run again after pulling changes)
dotnet run --project src/Tools/Migrator

# 3. APIs — the web dev servers forward /api to these ports
dotnet run --project src/Hosts/Internal.Api        # http://localhost:5080
dotnet run --project src/Hosts/External.Api        # http://localhost:5081
dotnet run --project src/Hosts/Worker              # notifications, PDF printing, deadlines, document dates
dotnet run --project src/Tools/MockGateway         # http://localhost:5090 — fake MoMRAH gateway (optional)

# 4. Front ends
cd web && npm ci
npm run start:internal                              # http://localhost:4200  → Internal.Api
npm run start:external                              # http://localhost:4201  → External.Api
```

Ports and the `Development` environment come from each project's `Properties/launchSettings.json`, so plain
`dotnet run` (or F5 in Visual Studio / Rider) needs no extra arguments. In development the three hosts keep stored
files (printed PDFs, uploads) in `.data/storage` at the repository root, as the containers share one volume.

| Symptom | Cause |
|---|---|
| `[vite] http proxy error … ECONNREFUSED` | The API behind that app is not running on its port (5080 internal, 5081 external) |
| Migrator: `Connection string … is not configured` | Not started in `Development` (use `dotnet run`, or set `DOTNET_ENVIRONMENT=Development`) |
| Migrator / API: `Failed to connect to 127.0.0.1:5432` | PostgreSQL is not running (step 1) |
| Sign-in page does not load | Keycloak is not running on :8080 (step 1; first start takes ~30 s) |

Without Docker: install PostgreSQL 16+ with role and database `dynamic_cube` / password `dynamic_cube`, and
Keycloak 26 started with `kc.sh start-dev --http-port 8080 --import-realm` after copying
`deploy/keycloak/realms/*.json` into its `data/import` folder.

### Tests

```bash
# .NET — PostgreSQL integration tests run when this variable is set, otherwise they are skipped
export DYNAMICCUBE_TEST_POSTGRES="Host=localhost;Username=dynamic_cube;Password=dynamic_cube;Database=postgres"
# Optional: print through a real Gotenberg and send through a real SMTP server (Mailpit)
export DYNAMICCUBE_TEST_GOTENBERG=http://localhost:3000 DYNAMICCUBE_TEST_MAILPIT=http://localhost:8025
dotnet test

# Angular
cd web && npm test && npm run lint
```

## Documentation

| Document | Purpose |
|---|---|
| [Vision & Scope](docs/01-vision-and-scope.md) | What we are building, principles, service archetypes |
| [Architecture](docs/02-architecture.md) | Modular monolith, hosts, network zones, deployment, stack |
| [Domain Model](docs/03-domain-model.md) | Party, Service, Request, Workflow, Issued Document, Configuration lifecycle |
| [Apps Control](docs/04-apps-control.md) | Configuration & user management data model (Class/Domain, LOV, roles, menus, org) |
| [Integration Manager](docs/05-integration-manager.md) | Configured connectors, envelopes, operations, mappings |
| [Reference Service: Classification Certificate](docs/06-reference-service-classification.md) | First end-to-end service used to drive the platform |
| [Roadmap](docs/07-roadmap.md) | Phases and vertical slices |
| [Architecture Decision Records](docs/adr/README.md) | One record per significant decision |
| [Glossary](docs/glossary.md) | Arabic / English terms |

## Technology (summary)

.NET 10 (ASP.NET Core) · Angular 22 + Angular Material in the DGA design system · PostgreSQL · Keycloak · Quartz.NET ·
Gotenberg (HTML→PDF) · Docker / Helm / N-tier. Permissive open-source licenses only
(see [ADR-0006](docs/adr/0006-permissive-licenses-only.md)).

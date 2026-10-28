# Dynamic Cube

A highly configurable, on-premises platform for building and running government e-services:
applications, workflows, integrations, notifications, issued documents and reporting, all
defined as configuration rather than code.

Think of it as a simpler, government-focused on-prem alternative to Dynamics 365 / Pega:
one installation per government entity (ministry, authority or company), deployable on
Kubernetes or on classic N-tier servers.

## Status

**Phase 0 (foundations)** and **Phase 1a (Apps Control + sign-in)** are in place: platform kernel, API hosts,
worker, migrator, Angular workspace, Keycloak realms, containers and CI, plus the Apps Control module (org
structure, users, roles and privileges, menus, Class/Domain with typed attributes, LOVs with grants and data
scopes, settings, UI texts — all edited through maker-checker change sets) and its screens in the internal
system. See the [roadmap](docs/07-roadmap.md) for what comes next.

## Repository layout

```
src/
  Hosts/Internal.Api   Internal API (employees, builders, Apps Control) — internal network only
  Hosts/External.Api   External API (applicants) — internet-facing
  Hosts/Worker         Outbox delivery, notifications, integrations, timers
  Modules/AppsControl  Configuration & user management (schema apps): API, change appliers, LOV engine, access
  Shared/Platform      Kernel: persistence base, audit, outbox, change sets, files, JSONLogic, i18n
  Shared/Hosting       Shared host setup: logging, telemetry, health, Keycloak JWT
  Tools/Migrator       Applies database migrations (run before upgrades)
tests/
  Platform.Tests       Unit tests
  Platform.IntegrationTests  PostgreSQL integration tests
  AppsControl.Tests    Apps Control integration and API tests (PostgreSQL)
  Hosts.Tests          API host smoke tests
  shared/              Cases shared by the .NET and Angular JSONLogic evaluators
web/                   Angular workspace: internal, external, field apps + shared library
deploy/docker          Dockerfiles, Nginx template, docker-compose.yml
deploy/keycloak        Realm definitions (internal + external) for development
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

Add `--profile full` to also start Gotenberg (PDF) and ClamAV (upload scanning).

### Option B — local development

Requires .NET 10 SDK, Node.js 24 LTS and Docker (for PostgreSQL and Keycloak only). Start the parts **in this
order**, each in its own terminal, from the repository root:

```bash
# 1. Database and identity provider (PostgreSQL on :5432, Keycloak on :8080 with the dev realms)
docker compose -f deploy/docker/docker-compose.yml up -d postgres keycloak

# 2. Create / upgrade the database (run again after pulling changes)
dotnet run --project src/Tools/Migrator

# 3. APIs — the web dev servers forward /api to these ports
dotnet run --project src/Hosts/Internal.Api        # http://localhost:5080
dotnet run --project src/Hosts/External.Api        # http://localhost:5081
dotnet run --project src/Hosts/Worker              # background jobs (optional for now)

# 4. Front ends
cd web && npm ci
npm run start:internal                              # http://localhost:4200  → Internal.Api
npm run start:external                              # http://localhost:4201  → External.Api
```

Ports and the `Development` environment come from each project's `Properties/launchSettings.json`, so plain
`dotnet run` (or F5 in Visual Studio / Rider) needs no extra arguments.

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

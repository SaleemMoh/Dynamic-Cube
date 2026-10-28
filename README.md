# Dynamic Cube

A highly configurable, on-premises platform for building and running government e-services:
applications, workflows, integrations, notifications, issued documents and reporting, all
defined as configuration rather than code.

Think of it as a simpler, government-focused on-prem alternative to Dynamics 365 / Pega:
one installation per government entity (ministry, authority or company), deployable on
Kubernetes or on classic N-tier servers.

## Status

**Phase 0 (foundations) is in place**: solution layout, platform kernel, API hosts, worker, migrator,
Angular workspace, Keycloak realms, container images, Docker Compose and CI. See the
[roadmap](docs/07-roadmap.md) for what comes next.

## Repository layout

```
src/
  Hosts/Internal.Api   Internal API (employees, builders, Apps Control) — internal network only
  Hosts/External.Api   External API (applicants) — internet-facing
  Hosts/Worker         Outbox delivery, notifications, integrations, timers
  Shared/Platform      Kernel: persistence base, audit, outbox, change sets, files, JSONLogic, i18n
  Shared/Hosting       Shared host setup: logging, telemetry, health, Keycloak JWT
  Tools/Migrator       Applies database migrations (run before upgrades)
tests/
  Platform.Tests       Unit tests
  Platform.IntegrationTests  PostgreSQL integration tests
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

Requires .NET 10 SDK, Node.js 24 LTS, PostgreSQL 16+ and (for login) Keycloak 26.

```bash
# Database (once): role/database dynamic_cube / dynamic_cube on localhost:5432
dotnet tool restore
dotnet run --project src/Tools/Migrator            # applies migrations

dotnet run --project src/Hosts/Internal.Api --urls http://localhost:5080
dotnet run --project src/Hosts/External.Api --urls http://localhost:5081
dotnet run --project src/Hosts/Worker

cd web && npm ci
npm run start:internal                              # http://localhost:4200 (proxies /api to :5080)
npm run start:external                              # http://localhost:4201 (proxies /api to :5081)
```

Keycloak for development: import `deploy/keycloak/realms/*.json` (see
[deploy/keycloak/README.md](deploy/keycloak/README.md) for users and production notes).

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

.NET 10 (ASP.NET Core) · Angular 22 + Angular Material · PostgreSQL · Keycloak · Quartz.NET ·
Gotenberg (HTML→PDF) · Docker / Helm / N-tier. Permissive open-source licenses only
(see [ADR-0006](docs/adr/0006-permissive-licenses-only.md)).

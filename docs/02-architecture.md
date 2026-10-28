# 2. Architecture

## 2.1 Overview

Dynamic Cube is a **modular monolith**: one codebase, clean module boundaries, a small number
of deployable processes. Modules talk to each other through plain C# interfaces in-process.
The only asynchronous mechanism is a **PostgreSQL outbox** processed by the Worker.

```
                Internet                                   Entity internal network
                   │                                                  │
        ┌──────────▼──────────┐                           ┌───────────▼──────────┐
 DMZ    │ External Web (SPA)  │                           │ Internal Web (SPA)   │
        │ External.Api        │                           │ Internal.Api         │
        └──────────┬──────────┘                           └───────────┬──────────┘
                   │                                                  │
        ───────────┴──────────────────────┬───────────────────────────┴─────────
                                          │
   App / data tier  ┌──────────┐  ┌───────▼──────┐  ┌─────────┐  ┌────────────┐
                    │ Worker   │  │ PostgreSQL   │  │ Files   │  │ Keycloak   │
                    └──────────┘  └──────────────┘  └─────────┘  └────────────┘
                    ┌──────────┐  ┌──────────────┐
                    │ Gotenberg│  │ ClamAV       │  (PDF rendering, upload scanning)
                    └──────────┘  └──────────────┘
```

## 2.2 Deployable units

| Unit | Zone | Purpose |
|---|---|---|
| **External Web** | DMZ | Angular SPA for applicants (static files behind Nginx/IIS) |
| **External.Api** | DMZ | Only applicant-facing endpoints (apply, track, pay, verify, calculator, field app API if enabled) |
| **Internal Web** | Internal | Angular SPA for employees: Action Center, builders, Apps Control |
| **Internal.Api** | Internal | Employee, admin and builder endpoints |
| **Worker** | Internal | Outbox, notifications, integration calls, SLA timers, scheduled jobs (Quartz.NET), draft expiry, document-date alerts |
| **PostgreSQL** | Data | Single database, one schema per module |
| **File storage** | Data | Filesystem/NAS by default; S3-compatible optional (storage abstraction) |
| **Keycloak** | Internal (+ DMZ for external realm login) | Authentication: internal realm (AD/LDAP federation), external realm (Nafath) |
| **Gotenberg** | Internal | HTML → PDF for certificates and letters (correct Arabic shaping) |
| **ClamAV** | Internal | Virus scanning of uploads from the internet (separate process) |

The two API hosts are **separate projects built from the same modules**. Each host registers
only the endpoints of its zone — the external host physically does not contain admin, builder
or employee endpoints.

Default data access: both API hosts connect to PostgreSQL with **separate database roles**
(least privilege). Entities that forbid DMZ → database connections can switch the external
host to call the internal API instead (option, not built on day one).

## 2.3 Modules

| Module (namespace) | DB schema | Responsibility |
|---|---|---|
| `AppsControl` | `apps` | Installation, apps, entity & org structure, Class/Domain, LOVs, data scopes, settings, UI texts, menus, users, roles, privileges, delegations |
| `Integration` | `integ` | Connections, auth profiles, envelopes, operations, mappings, call logs, API endpoint builder |
| `Communication` | `comm` | Channels, providers, templates, notification rules, outbound messages |
| `Parties` | `party` | Individuals, establishments, delegations, external credentials, projects, sanctions |
| `Services` | `svc` | Service definitions & versions, forms, question bank, fees, service levels, eligibility & availability rules, outputs |
| `Workflow` | `wf` | Workflow definitions & versions, engine, instances, tasks, actions, SLA timers |
| `Requests` | `req` | Requests, sub-requests, data payload, timeline, attachments, payments, visits |
| `Documents` | `doc` | Issued document registry, versions, line items, templates, verification |
| `Decisions` | `dec` | Decision step contract, rule sets (engine implementation pluggable) |
| `Reporting` | `rpt` | Datasets, generated views, dashboards, report definitions |
| `Content` | `cms` | Portal content manager |
| `Platform` (shared kernel) | `cfg`, `audit` | Configuration change sets (maker-checker), audit log, outbox, expression evaluation, localization, file abstraction |

Rules:

- A module owns its schema; other modules use its **public interface**, never its tables.
- Reporting reads through **generated views**, not module tables directly.
- Apps Control additionally exposes a **versioned REST API** (see
  [04-apps-control.md §4.12](04-apps-control.md#412-apps-control-api)) so other products can
  consume it later.

## 2.4 Solution layout

```
src/
  Hosts/
    Internal.Api/            # internal network only
    External.Api/            # internet-facing (DMZ)
    Worker/                  # jobs, outbox, notifications, integrations, SLA
  Modules/
    AppsControl/
    Integration/
    Communication/
    Parties/
    Services/
    Workflow/
    Requests/
    Documents/
    Decisions/
    Reporting/
    Content/
  Shared/
    Platform/                # kernel: config lifecycle, audit, outbox, expressions, i18n, files
web/                         # one Angular workspace
  projects/
    internal/                # internal system (Action Center + builders + Apps Control)
    external/                # external system (applicant portal)
    field/                   # field inspection PWA
    shared/                  # design system, form renderer, i18n, auth
deploy/
  docker/                    # Dockerfiles, docker-compose for small sites
  helm/                      # Helm chart (Kubernetes / OpenShift)
  ntier/                     # systemd units, Nginx/IIS configs, install scripts
docs/
```

Each module is internally layered (`Domain`, `Application`, `Infrastructure`, `Endpoints`),
kept light — no mediator library, no auto-mapper.

## 2.5 Cross-cutting concerns

### Identity & authorization
- **Authentication** in Keycloak (OIDC). Internal realm federates AD/LDAP; external realm uses
  Nafath as identity provider.
- **Authorization** in Apps Control: roles → privileges (resource + actions), data scopes
  (LOV grants), field rules. Resolved server-side and cached per session; tokens stay small.
- External users act **on behalf of** an establishment selected after login (delegation).

### Configuration lifecycle
- Versioned configuration (services, workflows, forms, templates, rule sets): draft → published
  (immutable) → retired; instances pinned to their version.
- Simple configuration (lookups, settings, menus, roles): edited through **change sets** with
  optional maker-checker (draft → submitted → approved → published).
- Export/import packages move configuration between environments using codes as keys.
  Ownership `SYSTEM` vs `ENTITY` decides what product upgrades may overwrite.

### Data storage
- Relational tables for platform and configuration entities.
- **JSONB** for request payloads and Class/Domain attributes, with **promoted columns** for
  fields used in queries and **generated typed views** for reporting.
- PostgreSQL `ltree` for org-unit subtrees; GIN indexes for JSONB.
- Ids: `uuid` (v7) primary keys; business `code` columns for configuration.

### Expressions
- Conditions (visibility, validation, routing, grants, availability) are stored as
  **JSONLogic**, produced by visual builders and evaluated identically in .NET and Angular.
  The server re-evaluates everything (authoritative).
- Advanced script steps (opt-in, privileged): sandboxed JavaScript via Jint.

### Reliability
- **Outbox** table written in the same transaction as the business change; Worker delivers
  notifications and integration pushes with retries and backoff.
- Integration calls: timeouts, retries, circuit breaker, full request/response log with masked
  PII.

### Audit
- Every change to business data and configuration is written to `audit.audit_log`
  (who, when, what, before/after, source app, IP). Required for NCA ECC controls.

### Localization & calendars
- `name_ar` / `name_en` columns everywhere; UI texts configurable in Apps Control.
- Hijri/Gregorian display; Saudi working calendar (weekends, holidays) for SLA calculations.

### Observability
- Serilog (structured logs) + OpenTelemetry (traces/metrics), exportable to whatever the
  customer runs.

## 2.6 Technology stack

| Concern | Choice | License |
|---|---|---|
| Runtime | .NET 10 LTS, ASP.NET Core | MIT |
| ORM / driver | EF Core + Npgsql | MIT / PostgreSQL |
| Database | PostgreSQL | PostgreSQL |
| Validation | FluentValidation | Apache-2.0 |
| Mapping | Manual / Mapperly | Apache-2.0 |
| Scheduling | Quartz.NET | Apache-2.0 |
| Expressions | JSONLogic (.NET + JS libs), Jint | MIT / BSD-2 |
| Templates | Fluid (Liquid) | MIT |
| PDF | Gotenberg | MIT |
| Identity | Keycloak | Apache-2.0 |
| Logs / traces | Serilog, OpenTelemetry | Apache-2.0 |
| Front-end | Angular, PrimeNG | MIT |
| Workflow canvas | Foblex Flow or ngx-vflow | MIT |
| Charts | Apache ECharts | Apache-2.0 |
| Tests | xUnit, Shouldly, Testcontainers | Apache / BSD / MIT |
| Upload scanning | ClamAV (separate process) | GPL (not linked) |

Not used, because of licensing: MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+,
QuestPDF, Duende IdentityServer. See [ADR-0006](adr/0006-permissive-licenses-only.md).

## 2.7 Deployment models

**Kubernetes / OpenShift** — container images per unit + Helm chart. External units in a DMZ
namespace / ingress; internal units on the internal ingress.

**N-tier on servers / VMs**
- Web tier: Nginx (or IIS) serving the Angular apps and reverse-proxying the APIs.
- App tier: `Internal.Api`, `External.Api`, `Worker` as systemd services (Linux) or IIS /
  Windows services.
- Data tier: PostgreSQL, file share, Keycloak.

**Small sites** — Docker Compose bundle.

## 2.8 Field app and network zones

The internal system is reachable only from the internal network, but inspectors work in the
field. Per entity, choose one:

1. **Managed devices + VPN/MDM** — the field app talks to the internal API.
2. **Narrow field API in the DMZ** — a minimal endpoint set on `External.Api` with strong
   authentication (internal realm, device binding).

Both are supported by design; the choice is deployment configuration. ⚑ Open per customer.

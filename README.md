# Dynamic Cube

A highly configurable, on-premises platform for building and running government e-services:
applications, workflows, integrations, notifications, issued documents and reporting, all
defined as configuration rather than code.

Think of it as a simpler, government-focused on-prem alternative to Dynamics 365 / Pega:
one installation per government entity (ministry, authority or company), deployable on
Kubernetes or on classic N-tier servers.

## Status

Design phase. The architecture and domain model are documented under [`docs/`](docs/README.md).
No application code yet.

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

.NET 10 (ASP.NET Core) · Angular + PrimeNG · PostgreSQL · Keycloak · Quartz.NET ·
Gotenberg (HTML→PDF) · Docker / Helm / N-tier. Permissive open-source licenses only
(see [ADR-0006](docs/adr/0006-permissive-licenses-only.md)).

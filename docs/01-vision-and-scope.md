# 1. Vision & Scope

## 1.1 What Dynamic Cube is

Dynamic Cube is a **government service engine**: a platform that lets a government entity
configure and run its e-services (applications submitted by citizens and establishments,
processed by employees, integrated with other government systems, and ending in an issued
document or decision) **without writing code for each service**.

It is **not SaaS**. Each customer (a ministry, authority or company) gets its **own on-prem
installation**, deployed in its own data center on Kubernetes/OpenShift or on classic
N-tier servers.

## 1.2 Why a "service engine" and not a blank-canvas low-code tool

Most government services follow the same skeleton:

**Applicant side**
1. Login (Nafath).
2. Pick a service; eligibility checks (internal rules + integrations).
3. Data auto-filled from integrations ("go and get").
4. Wizard: forms, attachments, fees (any step), review, declaration. Drafts are allowed.
5. Submit → request number.
6. Track; respond to "returned for correction"; pay.

**Employee side (Action Center)**
1. Inbox; distribution / claim.
2. Review (often several parallel reviews, each with maker/checker).
3. Actions: approve, reject, return to customer, forward, request third-party data,
   schedule visit, request payment, hold, escalate.
4. Final approval → output document (certificate / license / permit) with QR verification.
5. Notify, close.

Throughout: SLA timers, escalations, audit timeline, notifications.

Dynamic Cube **encodes this skeleton** and lets each service fill it in. That is what keeps it
simpler than a general-purpose platform while still being highly configurable.

## 1.3 Service archetypes

| # | Archetype | Input | Output |
|---|---|---|---|
| 1 | **Issuance** | New application | Creates an Issued Document (certificate, license, permit, approval on declaration…) |
| 2 | **Amendment** | An existing Issued Document + changes | A new version of that document (same or new number — configurable) |
| 3 | **Visit Request** | Applicant / site data | Visit report & outcome (visits can also be a *step* inside other services) |
| 4 | **Attestation** | An existing Issued Document | Attestation record / stamp on it |

Renewal, extension, cancellation, appeal and "add / remove scope" are configured as
amendment-style services that act on an Issued Document.

Cross-cutting capabilities usable as steps in any archetype:

- **Fees** — at the end or at one or more wizard / processing steps.
- **Integration** — inquire ("go and get") or push to third parties.
- **Visits** — field inspection steps (internal field app, external system, or manual entry).
- **Sub-requests** — a request can create child requests (prerequisite, bundled or spawned).
- **Decisions** — calculation / classification steps via a pluggable decision engine.

## 1.4 Components

| Component | Responsibility |
|---|---|
| **Apps Control** | Configuration Manager (Class/Domain lookups, LOVs, settings, menus, UI texts) and User Manager (users, roles, privileges, data scopes, org structure). Serves several apps. |
| **Integration Manager** | Configured connectors (REST/SOAP), auth profiles, response envelopes, operations, mappings, call logs, API endpoint builder. |
| **Communication Manager** | SMS, email, push, in-app; templates; event and scheduled notifications. |
| **Service Builder** | Service definitions, composable forms & wizards, validations, fees, service levels, eligibility, availability rules, outputs. |
| **Workflow Builder & Engine** | Processing workflows: human tasks, parallel branches, routing, timers/SLA, integration/decision/visit steps. |
| **Action Center** | Employee workspace: inboxes, distribution, take action, forward, return, contact customer. |
| **Party Registry** | Individuals and establishments, delegation, external credentials, project portfolios, sanctions. |
| **Issued Document Registry** | Certificates/licenses with versions, line items, statuses, verification. |
| **Reports & Dashboards** | Charts, statistics, KPI dashboards, report builder (data-scope aware). |
| **Portal Content Manager** | External portal content: sections, announcements, guides, service display. |

Apps on top of the platform:

- **Internal system** — employees; reachable only from the entity's internal network.
  Includes Action Center, builders and Apps Control screens (privilege-controlled).
- **External system** — applicants (citizens / establishments); internet-facing.
- **Field app** — inspectors (offline-capable PWA), when visits are done in-house.

## 1.5 Principles

1. **Everything configurable** — business specifics are data, never code.
2. **Don't over-engineer** — modular monolith, simple in-process communication, no broker,
   no service mesh. Add complexity only when a real requirement demands it.
3. **Deploy anywhere** — containers on Kubernetes/OpenShift *or* N-tier on VMs/servers.
   Nothing Windows-only.
4. **Free and reliable dependencies** — permissive licenses only (MIT / Apache-2.0 / BSD /
   PostgreSQL).
5. **Versioned configuration** — published configuration is immutable; requests stay pinned
   to the version they started on.
6. **Maker-checker everywhere** — configuration changes can require review and approval
   before publishing, with full audit.
7. **Security by zone** — internal and external systems are separate hosts with separate
   API surfaces.
8. **Arabic-first** — bilingual data, RTL layouts, Hijri dates, Saudi working calendar.

## 1.6 In scope / out of scope (initial)

**In scope**: everything in 1.4, the three apps, Saudi integrations via the Integration
Manager, on-prem deployment tooling, configuration export/import between environments.

**Out of scope for now**: multi-entity (multi-tenant) installations — one entity per
installation; AI assistant / chatbot (integration point only); custom-code plugin SDK
(planned later as an escape hatch).

## 1.7 Reference service

The **City Service Providers Classification Certificate** (MoMRAH guide, April 2026) is the
reference service for the first vertical slice. See
[06-reference-service-classification.md](06-reference-service-classification.md).

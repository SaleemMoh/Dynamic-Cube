# 3. Domain Model

This document describes the core business concepts and how they relate. Apps Control
(lookups, LOVs, users, roles, org structure) is detailed separately in
[04-apps-control.md](04-apps-control.md); integrations in
[05-integration-manager.md](05-integration-manager.md).

```
             ┌──────────────┐  defines   ┌───────────────────┐   runs as   ┌──────────────┐
             │ Service      │──────────▶ │ Service Version   │───────────▶ │ Request      │
             │ Definition   │            │ (form, fees, wf…) │             │ (instance)   │
             └──────────────┘            └───────────────────┘             └──────┬───────┘
                                                                                   │
        ┌──────────────┐  applies for / owns        ┌──────────────┐  issues /     │
        │ Party        │◀───────────────────────────│              │  amends       │
        │ (individual, │                            │ Workflow     │◀──────────────┤
        │ establishment│                            │ Instance &   │               │
        └──────┬───────┘                            │ Tasks        │               ▼
               │ holds                              └──────────────┘     ┌──────────────────┐
               └───────────────────────────────────────────────────────▶ │ Issued Document  │
                                                                         │ (versions, lines)│
                                                                         └──────────────────┘
```

---

## 3.1 Party Registry

A **Party** is anyone who applies for or holds something.

| Concept | Description |
|---|---|
| **Individual** | A person (national ID / iqama). Identified via Nafath at login; basic data from NIC (Yakeen-equivalent). |
| **Establishment** | A company / office / factory: CR number, unified number (700…), engineering license, etc. Profile data refreshed from integrations (MCI, Masdr/GOSI, Qawaem, SCE…). |
| **Relationship / Delegation** | Individual ↔ establishment: owner, manager, authorized delegate; scoped by services and validity dates. Sourced from integrations (Balady delegation, NIC active delegations, Balady business authorization) or managed in-system. |
| **Profile data** | Structured, refreshable attributes with source and `refreshed_at` (employee counts, Saudization, capital, revenue bracket, region, amanah…). A **refresh policy** per attribute decides when to re-fetch. |
| **External credentials** | Structured records issued by other authorities, with source, validity, file and **nested data** — e.g. the DGA vendor technical certificate (level → category → activity → value chain), NCA classification, SCE license. |
| **Project portfolio** | Projects with evidence documents (contract, BOQ, completion certificate, subcontract approval, revenue statement). Reused across requests and renewals; partly fetched from integrations (MOF contracts, Balady/Enjaz projects). |
| **Sanctions** | Bans and penalties (e.g. "may not apply for 6 months", "certificate suspended") recorded against a party; checked by eligibility rules. |

At login an external user selects the **acting context**: themselves, or an establishment they
are delegated for. Every request records both the **submitter** (individual) and the
**beneficiary** (party).

**Implementation (1b)** — module `Parties`, schema `party`:

- **Identity** — on first portal sign-in the individual is registered from the token's `national_id`
  claim (mapped from Nafath in Keycloak); `1…` numbers are national IDs, `2…` iqamas. Parties carry typed
  identifiers (national ID, iqama, GCC ID, passport, CR number, unified number), unique per type.
- **Acting contexts** — *context sources* (configuration) name an integration operation called with the
  individual's ID number and say where the items, identifiers, names, relation codes, statuses, validity
  and delegated services are in its result. The MCI source lists the CRs of a person with their relation
  type; provider codes are translated through the relationship types' external codes (seeded with the MCI
  guide's codes: owner, partners, managers can act; board members and other roles are recorded but cannot).
  Contexts are refreshed when older than the source's period or on request; relationships the provider no
  longer reports are ended, and when the provider fails the last known contexts stay (with a warning).
  Relationships recorded by employees (e.g. an authorized delegate for some services) are never ended by a
  refresh.
- **Acting party** — the portal sends the chosen party in the `X-Acting-Party` header; the API checks it
  against the applicant's active relationships whose type can act (`IActingPartyAccessor` in the kernel).
- **Profile fields** — integration-sourced fields name an operation, an output and which identifier feeds
  which input; one call per operation fills all its fields, and values older than the field's refresh period
  are fetched again. Manual fields are entered by employees.
- **Credentials, projects, sanctions** — recorded with source and validity; credentials keep nested data as
  JSON. A sanction must be lifted by someone other than who recorded it.
- Configuration (relationship types, context sources, profile fields, credential and sanction types) goes
  through change sets (area `party.registry`); registry operations are on the internal API, the acting
  contexts on the external API.

---

## 3.2 Service Definition

A **Service** is the configured definition of something applicants can request. It is
**versioned**: each published **Service Version** is immutable; drafts and requests are pinned
to the version they started on.

A Service Version contains:

| Part | Description |
|---|---|
| **Archetype** | Issuance · Amendment · Visit Request · Attestation |
| **Target document type** | For Amendment/Attestation: which Issued Document type it acts on (e.g. Classification Certificate) |
| **Eligibility rules** | JSONLogic conditions over party profile, credentials, sanctions and integration results; each failure has a bilingual reason |
| **Availability rules** | For document-based services: allowed document statuses + **date window** relative to issue/expiry (e.g. renewal = last 90 days before expiry). Drives "available now / locked" cards and countdowns in the external system |
| **Service levels** | Optional tiers (e.g. Regular / Premium / Diamond): SLA, priority, fee table, optional dedicated workflow branch |
| **Wizard** | Ordered steps; each step is a form section, attachments, fee, integration (go and get), review, declaration, or sub-request creation |
| **Composable forms** | Sections included according to applicant selections (e.g. chosen sectors). Fields reference a **question bank** by code so a shared question appears **once** even when referenced by several sections |
| **Validations** | Field- and cross-field rules (JSONLogic), evaluated client- and server-side |
| **Fees** | Fee items attached to steps; amount by fixed value, formula, or **bracketed lookup** (e.g. by annual revenue); VAT handling; payment via SADAD / Tahseel |
| **Workflow** | Reference to a published Workflow Version (§3.5) |
| **Outputs** | Document template(s) to issue or amend; numbering rule; validity rule |
| **Notifications** | Bindings from request/document events to templates and channels |
| **Draft policy** | Drafts allowed (default yes), expiry days, reminder; behaviour on new version publish (continue on old version — default — or restart) |
| **Decision steps** | Calls to the Decisions module (§3.7) |

Service-to-service links support offers such as a paid advisory service proposed after a
result.

**Implementation (1c)** — module `Services`, schema `svc`, configured through change sets (area
`svc.services`, one area so a version can use questions created in the same change set):

- **Service** — names, archetype, who may apply (individuals, establishments), catalog listing, draft
  policy (expiry days; on a new version drafts *continue* or *restart*), request number prefix.
- **Question bank** — typed questions (text, long text, number, date, choice with options or an Apps
  Control LOV with a parent question, yes/no), limits (min, max, length, pattern) and a *prefill* path
  into the applicant's facts (`party.identifiers.CR_NUMBER`, `party.profile.CR_CITY`).
- **Service version** `SERVICE/N` — the definition: levels (SLA working days), steps (`FORM`, `LEVEL`,
  `FEES`, `REVIEW`, `DECLARATION`), sections shown by JSONLogic over the answers, question uses
  (required, required-when, visible-when, read-only), eligibility and validation rules with bilingual
  messages, fees (fixed, formula or bracket table by any value, VAT included or added). Numbers are
  consecutive; publishing stores the version with **snapshots of its questions**, and afterwards only
  its names and the *retired* flag change.
- **Rules** see `data` (answers by question code), `level`, `today`, `party` (the beneficiary's
  identifiers, profile, sanctions in force, credentials) and `applicant` (relationship of the person
  filling in); visibility rules see the answers only, exactly as in the wizard.
- **Shared questions** are asked once: where a question first shows (across sections and steps); the
  wizard and the server apply the same rule.
- **Eligibility** — built in: who may apply, delegation scope (a delegate's services), sanctions that
  block applications; then the version's rules. Checked when listing, starting and submitting.
- **Server-side validation** repeats the wizard's rules and adds types, choices, LOV grants, limits,
  patterns and cross-field rules; hidden and unknown answers are dropped, read-only prefilled answers are
  taken from the party's data.
- **Category** (1g) `svc.category` — names, description, icon and order; a service names an existing category,
  which groups it in the portal (home tabs, search filter, header menu).
- **Service card** (1g) — `definition.card` of a version: target audience, numbered steps, terms of use,
  required documents (with notes), channels (`PORTAL`, `MOBILE_APP`, `BRANCH`, `CALL_CENTER`), FAQ link, phone and
  email. Duration comes from the levels and cost from the fees (fixed amounts and bracket ranges; *depends on the
  request* for formulas and conditional fees), so the card never repeats them.

---

## 3.3 Request

A **Request** is one application instance.

| Attribute | Notes |
|---|---|
| `request_number` | Human-readable, configurable pattern |
| `service_version_id` | Pinned version |
| `service_level` | Chosen tier, if any |
| `submitter_party_id`, `beneficiary_party_id` | Individual and acting party |
| `status_category` | Fixed system category (below) |
| `display_status_code` | Configurable display status (below) |
| `data` | JSONB payload of the form (answers by question code); frequently queried fields promoted to columns |
| `region_code`, `amanah_code`, `org_unit_id` | Routing & reporting dimensions |
| `parent_request_id`, `relation_type` | Sub-request structure |
| `document_id` | Issued Document produced or acted on |
| `submitted_at`, `closed_at`, `sla_due_at`, `sla_paused_ms` | SLA tracking |

Owned collections: **timeline** (every transition, action, comment, notification),
**attachments** (scanned, typed by a document class), **payments** (fee items, bills, status),
**visits** (§3.6), **decision results** (with explanation trace).

### Statuses — two levels

**Level 1 — status categories (fixed, used by engine, SLA and reports):**

`DRAFT` → `SUBMITTED` → `IN_PROGRESS` ⇄ `PENDING_CUSTOMER` ⇄ `PENDING_EXTERNAL` →
`APPROVED` | `REJECTED` | `CANCELLED` → `CLOSED`

**Level 2 — display statuses (configurable, what users see).** Each maps to exactly one
category and is typically generated from step + action + actor:

| Display status (example) | Category |
|---|---|
| Draft / Cancelled draft | DRAFT / CANCELLED |
| {Department} – Under review | IN_PROGRESS |
| Initial approval / Initial rejection | IN_PROGRESS |
| Correction required | PENDING_CUSTOMER |
| Corrected | IN_PROGRESS |
| Pending – Payment | PENDING_CUSTOMER |
| Pending – Inspection / Visit output | PENDING_EXTERNAL / IN_PROGRESS |
| Pending – {Third party} | PENDING_EXTERNAL |
| {Role/Department} – Approval / Rejection | IN_PROGRESS |
| Approved / Rejected | APPROVED / REJECTED |
| Cancelled by request manager / Cancelled – returned to beneficiary | CANCELLED |

SLA clocks can pause on `PENDING_CUSTOMER` (configurable per service).

### Sub-requests

| Relation | Behaviour |
|---|---|
| **Prerequisite** | Child must reach an approved outcome before the parent proceeds; its output feeds the parent |
| **Bundled** | Submitted together, processed in parallel; parent completes per rule (all / any) |
| **Spawned** | Created by the workflow during processing (e.g. a visit or an inter-department request) |

---

## 3.4 Action Center

The employee workspace over workflow tasks:

- **Inboxes / queues** by role, org unit, position, or personal; filtered by data scopes.
- **Distribution** — a manager assigns requests to analysts (manual), or automatic
  (round-robin / least-loaded), or claim from a shared queue.
- **Task view** — request data, attachments viewer, decision explanation, timeline, internal
  notes, integration results.
- **Actions** from the configured action catalog (§3.5).
- Delegation / acting (out-of-office) from Apps Control.

**Implementation (1d)** — module `Workflow`, internal API `/api/workflow/v1`, menu group *Action Center*.
Inboxes: **mine** (assigned to me or to someone I stand in for), **queue** (unassigned tasks I may claim),
**to distribute** (tasks waiting for me to assign them) and **escalated** (overdue tasks escalated to my role),
with counts and search. The task page shows the request's answers, the deadline, who has the task, the actions
the employee may take now (claim, release, assign or reassign among the candidates with their open-task count,
the node's actions with a comment, required for rejections and returns), internal notes, the steps and the
timeline. *Supervision* (`action-center.supervision`) lists every open task with deadline and unassigned filters;
supervisors with *edit* reassign. Delegations (delegate, dates, reason) are kept in the Workflow module rather
than Apps Control: the delegate sees the delegator's assigned tasks and acts *on behalf of* them.

---

## 3.5 Workflow

Workflows are configured in the Workflow Builder and executed by a **custom, PostgreSQL-backed
engine** (see [ADR-0003](adr/0003-custom-workflow-engine.md)). Workflow Versions are immutable
once published; instances stay on their version.

### Node types

| Node | Purpose |
|---|---|
| **Human task** | Assigned by routing target; offers configured actions |
| **Parallel split / join** | Concurrent branches (e.g. governance, projects, financial reviews); join = all / any / N |
| **Exclusive gateway** | Route by JSONLogic condition |
| **Integration** | Inquire or push via an Integration Manager operation |
| **Decision** | Invoke the Decisions module |
| **Fee / payment wait** | Create bill, wait for payment confirmation |
| **Visit** | Schedule and wait for visit outcome (§3.6) |
| **Sub-request** | Create children and/or wait for them |
| **Timer / SLA** | Wait, escalate, remind, auto-action on expiry |
| **Document** | Issue / amend / suspend / cancel an Issued Document |
| **Notification** | Send templated messages |
| **Script** (advanced) | Sandboxed script, privileged |

### Action catalog (configurable per human task)

Approve · Reject · Return for correction · Forward · Assign / Reassign / Claim · Distribute ·
Request third-party inquiry · Schedule visit · Request payment · Put on hold · Escalate ·
Add internal note · Contact customer.

Each action configures: required comment/attachment, editable & required fields, resulting
display status, notifications, and next route. **Maker-checker** inside a branch (analyst →
auditor) is a standard pattern.

### Routing targets

Role · org unit · position within a unit · specific employee · relative targets (applicant's
region branch, parent unit's manager, same unit as previous step, request manager's pick).
Data scopes apply to every target.

### SLA

Per workflow, per step and per service level; computed on the Saudi working calendar;
early-warning thresholds; escalation chains; breach reporting.

**Implementation (1d)** — schema `wf`; configured through change sets (area `wf.workflows`: `wf.workflow`,
`wf.workflow-version` `CODE/N`, immutable once published). Node types so far: `START`, `HUMAN_TASK`,
`PARALLEL_SPLIT`, `JOIN` (`ALL` / `ANY` / `COUNT`), `GATEWAY` (JSONLogic routes over the answers, the level and
the last action of each step, then a default) and `END` (`APPROVED`, `REJECTED`, `CLOSED`). A human task has an
assignment (`ROLE` with optional org unit and position, `USER`, or `PREVIOUS_ACTOR` of a step falling back to a
role; mode `QUEUE`, `DISTRIBUTE` with a distributor role, or `AUTO` least-loaded), `excludeActorsOf` for
maker-checker, actions (`COMPLETE`, `APPROVE`, `REJECT`, `RETURN`; comment required or shown to the applicant;
resulting display status; `RETURN` may resume at another step), an optional display status and an SLA
(`workingDays`, `byLevel`, `warnPercent`, `escalateToRole`). The overall deadline is the service level's target
(else the workflow's `slaWorkingDays`); it stops while the request waits for the applicant. Working days come
from the installation's weekend days and the holidays (`apps.holiday`). The worker's SLA job runs every minute.
How the engine runs is recorded in [ADR-0019](adr/0019-workflow-runtime.md).

**Service-owned workflows (1f)** — every workflow belongs to one service (`serviceCode`, unique, fixed once set).
Publishing a new service creates its workflow with version 1 *Start → Completed (approved)*, which needs no review;
review steps come as later versions. A service version cannot be published unless its service's workflow has a
published version.

---

## 3.6 Visits

Usable as an archetype (Visit Request) or as a workflow step.

| Mode | Description |
|---|---|
| **Internal field app** | Scheduling, inspector assignment, configurable outcome form, photos, GPS, signature; offline PWA with sync |
| **External system** | Create the inspection in another system (e.g. CIS "Create Inspection Type"), receive the result by callback or polling |
| **Manual entry** | Outcome entered in the internal system |

---

## 3.7 Decisions (calculation / classification)

Decision logic (e.g. classification grading) is provided by a **pluggable decision engine**.
The platform defines only the contract:

- **Decision step** in a service or workflow, referencing a **rule set code**.
- **Input mapping** — from request data, party profile, credentials, integration results.
- **Output mapping** — results (e.g. grade per sector) written back to the request.
- **Explanation trace** — stored with the result for reviewers, appeals and certificates.
- **Versioning** — rule-set configuration is published like any configuration; the request
  records the version used.
- **Stateless invocation** — so the same engine powers a public calculator.

**Implemented (1e):** the contract in the kernel (`IDecisionService`, `IDecisionEngine`), rule sets with immutable
versions, the built-in `RULES` engine (JSONLogic steps, per-item results, trace), an evaluation log and the workflow
DECISION step (results kept as `decisions.<name>` in the instance). A specialised classification engine can be
registered beside it.

---

## 3.8 Issued Document Registry

| Concept | Description |
|---|---|
| **Document type** | Certificate / license / permit / approval…; numbering rule, validity rule, template |
| **Issued document** | Number, holder party, type, status, issue/expiry dates, current version |
| **Version** | Immutable snapshot of document data; created on issuance and each amendment |
| **Line items** | Repeating entries within one document — e.g. one certificate with **several sectors, each with its own grade** |
| **Statuses** | Active · Expired · Suspended · Revoked/Cancelled · Superseded |
| **Status actions** | Suspend, release, downgrade, cancel — from workflows or sanction processes |
| **Verification** | QR code → public verify endpoint on the external system |
| **Rendering** | HTML template (Liquid) → PDF via Gotenberg; bilingual, RTL |

**Amendment numbering** is configurable per document type: same number + new version
(default) or new number; validity reset or kept.

**Document-date alerts** (Worker, scheduled): nearing expiry, renewal available, extension
available, expired — feeding notifications and availability cards.

---

## 3.9 Communication

- **Channels**: SMS (CST-registered sender), email, push, in-app.
- **Templates**: Liquid, bilingual, per channel; variables from request/document/party.
- **Rules**: on event (status change, action, document event) or schedule (document dates,
  draft expiry, SLA warning); conditions in JSONLogic.
- **Outbound log** with delivery status; retries via outbox.

**Implemented (1e):** event types registered by modules (requests, documents, tasks) with sample facts; templates
with Arabic and English wording per channel; rules with a JSONLogic condition and recipients (party, users, roles);
in-app, email (SMTP) and SMS (an Integration Manager operation); the message log with resend. Party addresses come
from the `EMAIL` / `MOBILE` profile fields.

---

## 3.10 Reporting

- **Datasets** built on generated views: requests (with promoted fields), tasks, SLA,
  documents, payments, parties, lookups (`v_class_*` typed views).
- **Dimensions** commonly needed: service, service level/track, status, region, amanah,
  sector, grade, org unit, month.
- **Dashboards**: KPI tiles, charts (ECharts), SLA compliance, workload & aging, funnel /
  **draft abandonment** (where applicants stop).
- **Data scopes apply** — a report never shows what the user's grants hide.
- Pixel-perfect report designer: later phase.

---

## 3.11 Configuration lifecycle

| Kind | Mechanism |
|---|---|
| **Versioned configuration** — services, forms, workflows, templates, rule sets | Draft → Published (immutable) → Retired; instances pinned to version |
| **Simple configuration** — Class/Domain, LOVs, settings, menus, roles, content | **Change sets**: draft → submitted → approved → published; maker-checker policy per configuration area |
| **Packages** | Export/import between environments by `code`; preview diff before import |
| **Ownership** | `SYSTEM` (product-owned, updated by upgrades) vs `ENTITY` (customer-owned, never overwritten) |

All changes are audited.

---

## 3.12 Portal Content Manager

External-portal content managed without development: portal title/description/logo,
sections (show/hide/order), announcements & circulars (type, priority, start/end dates,
attachment), guides & regulations, service display (show/hide, rename, order, featured),
sectors display. Roles: portal admin, content editor, content approver, viewer. Uses change
sets (create → review → approve → publish), preview, drafts and change log.

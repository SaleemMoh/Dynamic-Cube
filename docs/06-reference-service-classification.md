# 6. Reference Service — City Service Providers Classification Certificate

The classification certificate (شهادة تصنيف مقدمي خدمات المدن) of the Ministry of
Municipalities and Housing is the reference service for the first vertical slice. It exercises
almost every platform capability: party profile and delegation, many integrations, composable
multi-sector forms, a decision engine, parallel maker-checker reviews, service levels with
fees, a multi-line issued document, document-driven follow-up services, and sanctions.

Sources: *Classification guide for contractors and engineering consultancy companies/offices*
(April 2026, public), the unified-portal proposal, journey and performance analyses, and the
DGA vendor technical certificate sample.

## 6.1 Who and what

- **Applicants**: establishments (contractors, engineering consultancy offices/companies,
  other city service providers), acting through an owner or authorized delegate.
- **Classification** = assessment of the establishment's financial, technical, administrative
  and execution capabilities to place it in a **sector (field), activity and grade**.
- **Nine sectors**: Construction & Building · ICT · Operation, Maintenance & Services · Real
  Estate Development · Catering & Subsistence · Exhibitions & Conferences · Surveying &
  Geospatial · Cybersecurity · Engineering Consultancy Offices & Companies.
- **Grades**: 1 (highest) to 5, or *unclassified*.

## 6.2 Service catalog → archetypes

| Service | Archetype | Notes |
|---|---|---|
| Classification calculator | — (stateless decision) | Public estimate before applying |
| Pre-qualification / readiness check | — (eligibility) | Prerequisites, sanctions |
| New classification | Issuance | One or more sectors in one request |
| Renewal | Amendment | Same grade & sectors, new validity |
| Extension | Amendment | Extends validity |
| Add / remove sector, modify / upgrade grade | Amendment | Changes line items |
| Update certificate data | Amendment | Legal data changes (name, ownership, CR…) |
| Appeal | Amendment-style | Against an issued result |
| Project withdrawal | Amendment-style | Affects performance record |
| Instant accreditation | Issuance (fast path) | ⚑ Open: confirm scope |
| Classification consultant ("وكّل مستشارك") | Advisory service | Paid; offered after a result |

The external system presents these as one journey: **Service identifier** → *My requests*,
*My certificates*, *New request*; each certificate page shows **available / locked** services.

## 6.3 Prerequisites (eligibility)

1. Valid CR (contractors) or valid engineering license (consultancy), covering the activities
   subject to classification.
2. Audited financial statements uploaded to Qawaem for the last fiscal year, between 6 and 18
   months from the CR registration date, certified by a licensed accountant.
3. Collective labor housing license (or licensed housing lease) for Construction, O&M and Real
   Estate Development.
4. Valid commercial license.
5. No active sanction banning applications (§6.9).

## 6.4 Request composition

- Applicant selects one or more **sectors**; one is marked **primary**.
- Form sections are composed from the selected sectors; **shared questions** (governance,
  financial) are answered **once** (e.g. "35 questions · 15 shared · 57 answered").
- Data auto-filled from integrations wherever possible (CR, owners, employees, Saudization,
  qualifications, financial statements, projects, DGA/NCA results); manual entry only where
  no source exists.
- The request is bound to the **latest published guide version** (rule-set version) and stays
  bound to it.
- Steps: establishment → service level → sectors → profile & staff → projects → governance →
  financial → declaration & pledge → fees → submit.

## 6.5 Evaluation (decision engine inputs and outputs)

The platform invokes the decision engine (§3.7) per selected sector. Summary of the guide's
logic for reference; the engine implementation is supplied separately.

### Size classes (employees)

Micro 1–5 · Small 6–49 · Medium 50–499 · Large 500+.

### Axis 1 — Technical evaluation

| Sector | Assessed by | Criteria (points) |
|---|---|---|
| Construction & Building | MoMRAH with Saudi Contractors Authority | Core 80: engineers % (30), technicians % (30), engineers' experience (10), technicians' experience (10). Additional 20: Saudis %, Saudi engineers %, high-wage Saudis %, Saudi women % (5 each) |
| O&M & Services | MoMRAH with SCA | Core 70: value-added jobs % (60), their experience (10). Additional 30: Saudi technicians (10), Saudis, Saudi engineers, high-wage Saudis, Saudi women (5 each) |
| Real Estate Development | MoMRAH | Business risk (62.5) + governance & management (37.5) |
| Catering & Subsistence | MoMRAH | Mandatory 70: chefs % (35), chefs' experience (35). Incentive 30: Saudis (7.5), Saudi women (7.5), Saudi chefs, Saudi chefs' experience, high-wage chefs (5 each). Separate tables for accredited vs non-accredited chefs |
| Exhibitions & Conferences | MoMRAH with Saudi Exhibitions Authority | Core 80: specialists %, specialists' experience, technicians %, technicians' experience (20 each). Additional 20: Saudis, Saudi specialists, Saudi women, high-wage Saudis (5 each) |
| Surveying & Geospatial | GEOSA | Core 80: engineers % (30), technicians % (10), engineers' experience (30), technicians' experience (10). Additional 20: Saudis (5), Saudi engineers (2.5), Saudi engineers' experience (2.5), Saudi women (5), high-wage Saudis (5) |
| Engineering Consultancy | Saudi Council of Engineers | Same weights as Surveying |
| ICT | **DGA** (integration) | Categories Platinum / Gold / Silver / Beginner by value-chain qualification: RDI ≥70%, IIS ≥60%, SS ≥50%, S&D ≥40%; overall-score grade minimums 79 / 69 / 59 / 49 / 40 |
| Cybersecurity | **NCA** (integration) | Experience & business, workforce, innovation & IP axes |

Criterion points come from **band tables per size class** (value range → points; bands are not
always monotonic, some cells not applicable). Technical grade by points: ≥70 → 1, 50–<70 → 2,
40–<50 → 3, 30–<40 → 4, 20–<30 → 5, <20 → unclassified.

For ICT the technical result is read only from the DGA integration; the DGA certificate
(level → category → activity → value chain, 1-year validity) is stored as an external
credential on the party.

### Axis 2 — Performance record (projects, last 10 years, SAR million)

| Sector | Cumulative value G1 / G2 / G3 / G4 / G5 | Largest project G1 / G2 / G3 / G4 / G5 |
|---|---|---|
| Construction & Building | ≥160 / 90 / 50 / 30 / <30 | 70 / 22 / 14 / 8 / 0 |
| ICT | ≥100 / 70 / 50 / 30 / <30 | 35 / 22 / 14 / 8 / 0 |
| O&M & Services | ≥160 / 90 / 50 / 30 / <30 | 70 / 22 / 14 / 8 / 0 |
| Real Estate Development | ≥160 / 90 / 50 / 30 / <30 | 70 / 22 / 14 / 8 / 0 |
| Catering & Subsistence | ≥60 / 40 / 30 / 20 / <20 | 35 / 22 / 14 / 8 / 0 |
| Exhibitions & Conferences | ≥60 / 40 / 30 / 20 / <20 | 35 / 22 / 14 / 8 / 0 |
| Surveying & Geospatial | ≥15 / 10 / 5 / 2.5 / <2.5 | 6 / 4 / 2 / 1 / 0 |
| Cybersecurity | ≥30 / 20 / 15 / 10 / <10 | 15 / 10 / 5 / 2.5 / 0 |
| Engineering Consultancy | ≥40 / 30 / 20 / 10 / <10 | 10 / 8 / 5 / 2.5 / 0 |

If the cumulative value is not met, the grade is determined by the largest completed project
(the largest-project grade **replaces** the cumulative grade).

Project counting rules: contracts within 10 years of application; completion ≥50% for
execution and ≥30% for maintenance/operation projects; projects abroad count when ≥10M SAR,
within 7 years, with audited revenue statements; values in SAR only; projects >600M SAR need a
split request. Evidence: contract (number, owner, dates, duration, value, completion %, role:
main / joint / subcontractor), BOQ, completion certificate, subcontract approval, revenue
statement.

### Axis 3 — Governance

Yes/No questionnaire with points and required attachment when "yes" (board of directors 10,
board meetings 5, ERP 10, project management system 15, board committees 5, committee meetings
5, organization structure 10, social contribution 10, ISO / safety certificate 10, local content
certificate 10, risk management 10). Total 100.

| Grade | General | Engineering Consultancy |
|---|---|---|
| 1 | 85–100 | 50–100 |
| 2 | 75–84 | 40–49 |
| 3 | 60–74 | 30–39 |
| 4 | 50–59 | 20–29 |
| 5 | <50 | 0–19 |

### Axis 4 — Credit rating (financial grade)

External rating AAA … D (financial position, income, cash flows, governance, sector and
business risk).

| Financial grade | General | Engineering Consultancy |
|---|---|---|
| 1 | BBB+ … AAA | BBB- … AAA |
| 2 | BBB- … BBB | BB … BB+ |
| 3 | BB- … BB+ | B+ … BB- |
| 4 | B … B+ | B- … B |
| 5 | CCC+ … B- | CCC- … CCC+ |
| Insufficient | D … CCC | D … CC |

C or D ⇒ certificate cannot be issued.

### Final grade

- Non-financial grade = **lowest** of technical, performance and governance grades.
- Final grade = **matrix**(financial grade × non-financial grade) — currently equivalent to
  the worse of the two; kept as a matrix so policy can change.

### Adjusters (after calculation)

- Withdrawn government projects: 2+ from different government entities within 3 years ⇒
  downgrade one grade in **all** sectors.
- Municipal violations: 70+ in the same sector within 3 years ⇒ downgrade one grade in **that**
  sector.

## 6.6 Service levels and fees

| Level | Duration | Fee by annual revenue (<10M / 10–50M / 50–200M / ≥200M SAR) |
|---|---|---|
| Regular | 55–60 working days | Free |
| Premium | 25–35 working days | 1,000 / 5,000 / 15,000 / 30,000 |
| Diamond (new) | 5–10 working days | 60,000 / 70,000 / 90,000 / 120,000 |

VAT included. Chosen **before** the request starts, with a fee and duration estimate.
SLA monitoring with early warning is essential (the current fast track breaches its SLA in
most cases).

## 6.7 Processing workflow

```
Submit
  └─▶ Request manager: review & distribute
        ├─▶ Governance analyst ──▶ Governance auditor ─────┐
        ├─▶ Projects & experience analyst ──▶ Projects auditor ─┤  (parallel, maker-checker)
        └─▶ Financial analyst ──▶ Audit manager ─────────────┘
                                                          join
  └─▶ General manager (classification) approval
  └─▶ Issue / amend certificate ─▶ Notify
```

Fast track adds: invoice & payment step; requests for additional data go through the
platform's **return for correction** (not email).

Every reject / return requires a reason visible to the applicant.

## 6.8 Certificate

- **One certificate per establishment**, containing **several sectors**, each with its grade
  (document line items).
- Issue date, expiry date (validity configurable — sample shows 2 years), QR verification.
- Amendments keep the certificate number and create a new version.

### Availability of follow-up services

| Service | Window |
|---|---|
| Appeal | Within 60 days of issue |
| Modify / upgrade grade | Opens 90 days after issue |
| Renewal | Within the 90 days before expiry |
| Extension | From 15 days before expiry until 90 days after expiry |
| After the extension window | New request only |

## 6.9 Sanctions (violations table, summary)

Progressive penalties by offence count, different for classified and unclassified applicants:

- False / forged financial statements or project data → suspend request or certificate, ban
  from applying/renewing (6 months → 1 year → 2 years → 3 years / cancellation).
- Not updating legal data after issuance → warning (30 days) then suspension until updated.
- Not cooperating with inspectors → suspension until the inspection visit is completed.
- Incorrect technical staff data → warning (90 days), downgrade to actual grade, suspension
  and ban on renewal/modification for 1–3 years, cancellation.
- Undisclosed subcontracts, undisclosed withdrawn projects, incorrect labor-housing data
  (20+ workers) → downgrade, suspension, bans, cancellation.
- Serious violations by official notice → suspension / downgrade / cancellation by severity.
- Forged certificate → cancellation.

Cancellation may be reconsidered by committee after at least three years. Platform needs:
sanction records on the party, document status actions, eligibility checks against bans.

## 6.10 Notifications

Request rejected · documents required (returned) · request completed · certificate nearing
expiry · renewal available · extension available · certificate expired · offer of advisory
service after a result.

## 6.11 KPIs (dashboards)

Requests by status / service / track / month; SLA compliance per track; average processing
days; certificates by region, sector and grade; draft abandonment; complaints by type, source
and amanah.

## 6.12 Open items

- ⚑ Decision engine implementation (to be supplied) and its configuration format.
- ⚑ Instant accreditation scope.
- ⚑ Certificate validity per sector / service level.
- ⚑ Consultant service: internal staff or external offices; payment and assignment model.

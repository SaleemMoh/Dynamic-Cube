# Demo data

A fresh installation loads one demo set, so every screen shows realistic data. The Migrator loads it once, after the
migrations, into a database that does not have it yet (`DemoData:Name`; Docker: `DEMO_DATA`, default `nhc`; `none` for
an empty system). Each file is an ordinary configuration package: it goes through change sets (drafted by
`demo-loader`, approved by `demo-checker`), so the usual checks apply and the change sets show in Apps Control.

## `nhc` — NHC Innovation, contractors and engineering offices classification

Built from the customer journey study (V05), the classification rules workbook (NHC_1) and the application fields
(12 Nov 2025).

| File | What it holds | Where to see it |
|---|---|---|
| 01-lists | 13 administrative regions | Apps Control → Classes |
| 02-organization | NHC Innovation: general manager office, classification sector (Riyadh, Jeddah, Dammam requests; governance, projects and financial evaluation; audit), customer experience, IT; 7 positions | Apps Control → Organization structure |
| 03-organization-companies-branches | Organization *National Housing Company (NHC)*, company *NHC Innovation*, branches Riyadh (default), Jeddah, Dammam, each served by its requests office; the app *Classification platform* (internal system and portal) that owns the services, certificate, rules and roles; calendar and 2027 holidays | Apps Control → Installation & organization |
| 04-integrations | Ministry of Commerce (CRs of a person, CR details) and SADAD through the gateway | Integration Manager |
| 05-parties | Establishments at sign-in from the CR registry; CR status, city, expiry, capital, activities; email and mobile | Party registry |
| 06-certificate-type | One certificate per establishment with a line per domain and grade (36 months) | Documents → Types |
| 07-services | Category and 3 services: **new**, **renew**, **change** (upgrade, add or remove a domain); 54 questions from the fields file (licences, 9 domains, collective housing, staff, projects, governance G1–G11 with proofs, financial and credit, undertakings) | Services, portal |
| 08-people | 9 roles and 14 staff (below) | Apps Control → Roles, Users |
| 09-grading-rules | The guide's rules: size class, technical, performance (10-year cumulative and largest project per domain group), governance (100 points), financial (credit rating cut-offs), worst axis, one grade lower for withdrawn government projects or 70+ violations | Decisions → Rule sets |
| 10-payments | SADAD bills of the fast track | Payments |
| 11-workflows | New and change: branch manager review → *fast track: invoice and payment* → three parallel analyst/auditor evaluations → grading → general manager → certificate. Renew: branch manager review → renewal | Workflows |
| 12-notifications | Returned, rejected, approved, certificate issued, renewal available, expiry, task assigned, overdue | Communication |
| 13-portal | Portal home, *About classification* and 10 FAQs answering the complaint themes of the study | Portal |

**Tracks:** *normal* — free, up to 60 working days; *fast (NHC)* — paid, 5 working days. The fast-track fee
(5,000 / 10,000 / 20,000 / 30,000 SAR by annual revenue) is an assumption; change it in the service's Fees tab.
Violations come from Eyfaa, which is not connected yet, so the demotion for violations does not apply until it is.

### Sign-in (password `Dev@12345`)

| Who | User name | Role |
|---|---|---|
| Branch requests managers | `ruh.manager`, `jed.manager`, `dmm.manager` | Review, assign analysts, follow tasks |
| Governance | `gov.analyst1`, `gov.analyst2`, `gov.auditor` | Analysis, audit |
| Projects & experience | `prj.analyst1`, `prj.analyst2`, `prj.auditor` | Analysis, audit |
| Financial | `fin.analyst1`, `fin.analyst2`, `audit.manager` | Analysis, audit manager review |
| General manager | `nhc.gm` | Final approval |
| Customer experience | `care.agent` | Views requests and certificates |
| Configuration | `admin`, `maker`, `checker` | Apps Control |
| Applicants (portal) | `1000000001`, `2000000002` | Act for the establishments the CR registry (mock gateway) reports |

Staff are fictional and named by their job.

### Start again with the demo

```bash
# Docker: removes every container and the database volume, then starts and loads the demo
docker compose -f deploy/docker/docker-compose.yml down -v
docker compose -f deploy/docker/docker-compose.yml up -d --build
```

Local development: drop and create the `dynamic_cube` database, then run the Migrator with the demo:
`DemoData__Name=nhc dotnet run --project src/Tools/Migrator`. Keycloak imports its realm only when it starts without
it; local Keycloak installs keep their users, so add the staff above in the admin console if they are missing.

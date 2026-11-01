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
| 08-people | 9 roles, default roles of five departments and 14 staff (below) | Apps Control → Roles, Org units, Users (Access) |
| 09-grading-rules | The guide's rules: size class, technical, performance (10-year cumulative and largest project per domain group), governance (100 points), financial (credit rating cut-offs), worst axis, one grade lower for withdrawn government projects or 70+ violations | Decisions → Rule sets |
| 10-payments | SADAD bills of the fast track | Payments |
| 11-workflows | New and change: branch manager review → *fast track: invoice and payment* → three parallel analyst/auditor evaluations → grading → general manager → certificate. Renew: branch manager review → renewal | Workflows |
| 12-notifications | Returned, rejected, approved, certificate issued, renewal available, expiry, task assigned, overdue | Communication |
| 13-portal | Portal home, *About classification* and 10 FAQs answering the complaint themes of the study | Portal |
| 14-individual-service | **Demo service for individuals** — *Professional qualification for housing-sector practitioners* (below): NIC and SCE through the gateway, the regions as a list of values, the individual's ID status in the party registry, the qualification card, 26 questions, the workflow and its notifications | Services, portal, Workflows, Documents |
| 15-query-lists | A list built with the visual query builder: *My branches by region* (registered table `BRANCHES` joined to the regions class; each user sees their company's branches they work in, with the region as parent) | Apps Control → Lists of values |
| 16-connected-app | **Connected app** *Projects management system* (`PMS`): its sign-in client `dc-app-pms`, 4 screens with their actions, a menu tree of two groups, roles *Projects manager* (Fahad Al-Otaibi, Riyadh branch) and *Projects viewer* (the audit manager, all branches), and *My branches by region* shared with it. The sample app at http://localhost:5090/connected-app signs in as any of them and shows their menu, screens and the list with their grants | Apps Control → Installation & entity → Apps (Screens and actions, Sign-in and credentials); Menus; Roles |
| 17-classification-panel | **Side panel** (ADR-0040) on *version 2* of the new-classification service: *وكيل التصنيف* with a greeting and notes per step (with their rule under *Why?*) and five questions it answers, the readiness check, the request summary (with the CR number, domains and employees) and the live result — the grading step's best grade on the 1–5 scale (*غير مصنف* when none) with the size, the points and a card per domain showing its technical, performance, governance and financial grades | Portal → new classification request (side column); Services → Service Builder → *Side panel* tab |
| 18-public-api | **Public API** (ADR-0042) for the sample partner *Partner demo* (`PARTNER_DEMO`, sign-in client `dc-api-partner-demo`, company NHC Innovation, 60 calls a minute): `/regions` reads the regions class (optional `name` filter), `/requests/{number}/status` a request's status and its place in the workflow, `/certificates/{number}` checks a classification certificate | Integrations → API endpoints, API clients, API calls |
| 19-public-api-lookups | `/lookups` for the partner demo: several classes and public lists of values in one call (`?classes=REGION,SVC_CATEGORY&lovs=REGIONS`) | Integrations → API endpoints |
| 20-individual-nafath | *Version 2* of the professional qualification service asks the applicant to **confirm the request with Nafath** before it is submitted (ADR-0043): on submit the portal shows a number; pick it on the mock Nafath app page (below) | Portal → the individual service; Services → Service Builder → *Rules* → *Submitting* |

**Tracks:** *normal* — free, up to 60 working days; *fast (NHC)* — paid, 5 working days. The fast-track fee
(5,000 / 10,000 / 20,000 / 30,000 SAR by annual revenue) is an assumption; change it in the service's Fees tab.
Violations come from Eyfaa, which is not connected yet, so the demotion for violations does not apply until it is.

### Demo service for individuals: professional qualification

Shows every part of a service in one place. Sign in to the portal as `1000000001`, choose **yourself** (not an
establishment) and open *Services for individuals → Professional qualification for housing-sector practitioners*.

| Part | In this service |
|---|---|
| Question types | Text, multi-line text, number, date, single and multiple choice, choice from a list of values (regions), checkbox, single and multiple file uploads |
| Where answers come from | ID number from the beneficiary (locked); full name and date of birth from the National Information Center (locked); nationality from the identity check step (editable); the rest typed by the applicant |
| Question bank | Mobile, email, region and address are shared questions; the others belong to this service only |
| Integration step | *Identity check* calls the NIC on arrival and shows the result card (name, date of birth, nationality, ID status and expiry) |
| Paths in the form | Track: *standard* (free) or *express* (SAR 500 with VAT). The engineer, technician and craftsman sections appear for their profession, each with its own required questions; the graduation date is hidden for high school; the housing projects description appears when its box is ticked. The *Experience* step uses tabs |
| Checks | Eligibility: a national ID that has not expired at the NIC. Validations: an engineer needs at least a bachelor's degree; a craftsman must have passed the skills test |
| Branch | The region answer sends the request to that region's branch (Makkah → Jeddah, Eastern → Dammam, others → Riyadh head office) |
| Workflow | Branch review → *express?* SADAD bill and payment → *engineer?* SCE membership checked through the gateway (on failure the branch checks it by hand) → professional evaluation → *10 years or more?* general manager approval → card issued |
| Result | *Housing-sector professional qualification card* (`QLF-…`, 24 months, one per person; applying again adds a version) with profession, specialty, SCE grade, grade (practitioner under 5 years, professional, expert from 10), years and region |
| Notifications | Returned, rejected and card issued (to the applicant); task assigned (to the employee) |

Who works on it: the branch requests manager of the request's branch (`ruh.manager`, `jed.manager`, `dmm.manager`),
the projects analysts (`prj.analyst1`, `prj.analyst2`) and the general manager (`nhc.gm`). SCE membership numbers in
the mock gateway: `123456` and `654321` (active), `111111` (expired: the automatic check fails and the branch checks it
by hand); any other number is not found. NIC data exists for `1000000001` (and `1000000003`).

### Nafath (mock)

The mock gateway stands in for Nafath (ADR-0043); nothing reaches Elm.

- **Sign in with Nafath:** on the portal's sign-in page choose *نفاذ (Nafath)*. The mock Nafath page
  (http://localhost:5090/nafath/portal) asks for a national ID — type `1000000001` (or any ID; names come from the NIC
  sample when known) and approve: you are back in the portal, signed in as that person (an existing account with that
  user name is linked; otherwise one is created).
- **Confirm a request with Nafath:** start *Professional qualification for housing-sector practitioners* as
  `1000000001` and submit it. When everything else is in order the portal shows a number; open
  http://localhost:5090/nafath/app?id=1000000001 (the mock Nafath app), pick the same number and the request is
  submitted. Picking another number or *reject* rejects it; *Send a new request* tries again. A draft started on
  version 1 before this file was loaded does not ask.

### Sign-in (password `Dev@12345`)

| Who | Name — user name | Role |
|---|---|---|
| Branch requests managers | Fahad Al-Otaibi — `ruh.manager`, Reem Al-Ghamdi — `jed.manager`, Khalid Al-Dosari — `dmm.manager` | Review, assign analysts, follow tasks |
| Governance | Noura Al-Qahtani — `gov.analyst1`, Abdullah Al-Shehri — `gov.analyst2`, Sarah Al-Zahrani — `gov.auditor` | Analysis, audit |
| Projects & experience | Majed Al-Harbi — `prj.analyst1`, Haifa Al-Mutairi — `prj.analyst2`, Turki Al-Subaie — `prj.auditor` | Analysis, audit |
| Financial | Lama Al-Anazi — `fin.analyst1`, Yousef Al-Shammari — `fin.analyst2`, Munira Al-Rasheed — `audit.manager` | Analysis, audit manager review |
| General manager | Abdulrahman Al-Salem — `nhc.gm` | Final approval |
| Customer experience | Amal Al-Juhani — `care.agent` | Views requests and certificates |
| Configuration | `admin`, `maker`, `checker` | Apps Control |
| Applicants (portal) | `1000000001`, `2000000002` | Act for the establishments the CR registry (mock gateway) reports |

Staff are fictional people with realistic names; their user names follow their job so they are easy to sign in with.
How they get their access (edit a user, *Access* tab):

- **By company and branch:** each branch requests manager holds *Branch requests manager* in company *NHC Innovation*,
  only in their own branch; auditors and the audit manager hold their roles in the company, in *All branches*.
- **From the department:** the governance, projects and financial evaluation departments, the general manager office
  and customer experience carry default roles, so their staff have no roles of their own.
- **Personal:** the audit manager can also open *Task supervision*.

### Calling the public API as the partner demo

The sample partner `dc-api-partner-demo` (development secret `dev-api-partner-secret`) gets a token with client
credentials and calls the endpoints given to it on the external API:

```bash
TOKEN=$(curl -s http://localhost:8080/realms/dynamic-cube-external/protocol/openid-connect/token \
  -d grant_type=client_credentials -d client_id=dc-api-partner-demo -d client_secret=dev-api-partner-secret \
  | sed -E 's/.*"access_token":"([^"]+)".*/\1/')
curl -s -H "Authorization: Bearer $TOKEN" "http://localhost:5081/api/public/v1/regions?name=mak"
curl -s -H "Authorization: Bearer $TOKEN" "http://localhost:5081/api/public/v1/lookups?classes=REGION,SVC_CATEGORY&lovs=REGIONS"
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5081/api/public/v1/requests/CLS-2026-000001/status
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5081/api/public/v1/openapi.json
```

Each call shows under **Integrations → API calls**. A request number that is not the partner's company's, or that does
not exist, answers 404; the same token on any other endpoint answers 403.

### Update without losing data

`docker compose -f deploy/docker/docker-compose.yml up -d --build` rebuilds and restarts; the database migrates on
start and keeps everything (branding, uploads, users, requests). The demo is loaded in full only into an empty
database; a demo file added later (such as `14-individual-service`) is loaded into an existing database on the next
start, and the files loaded before are not applied again, so changes made since are kept.

To refresh only the NHC staff and roles in a database that already has the demo (for example after the staff got
their names), import `deploy/demo-data/nhc/08-people.json` under **Apps Control → Change sets → Import package**, then
submit and approve it: its roles and users are `UPSERT`, so they are updated in place and nothing else changes.

### Start again with the demo

```bash
# Docker: removes every container AND the database volume (all data, including uploaded branding images),
# then starts and loads the demo
docker compose -f deploy/docker/docker-compose.yml down -v
docker compose -f deploy/docker/docker-compose.yml up -d --build
```

Use this only to wipe everything. Export what you want to keep first (e.g. the branding JSON from *Apps Control →
Branding*; uploaded images are kept in the database and are lost with the volume).

Local development: drop and create the `dynamic_cube` database, then run the Migrator with the demo:
`DemoData__Name=nhc dotnet run --project src/Tools/Migrator`. Keycloak imports its realm only when it starts without
it; local Keycloak installs keep their users, so add the staff above in the admin console if they are missing.

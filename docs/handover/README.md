# Technical handover

A walk through the code and tables of Dynamic Cube, one part at a time, for a .NET and Angular developer who is used to a
layered solution (Domain, Application, DataAccess, Repository, Business, WebAPI) and is new to a **modular monolith**.

| # | Part | Status |
|---|---|---|
| 0 | [How the solution is organized](#how-the-solution-is-organized) (this page) | ✅ |
| 1 | [Apps Control — Classes and domains (the data dictionary)](01-apps-control-data-dictionary.md) | ✅ (review points decided) |
| 2 | [Apps Control — Lists of values (LOVs)](02-apps-control-lists-of-values.md) | ✅ (review points open) |
| — | [Legacy Apps Control (Oracle) ↔ ours: table and column names](legacy-apps-control-mapping.md) | done (Apps Control renamed); legacy gaps open |
| 3 | Apps Control — Menus | next |
| … | Roles and privileges, organization, users, settings, then the other modules | later |

---

## How the solution is organized

### Layered solution vs. modular monolith in one picture

In your usual solution, each **layer** is a project and every feature is spread across all of them:

```
PRJ.Domain        ← all entities of all features
PRJ.Application   ← all DTOs, enums, mappers of all features
PRJ.DataAccess    ← one DbContext with every table
PRJ.Repository    ← all repositories
PRJ.Business      ← all managers
PRJ.WebAPI        ← all controllers
```

In Dynamic Cube, each **business area (module)** is a project and the layers are **folders inside it**:

```
src/Modules/AppsControl/      ← one project = one module (Apps Control)
    Domain/                   ← its entities              (= your PRJ.Domain, for this module only)
    Persistence/              ← its DbContext + migrations (= your PRJ.DataAccess, for this module only)
    Seeding/                  ← its seed data             (= the seed part of PRJ.DataAccess)
    Changes/                  ← DTOs ("payloads") + the "managers" that validate and save them
    DataDictionary/, Lovs/, Access/  ← read-side services ("managers" that answer questions)
    Endpoints/                ← its HTTP endpoints        (= your controllers)
    AppsControlModule.cs      ← registers all of the above in DI and maps the endpoints
src/Modules/Services/         ← another module, same inside shape
src/Modules/Workflow/         ← …
```

The rule that makes it a *modular* monolith: **a module never references another module's project**. When Services
needs something from Apps Control (e.g. the values of a class), it calls an **interface** declared in the shared kernel
(`src/Shared/Platform`), which Apps Control implements and registers in DI. Everything still runs in one process and one
database, so there is no network call between modules.

### Your projects → our folders

| Your project | What it holds | Where it is in Dynamic Cube |
|---|---|---|
| **PRJ.Domain** | Database models | `src/Modules/<Module>/Domain/*.cs`. Common base classes (e.g. `ConfigEntity`: id, code, Arabic/English name, active, owner, audit columns) in `src/Shared/Platform/Configuration` and `…/Persistence` |
| **PRJ.Application** | DTOs, enums, mappers, templates | **DTOs** = the *payload* classes next to their applier (`Changes/*Appliers.cs`, e.g. `ClassPayload`) and the `record` responses in `Endpoints/*.cs`. **Enums** live with the entity in `Domain/`. **Mappers**: no AutoMapper; each applier maps by hand in two methods: `ApplyAsync` (DTO → entity) and `ToPayloadAsync` (entity → DTO). **Templates**: `src/Shared/Platform/Templates/LiquidRenderer.cs` (Liquid templates for messages and documents). **DTOs shared between modules** ("contracts") in `src/Shared/Platform/<Area>/*Contracts.cs` |
| **PRJ.DataAccess** | DbContext and seed | `src/Modules/<Module>/Persistence/<Module>DbContext.cs` (one DbContext **per module**, one PostgreSQL **schema** per module) and `Persistence/Migrations/`; seed in `Seeding/<Module>Seeder.cs`; migrations and seeds are run by `src/Tools/Migrator` (not at API start-up) |
| **PRJ.Repository** | Repositories (interface + class) | **No repository layer.** The DbContext's `DbSet<T>` is used directly (EF Core is already a repository and unit of work). Reusable queries sit in the class that needs them (`Query()`/`FindAsync()` in appliers, services like `DataDictionaryReader`) |
| **PRJ.Business** | Managers that use the repositories | **Appliers** (`Changes/`) for every configuration write: validate a payload and copy it onto the entity. **Services** (`DataDictionary/`, `Lovs/`, `Access/`, `Runtime/`…) for reads and runtime logic. The generic save pipeline (`ChangeSetService`) is in the kernel |
| **PRJ.WebAPI** | Controllers and middleware | `src/Modules/<Module>/Endpoints/*.cs`: **Minimal API** endpoints (`group.MapGet(...)`) instead of controller classes. The three **hosts** `src/Hosts/Internal.Api`, `External.Api`, `Worker` hold `Program.cs` and the middleware (`src/Shared/Hosting`). Each host loads the modules it needs |
| **PRJ.GlobalComponents** | Shared helpers | `src/Shared/Platform` (the kernel: base entities, change sets, audit, outbox, JSONLogic evaluator, cross-module contracts) and `src/Shared/Hosting` (host set-up) |
| **PRJ-internal-app** | Angular for staff | `web/projects/internal` |
| (external app) | Angular for applicants | `web/projects/external` (the portal) |
| — | — | `web/projects/field` (offline app for inspectors) and `web/projects/shared` (a library used by all three: API clients, models, components) |
| — | Tests | `tests/<Module>.Tests` (xUnit + Shouldly, against a real PostgreSQL), `web/**/*.spec.ts` (Vitest) |

### The three hosts

| Host | Who calls it | What it maps |
|---|---|---|
| `Internal.Api` | The internal Angular app (staff) | Every module's admin and staff endpoints (Apps Control: `MapAppsControlInternal()`) |
| `External.Api` | The portal (applicants) and partner systems | Only the public/applicant endpoints (Apps Control: `MapAppsControlExternal()`) |
| `Worker` | Nobody (background) | No endpoints: outbox, SLA timers, scheduled reports. Uses the same module services |

All three call `services.AddAppsControl(...)`, `services.AddServices(...)`, … so the **same module code** runs in each.

### The one pattern you will see everywhere: configuration is saved through change sets

Your usual flow is `Controller.Create(dto) → Manager.Create(dto) → Repository.Add(entity) → SaveChanges`.

Dynamic Cube has **no per-entity create/update endpoints for configuration**. Every configuration screen sends the same
shape to the same endpoints:

```json
{ "entityType": "apps.class", "entityCode": "REGION", "operation": "CREATE", "payload": { … the DTO … } }
```

1. `Endpoints/AdminEndpoints.cs` receives it (`POST /change-sets/apply`, `/submit-change`, or added to a draft).
2. It checks the user's privilege on the type's screen, and finds the module that owns the type (`ChangeSetRouter`).
3. The kernel's `ChangeSetService` records a **change set** with one **change item** (before/after JSON), and either
   publishes it at once (area without approval) or waits for a checker (maker-checker).
4. On publish it calls the type's **applier** (`IChangeApplier.ApplyAsync`), which validates the payload, maps it onto the
   entity and lets the service save. All items of a change set are saved in one transaction.

Reads are generic too: `GET /admin/{entityType}` and `GET /admin/{entityType}/{code}` return the same payload shape.
Why: the same mechanism gives approval, history (before/after), export and import of configuration packages between
environments (codes, never ids), for every module, without writing it again per screen.

Each part of this handover shows this flow for its own tables.

### Database at a glance

One PostgreSQL database, one schema per module: `apps` (Apps Control), `integ`, `party`, `svc`, `wf`, `doc`, `dec`,
`com`, `pay`, `vis`, `cms` (content), `rpt`; plus the kernel's `cfg` (change sets), `audit` (audit log) and `platform`
(outbox). Column names are `snake_case` (EF convention `UseSnakeCaseNamingConvention`), enums are stored as text,
ids are UUID v7, and every configuration table has `xmin` as its row version (optimistic concurrency).

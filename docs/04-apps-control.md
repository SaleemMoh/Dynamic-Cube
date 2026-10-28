# 4. Apps Control

Apps Control is the configuration and user-management backbone. It **serves several apps**
(Internal, External, Field, and any future app built on Dynamic Cube) and will expose APIs to
other products later. It lives in the `AppsControl` module, PostgreSQL schema `apps`.

It evolves an existing, proven Apps Control design (Instance / Apps / Company / Menu Groups /
Menus / Class–Domain / List of Values / Roles / Users and their link screens). The concepts
are kept; the implementation is adapted for a single-entity, on-prem, multi-app product.

## 4.1 Decisions applied

| Topic | Decision |
|---|---|
| Tenancy | **Single entity per installation.** Departments are org units, not companies |
| Terminology | Keep **Class / Domain** (lookup type / lookup value) |
| Flex fields | Replace fixed `Flex_value_1..10` / dates / numbers with **typed attribute definitions** per class, values stored in **JSONB**, exposed to reporting through **generated typed views** |
| LOV | Keep LOV as the **security & filter layer** above Class/Domain and other tables; replace free SQL with **generated queries** over registered sources; add **rule-based grants**; apply grants to **data**, not only dropdowns |
| Privileges | Resource + **actions** (view, create, edit, delete, approve, export, print), plus field rules |
| Link screens | Same data, managed from the main record (user screen tabs, role screen tabs) |
| Lifecycle | All Apps Control configuration goes through **change sets** (optional maker-checker) and is audited |

## 4.2 Mapping from the existing design

| Existing screen | Dynamic Cube |
|---|---|
| Instance Setup | `installation` (single row). No DB connection fields — those live in deployment config / secrets |
| Apps Setup | `app` |
| Company Setup | `entity` (single row) + org structure |
| — | `org_unit_type`, `org_unit`, `position` (new) |
| Menu Groups Setup / Menus Setup | `menu_group`, `menu` (+ item types) |
| Class – Domain Setup | `class`, `class_attribute`, `domain`, `domain_external_code` |
| List of Values Setup | `data_object`, `lov` |
| Link Classes with Screens | Form schema: a field references an LOV |
| Menu – Value Setup | `lov_binding` (screen ↔ LOV, selection mode) |
| Roles Setup | `role`, `privilege` |
| Users Setup | `user_account` (+ assignments, delegations) |
| Link Apps with Inst & Cmp | Not needed (single instance & entity); app access via roles |
| Link User with Instance / Company | Not needed / user screen |
| Link Users with Roles | `user_role` (user or role screen tab) |
| Link Roles with Domain (Roles – Screens Setup granted values) | `lov_grant` (global per role, optional per-screen override) + `data_scope_binding` |

## 4.3 Common columns

Unless stated otherwise every table has:

| Column | Type | Notes |
|---|---|---|
| `id` | uuid (v7) | Primary key |
| `code` | text | Stable business key (unique per scope); used across environments |
| `name_ar`, `name_en` | text | Bilingual labels |
| `is_active` | bool | |
| `owner` | text | `SYSTEM` (product-owned) / `ENTITY` (customer-owned) |
| `created_at`, `created_by`, `updated_at`, `updated_by` | | Audit columns |
| `row_version` | xmin | Optimistic concurrency |

## 4.4 Installation, apps, entity

**`installation`** (one row) — `system_type`, `copyright`, `default_language`, `timezone`,
`weekend_days`, `calendar_display` (Hijri/Gregorian/both), `logo_file_id`, `default_file_folder`.

**`app`** — `app_type` (`INTERNAL` | `EXTERNAL` | `FIELD` | `OTHER`), `base_url`,
`display_order`. Examples: `INTERNAL`, `EXTERNAL`, `FIELD`.

**`entity`** (one row) — the government entity: names, `entity_kind` (ministry / authority /
company — a domain of class `ENTITY_KIND`), `unified_number`, `cr_number`, `logo_file_id`.

## 4.5 Organization structure

**`org_unit_type`** — configurable unit types (Sector, General Department, Department,
Section, Branch, Region, Committee…): `display_order`, `allowed_parent_type_ids` (uuid[]).

**`org_unit`** — `org_unit_type_id`, `parent_id`, `path` (ltree, for subtree queries),
`attributes` (jsonb: region, amanah, cost center…), `effective_from`, `effective_to`.

**`position`** — generic positions used in routing (Manager, Analyst, Auditor, Inspector,
Request Manager…).

## 4.6 Users, roles, privileges

**`user_account`** — `username`, `idp_subject` (Keycloak), `auth_source` (`AD` | `LOCAL`),
`name_ar`, `name_en`, `email`, `mobile`, `national_id`, `status` (`ACTIVE` | `LOCKED` |
`DISABLED`), `preferred_language`, `last_login_at`. Authentication stays in Keycloak.

**`user_assignment`** — `user_id`, `org_unit_id`, `position_id`, `is_primary`,
`effective_from`, `effective_to`.

**`delegation`** (acting / out-of-office) — `from_user_id`, `to_user_id`, `scope`
(`ALL` | `ROLES` | `TASK_TYPES`), `scope_codes` (text[]), `effective_from`, `effective_to`,
`reason`.

**`role`** — `app_id` (null = cross-app), `reference_code`.

**`user_role`** — `user_id`, `role_id`, `org_unit_id` (optional: role held *within* a unit,
e.g. "Analyst in Riyadh branch"), `effective_from`, `effective_to`.

**`resource`** — anything securable: `app_id`, `resource_type` (`SCREEN` | `API` | `REPORT` |
`SERVICE` | `WORKFLOW_ACTION` | `FIELD`), `parent_id`, `available_actions` (text[]).
Screens, services and reports register themselves as resources.

**`privilege`** — `role_id`, `resource_id`, `actions` (text[] ⊆ `VIEW`, `CREATE`, `EDIT`,
`DELETE`, `APPROVE`, `EXPORT`, `PRINT`), `field_rules` (jsonb: field → `HIDDEN` | `READONLY`).

"Copy privileges from another role" is a built-in admin action.

## 4.7 Menus

**`menu_group`** — `app_id`, `display_order`, `icon`.

**`menu`** — `app_id`, `menu_group_id`, `parent_id`, `item_type` (`SCREEN` | `SERVICE` |
`REPORT` | `DASHBOARD` | `QUEUE` | `URL`), `target` (route / service code / report code /
queue code / URL), `resource_id`, `short_desc_ar`, `short_desc_en`, `desc_ar`, `desc_en`,
`icon` (icon name or SVG), `display_order`, `is_visible`.

A user's menu = items whose resource the user holds `VIEW` on, for the requested app.

## 4.8 Class / Domain

**`class`** — lookup type.

| Column | Notes |
|---|---|
| `code` | e.g. `SECTOR`, `REGION`, `AMANAH`, `REQUEST_STATUS` |
| `class_type` | `SYSTEM_LOOKUP` / `COMPANY_LOOKUP` (maps to `owner`) |
| `parent_class_id` | Reference class (e.g. `CITY` → `REGION`) |
| `is_hierarchical` | Values may have a parent value within the same class |
| `allow_multi_assign` | |
| `source_lov_id` | Virtual class backed by an LOV |

**`class_attribute`** — typed attribute definitions (replaces Flex Values / Dates / Numbers and
Domain Value / Date / Number Parameters).

| Column | Notes |
|---|---|
| `class_id`, `code`, `name_ar`, `name_en` | |
| `data_type` | `TEXT` · `NUMBER` · `DATE` · `BOOL` · `DOMAIN_REF` · `JSON` |
| `ref_class_id` | When `DOMAIN_REF` |
| `attribute_group` | `ATTRIBUTE` / `PARAMETER` (keeps the existing tab grouping) |
| `is_required`, `validation` (jsonb), `display_order` | |
| `is_reportable` | Included as a column in the generated view |

**`domain`** — lookup value.

| Column | Notes |
|---|---|
| `class_id`, `code` | `code` is the stable value used by forms, rules, reports |
| `parent_domain_id` | Value-level parent (city → region) |
| `name_ar`, `name_en`, `details_ar`, `details_en` | |
| `display_order`, `is_default` | |
| `attributes` | jsonb, validated against `class_attribute` |
| `effective_from`, `effective_to`, `is_active` | |

Indexes: unique (`class_id`, `code`); GIN on `attributes`.

**`domain_external_code`** — `domain_id`, `provider_code` (integration provider, e.g. `MCI`),
`external_code`, `direction` (`IN` | `OUT` | `BOTH`). Translates provider lookups (e.g. MCI
relation types) to and from domain values.

**Generated views** — for each class with reportable attributes the platform maintains
`apps.v_class_<code>` exposing `code`, names, parent, and every attribute as a typed column.
Views are regenerated when attribute definitions are published. The report builder sees normal
columns.

Guidance: Class/Domain is for **reference values**. Data with its own lifecycle (people,
vehicles, establishments, projects) belongs in entities.

## 4.9 LOV — security & filter layer

An LOV is a **named, registered data source** above Class/Domain and other tables. It feeds
dropdowns and, through grants, **filters data** for each role.

### `data_object` (catalog)

Tables or views that LOVs and data scopes may use. Only registered objects are queryable.

| Column | Notes |
|---|---|
| `code`, names | e.g. `REQUEST`, `ESTABLISHMENT`, `ISIC_ACTIVITY` |
| `db_schema`, `db_object`, `object_type` (`TABLE` / `VIEW`) | |
| `columns` | jsonb: name, data type, labels, `filterable`, `searchable` |

### `lov`

| Column | Notes |
|---|---|
| `code`, names | e.g. `LOV_SECTOR`, `LOV_CITY_BY_REGION` |
| `source_type` | `CLASS` · `DATA_OBJECT` · `INTEGRATION` · `SQL` |
| `class_id` | When `CLASS` |
| `data_object_id` | When `DATA_OBJECT` |
| `integration_operation_code` | When `INTEGRATION` (cached) |
| `sql_text` | When `SQL` — **restricted** (see below) |
| `value_column`, `display_ar_column`, `display_en_column` | Both languages returned; client chooses |
| `static_filter` | JSONLogic / structured filter |
| `parameters` | jsonb: cascading parent (`:region`), user context (`:user_id`, `:org_unit_path`, `:app`) |
| `default_selection_mode` | `SINGLE` / `MULTI` |
| `search_mode` | `FULL` (small lists) / `TYPEAHEAD` (server-side search + paging) |
| `max_rows`, `cache_policy` | |

The platform **generates** the query:
`SELECT value, display_ar, display_en FROM <source> WHERE <static filter> AND <parameters> AND <grant predicate>`.

**Restricted SQL** keeps existing LOV queries usable: executed with a separate read-only database
role (it can read only the tables and views it was granted), one SELECT statement, bound parameters
only (`@user_id`, `@username`, `@org_unit`, `@app`), validated on save, row limit and timeout,
editable only with a dedicated privilege. The LOV names the query's value, Arabic and English label
(and optional parent) columns; when left empty they default to `value`, `display_ar`, `display_en`,
so queries can be pasted as they are.

### `lov_binding` (screen ↔ LOV)

`resource_id` (screen or form), `field_code`, `lov_id`, `selection_mode`. Replaces
"Menu – Value Setup". Forms built in the Service Builder reference LOVs directly in field
definitions.

### `lov_grant` (role ↔ LOV)

| Column | Notes |
|---|---|
| `role_id`, `lov_id` | |
| `resource_id` | Null = global; set = per-screen override |
| `grant_type` | `ALL` · `VALUES` · `RULE` · `NONE` |
| `granted_values` | text[] when `VALUES` |
| `rule` | JSONLogic over the user context when `RULE`, **returning the granted value(s)**, e.g. `{"var": "user.orgUnitAttributes.REGION"}` |
| `default_value` | |

A user's effective grant on an LOV = union of their roles' grants (screen override first, then
global). Resolved grants are cached per session.

### `data_scope_binding` (grants applied to data)

| Column | Notes |
|---|---|
| `data_object_id`, `column_name` | e.g. `REQUEST.sector_code` |
| `lov_id` | e.g. `LOV_SECTOR` |
| `applies_to` | `ALL` or specific resources (inbox, search, report, export, API) |

The query layer (EF Core global filters and the report query builder) appends the user's grant
predicate to every query on a bound object. The same rule therefore governs dropdowns, inboxes,
searches, reports, exports and APIs.

## 4.10 Settings and UI texts

**`setting`** — `key`, `app_id` (null = global), `data_type`, `value` (jsonb), `description`,
`is_secret` (secrets stored encrypted / referenced from the secret store).

**`ui_text`** — `app_id`, `key`, `text_ar`, `text_en`. Overrides UI labels and messages
without a release.

## 4.11 Lifecycle and audit

- Every create/update/delete in Apps Control is captured in a **change set**
  (`cfg.change_set`, `cfg.change_item` with operation, entity type, code, before/after).
- Maker-checker policy per area (e.g. roles and grants require approval; UI texts do not).
- Publishing applies the change set atomically and writes `audit.audit_log`.
- Export/import packages carry Apps Control configuration by `code`; `SYSTEM` rows are updated
  by product upgrades, `ENTITY` rows never are.

## 4.12 Apps Control API

Used in-process by Dynamic Cube apps today; designed as a **versioned REST contract**
(`/api/apps-control/v1`, OpenAPI documented) so it can be exposed to other products later.

| Endpoint | Purpose |
|---|---|
| `GET me/apps` | Apps the user can access |
| `GET me/menu?app=` | Menu tree for an app |
| `GET me/privileges?app=` | Resources and actions |
| `GET me/grants?lov=` | Resolved grant for an LOV |
| `GET lovs/{code}/values?parent=&q=&page=` | LOV values (grant-filtered, both languages) |
| `GET classes/{code}/domains` | Domain values of a class |
| `GET settings/{key}` | Setting value (non-secret) |
| Admin endpoints | CRUD via change sets, internal host only |

Authentication via Keycloak tokens; authorization decisions are made server-side so tokens stay
small.

## 4.13 Implementation notes (Phase 1a)

What the first slice actually built, and the rules it follows.

**Editing.** Every change goes through a change set. A change item is `entity type + entity code +
operation + payload`; payloads refer to other rows **by code**, so change sets can be exported and
replayed in another environment. Composite keys: domain values `CLASS/CODE`, app-scoped settings and
UI texts `APP/KEY`. Items are validated when added (references may point to rows created by earlier
items of the same draft) and again when published; each item is saved in order inside one
transaction, so a failing item rolls the whole change set back.

| Area (maker-checker policy) | Entity types | Guarding resource |
|---|---|---|
| `apps.system` | installation, entity, app | `apps-control.installation` |
| `apps.organization` | org unit type, org unit, position | `apps-control.organization` |
| `apps.security` | user (with assignments and roles), role (with privileges and grants), resource | `apps-control.users` / `.roles` / `.resources` |
| `apps.menus` | menu group, menu | `apps-control.menus` |
| `apps.lookups` | class (with attribute definitions), domain (with external codes) | `apps-control.classes` |
| `apps.lovs` | data object (with data-scope bindings), LOV (with screen bindings) | `apps-control.lovs` (+ `apps-control.lov-sql` for SQL) |
| `apps.settings` / `apps.ui-texts` | setting / UI text | `apps-control.settings` / `.ui-texts` |
| `apps.governance` (always approval) | approval policy of the areas above | `apps-control.approval-policy` |

Adding an item needs `CREATE`/`EDIT`/`DELETE` on the guarding resource and `CREATE` on
`apps-control.change-sets`; approving needs `APPROVE` there and a different user than the maker.
Approval publishes immediately.

**Approval policy** is set per area on the *Approval policy* screen (`apps.change_area`; all areas
need approval on a new installation except UI texts). In areas without approval, **Save** publishes
the change at once (`POST change-sets/apply`); it is still recorded as a one-item change set with its
before/after, and in the audit log. Changing the policy itself always needs approval
(`apps.governance`), so nobody can switch approval off alone.

**Ownership.** `SYSTEM` rows (apps, Apps Control resources and menus, the `SYSTEM_ADMIN`,
`CONFIG_MAKER` and `CONFIG_CHECKER` roles, class `ENTITY_KIND`, position `MANAGER`) are inserted and
refreshed by the migrator on every upgrade and are read-only in change sets. Entities may add their
own values to a SYSTEM class.

**Typed attributes.** Domain attribute values (JSONB) are validated against the class's definitions
(type, required, `min`/`max`/`maxLength`/`pattern`, referenced class). Changing a definition is
refused while existing values would not fit; removing one removes its values. `apps.v_class_<code>`
is regenerated on publish with one typed column per reportable attribute.

**Users and access.** Accounts are created on first sign-in (or linked by username when an
administrator created them beforehand). `AppsControl:BootstrapUsers` maps usernames to roles that
are added at sign-in — used for the first administrator. Privileges, menu and user context are
cached per user for `AppsControl:AccessCacheSeconds` (60 s) and invalidated on publish.

**User context** available to grant rules and SQL LOVs: `user.id`, `user.username`, `user.roles`,
`user.positions`, `user.orgUnits`, `user.orgUnit` (primary), `user.orgUnitAttributes` (attributes
of the primary unit), `user.subtreeOrgUnits` (assigned units and everything below), `app`.

**Front end.** The internal system builds its side menu from `me/menu`; Apps Control screens are
generated from field definitions (`web/projects/internal/src/app/apps-control/config`) and render
with the shared `DynamicForm`. The domain value form is generated from the class's attribute
definitions. Nested lists (privileges, grants, assignments, LOV bindings) are edited as JSON until
the visual builders of Phase 3. Form fields can reference an LOV (`lov`, `lovParentField` for
cascading lists); options come from `lovs/{code}/values`, already filtered by the user's grants.

**Branding.** The *Branding* screen (resource `apps-control.installation`) sets the entity logo and the footer
links shown by every app. Logos (PNG, JPEG, WebP or script-free SVG, ≤ 512 KB) are uploaded to
`apps.branding_asset` — in the database, so the internal and external hosts both serve them without shared file
storage — and become current through a change set on the entity (`logoFileKey`, `footerLinks`). Until approved, an
uploaded image is visible only to Apps Control administrators; the published logo is public
(`GET branding`, `GET branding/assets/{id}`). Footer wording is the UI texts `footer.copyright` and `footer.owner`
(`{name}`, `{year}` are filled in). A slot for the DGA registration badge will be added when the asset is provided.

# Legacy Apps Control data → Dynamic Cube: import plan

Status: **plan, waiting for agreement**. Source: the owner's export of the Oracle Apps Control (28 CSV files).
Decisions already taken: remove the whole demo; **composite keys as in legacy**; users get a sign-in account with a
one-time password, with their companies and roles; the data is **not** committed to git (read from a folder on the
server).

## 1. What is in the file

| Legacy table | Rows | Goes to |
|---|---|---|
| `SYS_INSTANCEPROFILE` | 18 | `sys_instance_profile` (one row per instance; instance 0 "zero instance" imported inactive) |
| `SYS_COMPANY_PROFILE` | 42 | `sys_company_profile`, key **(instid, cmp_num)** |
| `SYS_APPS_PROFILE` / `SYS_APPS_COMPANY` | 13 / 31 | `sys_apps_profile` as *apps managed by Dynamic Cube*, with their companies |
| `SYS_MENU_GROUPS` | 8 | `sys_menu_groups`, key **(appsid, menu_group_id)** |
| `SYS_MENU` | 175 | `sys_menu`, key **(appsid, menu_code)**; `OBJECT_NAME` (e.g. `dashboard.settings.menu`) becomes the app's screen in `sys_resources` |
| `DD_CLASSES` | 182 | `dd_classes`; the 62 used flex slots become typed attributes (`dd_class_attributes`) |
| `DD_DOMAIN` | 637 | `dd_domain`, system values (no instance/company) |
| `DD_COMPANY_DOMAIN` | 8,042 | `dd_domain`, key **(class, instid, cmp_num, value)**; 6,847 are inactive and stay inactive |
| `DD_COMPANY_DOMAIN_VIRTUAL_VALUES` | 2 | the value's list-of-values attribute |
| `DD_LOVS` | 139 | `dd_lovs`: 120 that read a class become *Class* lists; 19 that read other legacy tables (employee, expense, project, document-owner views) are kept with their SQL but **inactive** (Oracle SQL on tables that are not here) |
| `SYS_MENU_LOVS` | 174 | `sys_menu_lovs` / `dd_data_scope_bindings` |
| `SYS_ROLES` | 86 | `sys_roles`, key **(instid, cmp_num, role_code)** |
| `SYS_ROLE_MENUS` | 420 | `sys_role_menus` + view privilege on the screen |
| `SYS_ROLE_MENU_LOVS` | 7,893 | `sys_role_menu_lovs`: one row per role/screen/list (751) with the values in `value_codes` and the default value |
| `SYS_USERS` | 365 | `sys_users` (name, email, phone, employee id); a Keycloak account each |
| `SYS_USERINSTANCES` / `SYS_USERINSTANCES_COMPANIES` | 399 / 522 | the user's instances and companies (default company → `last_cmp_num`) |
| `SYS_USER_ROLES` | 271 | `sys_user_roles` (161 users have roles) |

**Not imported**: every password, OTP code and token (`USERPWD`, `PASSWORD_*`, `OTPCODE`, `BI_USER_PWD`,
`REMEMBER_TOKEN`, `INSTDBPWD`, `DB_PSWD`, `SYSTEM_LOGIN_PWD`, `EMAIL_SETTINGS.USER_PWD`); instance database connections;
`SYS_SYSTEM_PROFILE` / `SYS_USER_SYSTEMS` (logins to other databases); `EMAIL_SETTINGS` (SMTP is set once in
Communication settings, its password in the secret store); `SYS_MENU_DOMAIN` (declined). `USERS`,
`AUTHENTICATABLE_USERS`, `BAS_COMPANIES`, `SYS_COMPANY_USERS`, `NOTIFICATIONS` are empty in the export.

## 2. Composite keys (the design change)

Today companies, roles, menus and class values each have one unique code. They become keyed like legacy:

| Table | Key now | Key after |
|---|---|---|
| `sys_company_profile` | `cmp_num` | `instid, cmp_num` |
| `sys_company_branches` | `branch_code` | `instid, cmp_num, branch_code` |
| `sys_roles` | `role_code` | `instid, cmp_num, role_code` (a role belongs to one company, as in legacy) |
| `sys_menu_groups` | `menu_group_code` | `appsid, menu_group_code` |
| `sys_menu` | `menu_code` | `appsid, menu_code` |
| `dd_domain` | `class_id, value` | `class_id, instid, cmp_num, value` (system values: instid and cmp_num empty) |

What follows from it:
- Tables keep the `id` (uuid) for foreign keys; the composite key is the unique business key.
- Change sets, packages, exports and the API name such a row by its key parts joined with `/`
  (`5/2` for a company, `12/99/ADMIN` for a role, `REGION/5/2/RIYADH` for a company value).
- The *current company* (header, workspace picker, requests, tasks, documents) becomes the pair instance + company.
- Values per company become one row per company (as `DD_COMPANY_DOMAIN`) instead of one row with a company list.
- Role company overrides are no longer needed (a role already belongs to one company) and are removed.
- Screens: company, role and value pickers show the instance and company; the role screen and the class values screen
  get an instance/company filter.

## 3. The import

- `Migrator` gets a **legacy import**: `LegacyImport:Folder=/data/legacy` (the unzipped CSV folder, mounted into the
  container, never in git) and `LegacyImport:Replace=true`.
- *Replace* empties every module's data (configuration, requests, tasks, documents, reports, change sets, audit),
  seeds what the product itself needs (admin, Dynamic Cube's own apps, menus, screens and system classes), and removes
  the demo users from Keycloak.
- It then loads the file in the order of section 1, as published change sets (so the audit shows the import), and
  reports what it skipped and why.
- **Sign-in**: one Keycloak account per user (username = `USERID`, email kept) with a random one-time password and
  "change password at first sign-in". The passwords are written once to a file in the import folder for the
  administrator to hand out, and are not stored anywhere else.
- Running it again with the same folder updates rows and adds missing ones.

## 4. Steps

1. Composite keys: tables, migration, change sets and API keys, current company as instance + company, screens. Tests.
2. Importer: replace, CSV reader, mapping above, Keycloak accounts, skip report. Tests with a small sample file.
3. Run on the dev database with the real file; check the screens (companies, classes and values per company, lists,
   menus per app, roles and their grants, users and their access); docs and the owner's run command.

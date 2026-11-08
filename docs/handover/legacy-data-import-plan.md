# Legacy Apps Control data → Dynamic Cube: import

Status: **built** (composite keys and the importer; the run is in section 3). Screens that pick a company, role or
value still need a pass for the instance/company filters (step 4.1, open). Source: the owner's export of the Oracle Apps Control (28 CSV files).
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
| `DD_COMPANY_DOMAIN` | 8,042 | `dd_domain`, key **(class, instid, cmp_num, value)**; `ACTIVE_FLAG` is `1` on 1,195, `0` on 7 and empty on 6,840: empty is imported as **active** for now (open question to the owner) |
| `DD_COMPANY_DOMAIN_VIRTUAL_VALUES` | 2 | the value's list-of-values attribute |
| `DD_LOVS` | 139 | `dd_lovs`: the lists that read a class become *Class* lists (117); the ones that run Oracle SQL on tables that are not here (employee, expense, project, document-owner views) are **not imported** and listed in the report, to be rebuilt with the query builder |
| `SYS_ROLES` | 86 | `sys_roles`, key **(instid, cmp_num, role_code)** |
| `SYS_ROLE_MENUS` | 420 | `sys_role_menus` + view privilege on the screen |
| `SYS_ROLE_MENU_LOVS` | 7,893 | `sys_role_menu_lovs`: one row per role/screen/list (751) with the values in `value_codes` and the default value |
| `SYS_USERS` | 365 | `sys_users` (name, email, phone, employee id); a Keycloak account each |
| `SYS_USERINSTANCES` | 399 | the user's default instance |
| `SYS_USER_ROLES` | 271 | `sys_user_roles` (161 users have roles) |

**Not imported**: every password, OTP code and token (`USERPWD`, `PASSWORD_*`, `OTPCODE`, `BI_USER_PWD`,
`REMEMBER_TOKEN`, `INSTDBPWD`, `DB_PSWD`, `SYSTEM_LOGIN_PWD`, `EMAIL_SETTINGS.USER_PWD`); instance database connections;
`SYS_SYSTEM_PROFILE` / `SYS_USER_SYSTEMS` (logins to other databases); `EMAIL_SETTINGS` (SMTP is set once in
Communication settings, its password in the secret store); `SYS_MENU_DOMAIN` (declined); `SYS_MENU_LOVS` and
`SYS_USERINSTANCES_COMPANIES` (a user's companies come from the roles they hold); the legacy Configuration Center
(`APPSID` 5, replaced by Dynamic Cube). `USERS`,
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
- The *current company* (header, workspace picker, requests, tasks, documents) is the key `instid/cmp_num`.
- Values per company become one row per company (as `DD_COMPANY_DOMAIN`) instead of one row with a company list.
- A role of one company cannot have company overrides; overrides stay for roles of no company (Dynamic Cube's own).
- Screens: company, role and value pickers show the instance and company; the role screen and the class values screen
  get an instance/company filter.

## 3. The import (built)

`src/Tools/Migrator/LegacyImporter.cs` (with `LegacyCsv.cs`). The migrator runs it instead of the demo when
`LegacyImport:Folder` names a folder holding `SYS_COMPANY_PROFILE.csv`. In Docker that folder is
`deploy/docker/legacy-data/` (mounted as `/data/legacy`; nothing in it is committed).

- **First run** (the first legacy company is not there yet): every module's data is emptied (configuration, requests,
  tasks, documents, reports, change sets, audit), what the product itself needs is seeded again (admin, Dynamic
  Cube's own apps, menus, screens, system classes), then the file is loaded as published change sets in the order of
  section 1, so the audit shows the import.
- **A run that stopped part way** carries on: rows already there are left alone, the rest is created.
- **Later runs** only create missing sign-in accounts. `LegacyImport:Refresh=true` applies the file again (updates rows).
- **Sign-in**: one Keycloak account per user (username = `USERID`, with spaces as `.`; e-mail kept unless another
  account has it), with a random one-time password the user changes at first sign-in. The passwords are written to
  `one-time-passwords-<date>.csv` in the folder, even when the run stops part way, and are stored nowhere else.
- **Administrator**: the legacy user `admin` is Dynamic Cube's administrator. It keeps the system level (it sees every
  instance) with the legacy roles added, instead of the legacy default instance.
- **Report**: `import-report.txt` in the folder lists what was loaded and every row left out or changed, with the
  reason (about 310 lines for the owner's file: role screens and list grants on items that are not imported screens,
  e-mails used twice, SQL lists, the Configuration Center, one class whose code differs only in case from another,
  user names Keycloak does not take).
- Speed: about 15 minutes on the first run (8,700 class values); a later run takes seconds.
- Users whose roles are in the legacy apps (connected apps) see only the dashboard in Dynamic Cube itself: their menus
  and screens are served to those apps through the Connect API (see `docs/04-apps-control.md`).
- Docker's Keycloak keeps no data between restarts: its accounts (and so the one-time passwords) are created again by
  the next run.

Check after a run (Docker):

    docker compose -f deploy/docker/docker-compose.yml exec postgres psql -U dynamic_cube -d dynamic_cube -c "\dt apps.*"
    docker compose -f deploy/docker/docker-compose.yml exec postgres psql -U dynamic_cube -d dynamic_cube -c "select count(*) from apps.sys_company_profile"

## 4. Steps

1. ~~Composite keys: tables, migration, change sets and API keys, current company as instance + company.~~ Done, with
   tests. Open: instance/company filters on the role and class-values screens.
2. ~~Importer: replace, CSV reader, mapping above, Keycloak accounts, skip report.~~ Done.
3. ~~Run on the dev database with the real file.~~ Done; the owner runs it with the command in
   `deploy/docker/legacy-data/README.md`.

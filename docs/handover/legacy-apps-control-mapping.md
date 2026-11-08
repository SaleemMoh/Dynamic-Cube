# Legacy Apps Control (Oracle) ↔ Dynamic Cube Apps Control

How the Oracle Apps Control schema being replaced maps to our `apps` schema (PostgreSQL), which table names we would take
to follow its naming convention, and what each side has that the other lacks. Laravel framework tables and copies are
left out as asked: `CACHE*`, `*_EMPTY_*`, `*_BK*`, `JOBS`, `JOB_BATCHES`, `FAILED_JOBS`, `SESSIONS`,
`PASSWORD_RESET_TOKENS`, `ZZ_*`, `MIGRATIONS`.

Status: **proposal**. Nothing is renamed until the decisions at the end are made.

## 1. The two naming conventions

| | Legacy (Oracle) | Ours today (PostgreSQL) |
|---|---|---|
| Area prefix | `SYS_` security, navigation, installation, companies; `DD_` data dictionary; `BAS_` base masters | none; the module is the schema (`apps.`) |
| Case | UPPER_CASE | lower_case (PostgreSQL folds unquoted names to lower case) |
| Singular / plural | mostly plural (`SYS_USERS`, `SYS_ROLES`, `DD_CLASSES`, `DD_LOVS`), some singular (`SYS_MENU`, `DD_DOMAIN`) | singular (`role`, `menu`, `domain`) |
| Link tables | parent then child: `SYS_USER_ROLES`, `SYS_ROLE_MENUS`, `SYS_ROLE_MENU_LOVS` | `user_role`, `role_menu`, `lov_grant` |
| Keys | natural composite keys (`INSTID, CMP_NUM, ROLE_CODE`) | `id` (uuid) plus a unique `code` |
| Arabic / English | `_NTV` / `_FRN` (`ROLE_DESC_NTV`) | `name_ar` / `name_en` |
| Instance / company | `INSTID`, `CMP_NUM` | `entity_code`, `company_code` |
| Active / order | `ACTIVE_FLAG` or `IS_ACTIVE`, `ORDERBY` or `ORDER_NUM` | `is_active`, `display_order` |
| Extra values | fixed `FLEX_VALUE_1..10`, `FLEX_DATE_1..5`, `FLEX_NUMBER_1..5` columns | typed attributes defined per class (`class_attribute`), values in one `attributes` column |

## 2. Proposed table names (legacy convention, lower case)

Where a legacy table exists, its name is taken as it is; where it does not, the same prefixes and plural form are used.

| Ours today | Proposed | Legacy counterpart |
|---|---|---|
| `apps.installation` | `apps.sys_instance_profile` | `SYS_INSTANCEPROFILE` |
| `apps.entity` | `apps.sys_entities` | (in `SYS_INSTANCEPROFILE`) |
| `apps.company` | `apps.sys_company_profile` | `SYS_COMPANY_PROFILE`, `BAS_COMPANIES` |
| `apps.branch` | `apps.sys_company_branches` | none |
| `apps.app` | `apps.sys_apps_profile` | `SYS_APPS_PROFILE` |
| `apps.business_app` | `apps.sys_business_apps` | `SYS_APPS_COMPANY` (its companies) |
| `apps.app_branding` | `apps.sys_apps_branding` | none |
| `apps.branding_asset` | `apps.sys_branding_assets` | none |
| `apps.user_account` | `apps.sys_users` | `SYS_USERS` |
| `apps.role` | `apps.sys_roles` | `SYS_ROLES` |
| `apps.user_role` | `apps.sys_user_roles` | `SYS_USER_ROLES` |
| `apps.resource` | `apps.sys_resources` | `SYS_MENU.OBJECT_NAME` |
| `apps.menu` | `apps.sys_menu` | `SYS_MENU` |
| `apps.menu_group` | `apps.sys_menu_groups` | `SYS_MENU_GROUPS` |
| `apps.role_menu` | `apps.sys_role_menus` | `SYS_ROLE_MENUS` |
| `apps.privilege` | `apps.sys_role_privileges` | none (legacy gives whole screens, no actions) |
| `apps.lov_grant` | `apps.sys_role_menu_lovs` | `SYS_ROLE_MENU_LOVS` |
| `apps.lov_binding` | `apps.sys_menu_lovs` (or dropped, LOV review point 1) | `SYS_MENU_LOVS` |
| `apps.user_privilege` | `apps.sys_user_privileges` | none |
| `apps.user_privilege_grant` | `apps.sys_user_privilege_lovs` | none |
| `apps.setting` | `apps.sys_settings` | none |
| `apps.ui_text` | `apps.sys_ui_texts` | none |
| `apps.change_area` | `apps.sys_change_areas` | none |
| `apps.class` | `apps.dd_classes` | `DD_CLASSES` |
| `apps.class_attribute` | `apps.dd_class_attributes` | `DD_CLASSES.FLEX_*` / `DOMAIN_FLEX_*` columns |
| `apps.domain` | `apps.dd_domain` | `DD_DOMAIN`, `DD_COMPANY_DOMAIN` |
| `apps.lov` | `apps.dd_lovs` | `DD_LOVS` |
| `apps.data_object` | `apps.dd_data_objects` | none |
| `apps.data_scope_binding` | `apps.dd_data_scope_bindings` | `SYS_MENU_LOVS.VALUE_FIELD` (partly) |
| `apps.org_unit` | `apps.org_units` | none |
| `apps.org_unit_type` | `apps.org_unit_types` | none |
| `apps.position` | `apps.org_positions` | none |
| `apps.user_assignment` | `apps.org_user_assignments` | none |
| `apps.holiday` | `apps.bas_holidays` | none |
| views `apps.v_class_<code>` | `apps.dd_v_<code>` (optional) | `APPS_CONTROL_DD_DOMAIN` view |

Shared platform tables keep their names: `cfg.change_sets`, `cfg.change_items`, `audit.audit_log`,
`platform.outbox_messages`.

## 3. Verification: each legacy table in our schema

| Legacy table | Ours | Notes |
|---|---|---|
| `SYS_INSTANCEPROFILE` | `installation` + `entity` | `INSTID` → `entity_code`; `INSTLOGO` → `logo_file_key`; `COPYRIGHT`, `SYSTEMTYPEID` → `copyright`, `system_type`. **`INSTDBNAME`/`INSTDBPWD`/`INSTDBCONN` are not stored in a table**: connection strings are deployment settings and secrets live in the secret store. |
| `SYS_USERS` | `user_account` | names `_NTV/_FRN` → `name_ar/name_en`; `USEREMAIL` → `email`; `USERPHONENO` → `mobile`; `LAST_LOGIN_DATE` → `last_login_at`; `ISADMIN` → a role. **Passwords, OTP, 2FA flag and `REMEMBER_TOKEN` are not stored**: sign-in, passwords and MFA are Keycloak's. Gaps: `USER_PICTURE`, `NICK_NAME_*`, `EMPID`, `ERPUSERID`, `BI_USER_ID`. |
| `SYS_USERINSTANCES` | none | one installation serves one entity per row (`entity_code` on the user); a user across several instances is not modelled. |
| `SYS_COMPANY_PROFILE` / `BAS_COMPANIES` | `company` | `CMP_NUM` (number) → `code` (text); `CMP_DES_*` → `name_ar/name_en`. Gaps: `DEFAULT_BU`, `DEFAULT_FILE_FOLDER`, `LOGO_PATH`, `PATH_WMARK`, `CMP_ABBREVIATION` (branding is per app in `app_branding`). |
| `SYS_USERINSTANCES_COMPANIES` | `user_role.company_code` | a user's companies come from their roles; the last company chosen is `user_account.last_company_code`. Gaps: default-company flag, `DEFAULT_CURRENCY`, `SYSTEM_DATE`. |
| `SYS_APPS_PROFILE` | `app` (+ `business_app`) | `APPSID` → `code`; `APPS_DES_*` → names; `CLASS_CODE_EMAIL_LIST` not modelled. |
| `SYS_APPS_COMPANY` | `business_app.companies` | a list column instead of a link table. |
| `SYS_USER_APPS` (view) | worked out by the access resolver | from the user's roles, as the view does. |
| `DD_CLASSES` | `class` + `class_attribute` | the 20 fixed flex slots and their label columns become typed attribute rows (no limit, types text/number/date/class/LOV…). `CLASS_TYPE` (system / company domain) → `values_per_company` (+ `owner`); `REFERENCE_CLASS_CODE` → `parent_class_id`; `VIRTUAL_REF_LOV_CODE`, `VIRTUAL_ENABLE_MULTIPLE_ASSIGN` → an attribute of type LOV and `allow_multi_assign`. Not carried: `CLASS_INPUT_SOURCE`, `CLASS_CATEGORY`. |
| `DD_DOMAIN` | `domain` | `VALUE` → `code`; `DOMAIN_DESC_*` → names; `DOMAIN_DESC_DETAIL_*` → `details_ar/en`; `ORDERBY` → `display_order`; `FLEX_*` → `attributes`. |
| `DD_COMPANY_DOMAIN` | `domain` with `companies` | one table for system and company values: a class with `values_per_company` lists the companies on each value (or "all"). |
| `DD_COMPANY_DOMAIN_VIRTUAL_VALUES` | `domain.attributes` (LOV attribute, several values) | |
| `DD_LOVS` | `lov` | `SQLSTMT` → `sql_text` (source SQL); `VALUE_FIELD` → `value_column`; `REF_CLASS_CODE` → `class_id` (source Class). We add sources Data object and Query (builder). |
| `SYS_MENU` | `menu` + `resource` | `MENU_CODE`, names, `ORDER_NUM`, `MENU_ICON`, `PARENT_MENU_CODE` → `parent_id`; `GENERAL_DETAIL_FLAG` → `item_type`; `OBJECT_NAME` → the screen `resource`; `SHOW_FLAG` → `is_visible`; `MENU_LEVEL_NUM` is worked out from the parent; long/short descriptions → `description_ar/en`. |
| `SYS_MENU_GROUPS` | `menu_group` | |
| `SYS_MENU_LOVS` | `lov_binding` / `data_scope_binding` | which list filters a screen's data; `VALUE_FIELD` → the column it filters (`data_scope_binding.column_name`). |
| `SYS_ROLES` | `role` | `ROLE_CODE`, names, `IS_ACTIVE`, `REF_CODE` → `reference_code`; per company: `override_companies` and the company on `user_role`. |
| `SYS_ROLE_MENUS` | `role_menu` + `privilege` | legacy gives a whole screen; we also store its actions (view, create, edit…). |
| `SYS_ROLE_MENU_LOVS` | `lov_grant` | one row per value in legacy; one row per role/screen/list with `granted_values` and `default_value` here. |
| `SYS_USER_ROLES` | `user_role` | plus `branches`, `effective_from/to` and the org unit it came from. |
| `SYS_MENU_DOMAIN` | none | class values shown per screen; to confirm what it is used for. |
| `SYS_SYSTEM_PROFILE` / `SYS_USER_SYSTEMS` | Integration Manager connections | external systems are connections with their secrets in the secret store; **per-user logins to other systems are not modelled**. |
| `EMAIL_SETTINGS` | Communication settings + secret store | one SMTP set-up per installation, not per user. |
| `USERS`, `AUTHENTICATABLE_USERS`, `SYS_COMPANY_USERS` | Keycloak + `user_account` (+ party registry for portal users) | |
| `PERSONAL_ACCESS_TOKENS` | API clients (`integ` schema, secrets in the secret store) | |
| `NOTIFICATIONS` | in-app notifications (`com` schema) | |
| `TRAN_MENU_PRIV_PRC` (copy a role's screens and list grants to another role) | none | a "copy from role" action would cover it. |
| `gen_mssql_inserts` | package export (JSON) | |

## 4. Ours without a legacy counterpart

`entity`, `branch`, `org_unit`, `org_unit_type`, `position`, `user_assignment` (organization structure),
`privilege` (actions per screen), `user_privilege` and `user_privilege_grant` (personal privileges), `resource`
(screens and actions as their own rows), `data_object` and `data_scope_binding` (data permissions), `class_attribute`,
`holiday`, `setting`, `ui_text`, `app_branding`, `branding_asset`, `change_area` (approval policy), and the platform's
change sets, audit log and outbox.

## 5. What the legacy has that we do not

1. User picture and nick name; employee, ERP and BI user ids.
2. A user across several instances (`SYS_USERINSTANCES`).
3. Default company flag, default currency and system date per user and company.
4. Company default business unit, file folder, logo, watermark and abbreviation.
5. Per-user logins to external systems; per-user SMTP settings.
6. Class values per screen (`SYS_MENU_DOMAIN`).
7. Copy one role's screens and list grants to another (`TRAN_MENU_PRIV_PRC`).
8. `CLASS_INPUT_SOURCE`, `CLASS_CATEGORY` on classes; `CLASS_CODE_EMAIL_LIST` on apps.

By design we do **not** keep passwords, OTP codes, database or SMTP passwords in tables (legacy: `USERPWD`,
`PASSWORD_LARAVEL`, `PASSWORD_NET`, `OTPCODE`, `BI_USER_PWD`, `INSTDBPWD`, `DB_PSWD`, `SYSTEM_LOGIN_PWD`,
`EMAIL_SETTINGS.USER_PWD`): sign-in is Keycloak's and every other secret is in the secret store.

## 6. Decisions needed

1. **Case**: lower case (`apps.sys_users`, recommended: PostgreSQL needs no quotes) or UPPER case
   (`apps."SYS_USERS"`, every SQL text, list of values and report would have to quote names).
2. **Names**: as in section 2 (legacy names where they exist, plural `sys_` / `dd_` / `org_` / `bas_` otherwise),
   or plural everywhere (`sys_menus`, `dd_domains`).
3. **Columns**: keep ours (`name_ar/name_en`, `entity_code`, `company_code`, `is_active`, `display_order`) or move to
   the legacy words too (`_ntv/_frn`, `instid`, `cmp_num`…). Tables only is the smaller, safer change.
4. **Other modules**: Apps Control only, or every module (`svc_`, `wf_`, `dec_`… inside their schemas).
5. **Gaps in section 5**: which to add.

## 7. What a rename touches

One migration renaming the tables (`ALTER TABLE … RENAME`, data kept), the `ToTable(...)` lines in the Apps Control
model, nine raw SQL references (class views and two module classes), the demo data object that reads `apps.branch`, the
query builder's registered tables, and the handover documents. Change-set type keys such as `apps.domain` are names of
entity types, not tables, and do not change.

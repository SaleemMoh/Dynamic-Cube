# Legacy Apps Control (Oracle) ↔ Dynamic Cube Apps Control

How the Oracle Apps Control schema being replaced maps to our `apps` schema (PostgreSQL), which table names we would take
to follow its naming convention, and what each side has that the other lacks. Laravel framework tables and copies are
left out as asked: `CACHE*`, `*_EMPTY_*`, `*_BK*`, `JOBS`, `JOB_BATCHES`, `FAILED_JOBS`, `SESSIONS`,
`PASSWORD_RESET_TOKENS`, `ZZ_*`, `MIGRATIONS`.

Status: **done** for Apps Control (migration `LegacyTableAndColumnNames`). Decided: lower case, the legacy table and
column names where they exist, the same words where they do not, Apps Control only, no table for `SYS_MENU_DOMAIN`.
The other modules keep their names for now.

## 1. The naming convention

| | Legacy (Oracle) | Apps Control now (PostgreSQL) |
|---|---|---|
| Area prefix | `SYS_` security, navigation, installation, companies; `DD_` data dictionary; `BAS_` base masters | the same, plus `org_` for the organization structure, inside the `apps` schema |
| Case | UPPER_CASE | lower case (PostgreSQL folds unquoted names to lower case, so no quotes are needed) |
| Singular / plural | the legacy name as it is (`sys_menu`, `dd_domain`, `sys_users`) | the same; tables without a legacy one are plural |
| Arabic / English | `_NTV` / `_FRN` (`ROLE_DESC_NTV`) | the same (`role_desc_ntv`, `role_desc_frn`) |
| Instance / company | `INSTID`, `CMP_NUM`, `CMP_NUMS` | the same (`instid`, `cmp_num`, `cmp_nums`) |
| Order | `ORDERBY` (domain), `ORDER_NUM` (others) | the same |
| Keys | natural composite keys (`INSTID, CMP_NUM, ROLE_CODE`) | `id` (uuid) plus a unique code column named like the legacy one (`role_code`, `menu_code`) |
| Extra values | fixed `FLEX_VALUE_1..10`, `FLEX_DATE_1..5`, `FLEX_NUMBER_1..5` columns | typed attributes defined per class (`dd_class_attributes`), values in one `attributes` column |

Columns with no legacy counterpart keep their own snake_case name (`is_active`, `effective_from`, `owner`,
`created_at`…). The names live in one place, `src/Modules/AppsControl/Persistence/AppsControlNaming.cs`; the C# classes
and properties keep their names.

## 2. Tables and columns

Every Apps Control table, its old name, and the columns that changed. Shared platform tables keep their names:
`cfg.change_sets`, `cfg.change_items`, `audit.audit_log`, `platform.outbox_messages`. The class views keep
`apps.v_class_<code>` and their columns (`code`, `name_ar`, `name_en`, `display_order`, `is_active`…), so lists of
values, reports and API endpoints that read a class are unchanged.

| Table (was) | Columns renamed (was → now) |
|---|---|
| `sys_instance_profile` (`entity`) | `code`→`instid`, `entity_code`→`ref_instid`, `is_active`→`activeflag`, `logo_file_key`→`instlogo`, `name_ar`→`instname_ntv`, `name_en`→`instname_frn` |
| `sys_installation` (`installation`) | `code`→`install_code`, `entity_code`→`instid`, `name_ar`→`install_desc_ntv`, `name_en`→`install_desc_frn`, `system_type`→`systemtypeid` |
| `sys_company_profile` (`company`) | `code`→`cmp_num`, `display_order`→`order_num`, `entity_code`→`instid`, `is_active`→`active_flag`, `name_ar`→`cmp_des_ntv`, `name_en`→`cmp_des_frn` |
| `sys_company_branches` (`branch`) | `code`→`branch_code`, `company_code`→`cmp_num`, `display_order`→`order_num`, `entity_code`→`instid`, `name_ar`→`branch_desc_ntv`, `name_en`→`branch_desc_frn` |
| `sys_apps_profile` (`app`) | `code`→`appsid`, `display_order`→`order_num`, `entity_code`→`instid`, `is_active`→`active_flag`, `name_ar`→`apps_des_ntv`, `name_en`→`apps_des_frn` |
| `sys_business_apps` (`business_app`) | `code`→`business_app_code`, `companies`→`cmp_nums`, `description_ar`→`business_app_long_desc_ntv`, `description_en`→`business_app_long_desc_frn`, `display_order`→`order_num`, `entity_code`→`instid`, `name_ar`→`business_app_desc_ntv`, `name_en`→`business_app_desc_frn` |
| `sys_apps_branding` (`app_branding`) | `code`→`branding_code`, `entity_code`→`instid`, `name_ar`→`branding_desc_ntv`, `name_en`→`branding_desc_frn` |
| `sys_branding_assets` (`branding_asset`) | — |
| `sys_users` (`user_account`) | `code`→`userid`, `email`→`useremail`, `entity_code`→`instid`, `last_company_code`→`last_cmp_num`, `last_login_at`→`last_login_date`, `mobile`→`userphoneno`, `name_ar`→`usernamentv`, `name_en`→`usernamefrn` |
| `sys_roles` (`role`) | `code`→`role_code`, `entity_code`→`instid`, `name_ar`→`role_desc_ntv`, `name_en`→`role_desc_frn`, `override_companies`→`override_cmp_nums`, `reference_code`→`ref_code` |
| `sys_user_roles` (`user_role`) | `company_code`→`cmp_num` |
| `sys_resources` (`resource`) | `code`→`resource_code`, `entity_code`→`instid`, `name_ar`→`resource_desc_ntv`, `name_en`→`resource_desc_frn` |
| `sys_menu` (`menu`) | `code`→`menu_code`, `description_ar`→`menu_long_desc_ntv`, `description_en`→`menu_long_desc_frn`, `display_order`→`order_num`, `entity_code`→`instid`, `icon`→`menu_icon`, `is_visible`→`show_flag`, `name_ar`→`menu_desc_ntv`, `name_en`→`menu_desc_frn`, `parent_id`→`parent_menu_id` |
| `sys_menu_groups` (`menu_group`) | `code`→`menu_group_code`, `display_order`→`menu_group_order`, `entity_code`→`instid`, `icon`→`menu_icon`, `name_ar`→`menu_group_des_ntv`, `name_en`→`menu_group_des_frn` |
| `sys_role_menus` (`role_menu`) | `company_code`→`cmp_num` |
| `sys_role_privileges` (`privilege`) | `company_code`→`cmp_num` |
| `sys_role_menu_lovs` (`lov_grant`) | `company_code`→`cmp_num`, `default_value`→`default_value_code`, `granted_values`→`value_codes` |
| `sys_menu_lovs` (`lov_binding`) | — |
| `sys_user_privileges` (`user_privilege`) | `company_code`→`cmp_num` |
| `sys_user_privilege_lovs` (`user_privilege_grant`) | `granted_values`→`value_codes` |
| `sys_settings` (`setting`) | `code`→`setting_code`, `description`→`setting_long_desc`, `entity_code`→`instid`, `name_ar`→`setting_desc_ntv`, `name_en`→`setting_desc_frn` |
| `sys_ui_texts` (`ui_text`) | `code`→`text_code`, `entity_code`→`instid`, `name_ar`→`text_desc_ntv`, `name_en`→`text_desc_frn` |
| `sys_change_areas` (`change_area`) | `code`→`area_code`, `entity_code`→`instid`, `name_ar`→`area_desc_ntv`, `name_en`→`area_desc_frn` |
| `dd_classes` (`class`) | `allow_multi_assign`→`virtual_enable_multiple_assign`, `code`→`class_code`, `description_ar`→`class_long_desc_ntv`, `description_en`→`class_long_desc_frn`, `entity_code`→`instid`, `name_ar`→`class_desc_ntv`, `name_en`→`class_desc_frn`, `parent_class_id`→`reference_class_id` |
| `dd_class_attributes` (`class_attribute`) | `code`→`attribute_code`, `data_type`→`attribute_type`, `display_order`→`order_num`, `name_ar`→`attribute_label_ntv`, `name_en`→`attribute_label_frn` |
| `dd_domain` (`domain`) | `code`→`value`, `companies`→`cmp_nums`, `details_ar`→`domain_desc_detail_ntv`, `details_en`→`domain_desc_detail_frn`, `display_order`→`orderby`, `entity_code`→`instid`, `is_active`→`active_flag`, `name_ar`→`domain_desc_ntv`, `name_en`→`domain_desc_frn` |
| `dd_lovs` (`lov`) | `class_id`→`ref_class_id`, `code`→`lov_code`, `entity_code`→`instid`, `name_ar`→`lov_desc_ntv`, `name_en`→`lov_desc_frn`, `sql_text`→`sqlstmt`, `value_column`→`value_field` |
| `dd_data_objects` (`data_object`) | `code`→`object_code`, `entity_code`→`instid`, `name_ar`→`object_desc_ntv`, `name_en`→`object_desc_frn` |
| `dd_data_scope_bindings` (`data_scope_binding`) | `column_name`→`value_field` |
| `org_units` (`org_unit`) | `code`→`unit_code`, `entity_code`→`instid`, `name_ar`→`unit_desc_ntv`, `name_en`→`unit_desc_frn`, `parent_id`→`parent_unit_id` |
| `org_unit_types` (`org_unit_type`) | `code`→`unit_type_code`, `display_order`→`order_num`, `entity_code`→`instid`, `name_ar`→`unit_type_desc_ntv`, `name_en`→`unit_type_desc_frn` |
| `org_positions` (`position`) | `code`→`position_code`, `entity_code`→`instid`, `name_ar`→`position_desc_ntv`, `name_en`→`position_desc_frn` |
| `org_user_assignments` (`user_assignment`) | — |
| `bas_holidays` (`holiday`) | `code`→`holiday_code`, `entity_code`→`instid`, `name_ar`→`holiday_desc_ntv`, `name_en`→`holiday_desc_frn` |

## 3. Verification: each legacy table in our schema

| Legacy table | Ours | Notes |
|---|---|---|
| `SYS_INSTANCEPROFILE` | `sys_instance_profile` (entity) + `sys_installation` | `INSTID`, `INSTNAME_NTV/FRN`, `INSTLOGO`, `ACTIVEFLAG` on the entity; `SYSTEMTYPEID`, `COPYRIGHT` on the installation. **`INSTDBNAME`/`INSTDBPWD`/`INSTDBCONN` are not stored in a table**: connection strings are deployment settings and secrets live in the secret store. |
| `SYS_USERS` | `sys_users` | `USERID`, `USERNAMENTV/FRN`, `USEREMAIL`, `USERPHONENO`, `LAST_LOGIN_DATE`, `LAST_CMP_NUM` as in legacy; `ISADMIN` → a role. **Passwords, OTP, 2FA flag and `REMEMBER_TOKEN` are not stored**: sign-in, passwords and MFA are Keycloak's. Gaps: `USER_PICTURE`, `NICK_NAME_*`, `EMPID`, `ERPUSERID`, `BI_USER_ID`. |
| `SYS_USERINSTANCES` | none | one installation serves one entity per row (`instid` on the user); a user across several instances is not modelled. |
| `SYS_COMPANY_PROFILE` / `BAS_COMPANIES` | `sys_company_profile` | `CMP_NUM` (text here, number in legacy), `CMP_DES_NTV/FRN`, `ACTIVE_FLAG`. Gaps: `DEFAULT_BU`, `DEFAULT_FILE_FOLDER`, `LOGO_PATH`, `PATH_WMARK`, `CMP_ABBREVIATION` (branding is per app in `sys_apps_branding`). |
| `SYS_USERINSTANCES_COMPANIES` | `sys_user_roles.cmp_num` | a user's companies come from their roles; the last company chosen is `sys_users.last_cmp_num`. Gaps: default-company flag, `DEFAULT_CURRENCY`, `SYSTEM_DATE`. |
| `SYS_APPS_PROFILE` | `sys_apps_profile` (+ `sys_business_apps`) | `APPSID`, `APPS_DES_NTV/FRN`, `ACTIVE_FLAG`; `CLASS_CODE_EMAIL_LIST` not modelled. |
| `SYS_APPS_COMPANY` | `sys_business_apps.cmp_nums` | a list column instead of a link table. |
| `SYS_USER_APPS` (view) | worked out by the access resolver | from the user's roles, as the view does. |
| `DD_CLASSES` | `dd_classes` + `dd_class_attributes` | the 20 fixed flex slots and their label columns become typed attribute rows (no limit, types text/number/date/class/LOV…). `CLASS_TYPE` (system / company domain) → `values_per_company` (+ `owner`); `REFERENCE_CLASS_CODE` → `reference_class_id` (a key to the class); `VIRTUAL_REF_LOV_CODE` → an attribute of type LOV; `VIRTUAL_ENABLE_MULTIPLE_ASSIGN` as in legacy. Not carried: `CLASS_INPUT_SOURCE`, `CLASS_CATEGORY`. |
| `DD_DOMAIN` | `dd_domain` | `VALUE`, `DOMAIN_DESC_NTV/FRN`, `DOMAIN_DESC_DETAIL_NTV/FRN`, `ORDERBY`, `ACTIVE_FLAG` as in legacy; `FLEX_*` → `attributes`. |
| `DD_COMPANY_DOMAIN` | `dd_domain` with `cmp_nums` | one table for system and company values: a class with `values_per_company` lists the companies on each value (or "all"). |
| `DD_COMPANY_DOMAIN_VIRTUAL_VALUES` | `dd_domain.attributes` (LOV attribute, several values) | |
| `DD_LOVS` | `dd_lovs` | `LOV_CODE`, `SQLSTMT`, `VALUE_FIELD` as in legacy; `REF_CLASS_CODE` → `ref_class_id` (a key to the class). We add sources Data object and Query (builder). |
| `SYS_MENU` | `sys_menu` + `sys_resources` | `MENU_CODE`, `MENU_DESC_NTV/FRN`, `MENU_LONG_DESC_NTV/FRN`, `ORDER_NUM`, `MENU_ICON`, `SHOW_FLAG` as in legacy; `PARENT_MENU_CODE` → `parent_menu_id` (a key); `GENERAL_DETAIL_FLAG` → `item_type`; `OBJECT_NAME` → the screen in `sys_resources`; `MENU_LEVEL_NUM` is worked out from the parent. |
| `SYS_MENU_GROUPS` | `sys_menu_groups` | `MENU_GROUP_CODE`, `MENU_GROUP_DES_NTV/FRN`, `MENU_GROUP_ORDER`, `MENU_ICON`. |
| `SYS_MENU_LOVS` | `sys_menu_lovs` / `dd_data_scope_bindings` | which list filters a screen's data; `VALUE_FIELD` is the column it filters (`dd_data_scope_bindings.value_field`). |
| `SYS_ROLES` | `sys_roles` | `ROLE_CODE`, `ROLE_DESC_NTV/FRN`, `IS_ACTIVE`, `REF_CODE` as in legacy; per company: `override_cmp_nums` and `cmp_num` on `sys_user_roles`. |
| `SYS_ROLE_MENUS` | `sys_role_menus` + `sys_role_privileges` | legacy gives a whole screen; we also store its actions (view, create, edit…). |
| `SYS_ROLE_MENU_LOVS` | `sys_role_menu_lovs` | one row per value in legacy; one row per role/screen/list with `value_codes` and `default_value_code` here. |
| `SYS_USER_ROLES` | `sys_user_roles` | plus `branches`, `effective_from/to` and the org unit it came from. |
| `SYS_MENU_DOMAIN` | none | not needed (decided). |
| `SYS_SYSTEM_PROFILE` / `SYS_USER_SYSTEMS` | Integration Manager connections | external systems are connections with their secrets in the secret store; **per-user logins to other systems are not modelled**. |
| `EMAIL_SETTINGS` | Communication settings + secret store | one SMTP set-up per installation, not per user. |
| `USERS`, `AUTHENTICATABLE_USERS`, `SYS_COMPANY_USERS` | Keycloak + `sys_users` (+ party registry for portal users) | |
| `PERSONAL_ACCESS_TOKENS` | API clients (`integ` schema, secrets in the secret store) | |
| `NOTIFICATIONS` | in-app notifications (`com` schema) | |
| `TRAN_MENU_PRIV_PRC` (copy a role's screens and list grants to another role) | none | a "copy from role" action would cover it. |
| `gen_mssql_inserts` | package export (JSON) | |

## 4. Ours without a legacy counterpart

`sys_instance_profile` as its own entity row, `sys_company_branches`, `org_units`, `org_unit_types`, `org_positions`,
`org_user_assignments` (organization structure), `sys_role_privileges` (actions per screen), `sys_user_privileges` and
`sys_user_privilege_lovs` (personal privileges), `sys_resources` (screens and actions as their own rows),
`dd_data_objects` and `dd_data_scope_bindings` (data permissions), `dd_class_attributes`, `bas_holidays`,
`sys_settings`, `sys_ui_texts`, `sys_apps_branding`, `sys_branding_assets`, `sys_change_areas` (approval policy), and
the platform's change sets, audit log and outbox.

## 5. What the legacy has that we do not

1. User picture and nick name; employee, ERP and BI user ids.
2. A user across several instances (`SYS_USERINSTANCES`).
3. Default company flag, default currency and system date per user and company.
4. Company default business unit, file folder, logo, watermark and abbreviation.
5. Per-user logins to external systems; per-user SMTP settings.
6. Copy one role's screens and list grants to another (`TRAN_MENU_PRIV_PRC`).
7. `CLASS_INPUT_SOURCE`, `CLASS_CATEGORY` on classes; `CLASS_CODE_EMAIL_LIST` on apps.

Class values per screen (`SYS_MENU_DOMAIN`) are not needed. Which of the rest to add is still open.

By design we do **not** keep passwords, OTP codes, database or SMTP passwords in tables (legacy: `USERPWD`,
`PASSWORD_LARAVEL`, `PASSWORD_NET`, `OTPCODE`, `BI_USER_PWD`, `INSTDBPWD`, `DB_PSWD`, `SYSTEM_LOGIN_PWD`,
`EMAIL_SETTINGS.USER_PWD`): sign-in is Keycloak's and every other secret is in the secret store.

## 6. How the rename was made

- **One migration** (`src/Modules/AppsControl/Persistence/Migrations/…_LegacyTableAndColumnNames.cs`) renames the 34
  tables, their columns, keys and indexes in place (`ALTER TABLE … RENAME`): no data is copied or lost. It can be
  rolled back (`Down`).
- **Saved definitions move with it** (`LegacyNamesDataFix.cs`, same migration): a data object registered on a renamed
  table gets the new table and column names, and so do the lists of values, data-scope bindings, reports and API
  endpoints that read it (columns written `alias.column`). In the demo this is the *Branches* data object
  (`apps.sys_company_branches`) and what reads it: *My branches by region*, the request and task reports, the dashboard
  reports.
- **SQL written by hand** follows the new names: the class view generator (which keeps the view's column names), the
  org unit path update, the attribute clean-up, and the class moves of older migrations.
- **Not changed**: change-set entity type keys such as `apps.domain` or `apps.lov` (names of configuration types, not
  tables), the API, the screens, and the other modules' tables.

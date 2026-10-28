# Sample data: radiation registry

Sample configuration taken from the legacy radiation-sources registry's Excel exports (lookup sets, roles and
permissions, AMAN facilities), converted into Dynamic Cube [configuration packages](../../config-packages/README.md).
Each package becomes a **draft change set** that is submitted and approved like any other change.

Import in the internal system: **Apps Control → Change sets → Import package**, one file at a time **in the order
below**, approving each before importing the next (later packages refer to rows the earlier ones create).

| # | Package | Goes to | Contents |
|---|---|---|---|
| 1 | `01-lookups.json` | Apps Control → Classes & domains | 10 classes and 207 values: activity units (with the factor to becquerel as a typed number), yes/no, container sizes, gender, countries of manufacture, physical forms, provinces, regions, source statuses; the `MANUFACTURER` class |
| 2 | `02-manufacturers-1…4.json` | Apps Control → Classes & domains | The 1,542 manufacturers, in four parts (an import takes at most 500 items) |
| 3 | `03-roles-and-permissions.json` | Apps Control → Resources, Roles & privileges, Users | 40 screens with their actions, 12 roles with their privileges (the legacy role id is the role's reference code), 3 users with their roles |
| 4 | `04-facilities.json` | Apps Control → Classes & domains | Class `CITY` (under `REGION`) with the facilities' 6 cities, and class `FACILITY` with typed attributes (city, national address, phone, email, operational, AMAN ID, licences) and the 8 facilities |

## How the exports were mapped

- **Lookup sets → classes, terms → values.** Class codes are the set names in `UPPER_SNAKE` (`ActivityUnit` →
  `ACTIVITY_UNIT`). A value keeps its legacy value as code when every value of the set is short, valid and unique
  (statuses `c`, `d`…, countries `AE`…); otherwise the legacy term id is the code (manufacturers, container sizes,
  provinces). Every value keeps its legacy term id as external code `LEGACY`, for mapping old records when data is
  migrated. *Extra data* columns became typed attributes where they mean something (`BQ_FACTOR` on activity units;
  `EXTRA_DATA_1/2` kept as text on statuses) and were dropped where they repeat the name (manufacturers).
- **Screens → resources, permissions → actions.** Resources keep the legacy screen codes (`action-center`,
  `facility-profile`…) on the internal app. Permissions became action codes: `view`, `add`, `edit`, `delete`,
  `export`, `print` → `VIEW`, `CREATE`, `EDIT`, `DELETE`, `EXPORT`, `PRINT`; the rest in `UPPER_SNAKE`
  (`sources-review` → `SOURCES_REVIEW`, `acceptSources` → `ACCEPT_SOURCES`, `viewsourcesbutton` →
  `VIEW_SOURCES_BUTTON`). The permissions' Arabic/English labels are not stored: resources hold action codes only.
- **Roles and users** keep their legacy codes and user names. Users sign in through Keycloak with the same user
  name; the development realm only has `admin`, `maker` and `checker`, so add these users there to sign in as them.
- **Facilities → class `FACILITY`.** The Party Registry holds establishments by CR or unified number and has no
  facility (site) record yet, and the export carries no CR numbers, so facilities are reference values with typed
  attributes for now (also visible in the report view `apps.v_class_facility`). They move to the Party Registry
  when it gains facilities under establishments.

## Data fixed or left out on the way in

- Whitespace trimmed; the Arabic *لمنطقة الشرقية* corrected to *المنطقة الشرقية*.
- Manufacturers: one empty term and 14 duplicate names skipped (the generator prints them).
- Container sizes have no Arabic names in the export (English used for both); two sizes share the legacy value `K`.
- The *Admin* role (`r001`) had no Arabic name: *مسؤول النظام*. Province and Region sets had no Arabic names:
  *المنطقة* and *المنطقة الإدارية*.
- Facilities have no Arabic names (English used for both); the province column was empty (the city's region gives
  it); only one facility has an AMAN ID.

## Regenerating

```bash
pip install openpyxl
python3 deploy/sample-data/radiation-registry/generate.py lookup-sets.xlsx roles-and-permissions.xlsx aman-facilities.xlsx
```

The Apps Control API tests import and publish all the packages (`The_radiation_registry_sample_packages_import_and_publish`).

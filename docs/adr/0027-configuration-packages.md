# ADR-0027: Configuration packages are exported by code, imported as upserts and reviewed field by field

**Status:** Accepted

## Context
Configuration moves between environments: built in development, tested, then taken to production. Before this,
packages were written by hand from a template, and the import review showed only what each item carried. Phase 3d
asked for two things:
- Export from any screen.
- An import review that shows what would change.

## Decision
- **Export is a server endpoint.** `POST /api/apps-control/v1/admin/export` takes types in apply order, each with all
  its rows or chosen codes.
  - It returns a package that the import takes back as is: `{title, exportedAt, items}`. Each item is
    `{entityType, entityCode, operation: "UPSERT", payload}`.
  - Payloads are the same snapshots the admin screens read, without `code` and `owner`. Rows refer to each other by
    code, so a package works in any environment.
  - Viewing a type is enough to export it.
  - Rows maintained by the product (owner `SYSTEM`) come with every installation and are left out unless asked for.
  - Within a type, rows come in an order that can be applied:
    - parents before their children (classes, values, menus, resources, organization units)
    - versions in number order
  - A package carries at most 2000 items; the check and the import share the limit.
- **`UPSERT`** (or no operation) creates the row where it is missing and updates it where it exists. The check and
  the import resolve it against the target environment. `CREATE`, `UPDATE` and `DELETE` keep their meaning.
- **Unchanged rows are found by the server, not guessed.**
  - Each type reads the incoming payload as it would apply it: unknown properties dropped, missing ones at their
    defaults. It compares the result with the row as it is.
  - An update that changes nothing is reported as unchanged, and the import skips it. Re-importing an export
    therefore drafts nothing, and a package of mixed rows drafts only what differs.
- **The check returns both sides.** Each item carries `current` (the row now) and `incoming` (the row as the package
  would leave it). The review shows, field by field:
  - nested objects followed (`card.icon`)
  - lists of coded rows matched by code (`attributes[MIN_CAPITAL].nameEn`), other lists compared whole
  - images as pictures

  Counts (new, changed, delete, unchanged, problems) head the review, and unchanged items are hidden by default.
  Change-set details use the same comparison.
- **Related configuration is followed by the screens.**
  - The screens know each type's reference fields (the metadata that drives their editors). Exporting chosen rows
    adds what they refer to, round after round, until nothing new turns up.
  - Classes bring their values; services, workflows and rule sets bring their versions.
  - Types are ordered so that referenced types come first, with a fixed module order as the fallback.
  - References inside structured definitions (questions in a service version, roles in a workflow graph) are not
    followed. The import check reports any that are missing in the target.
- **Where to export from:**
  - Every configuration table: ticked rows or the whole type.
  - The classes, menus, roles, branding and organization screens.
  - *Change sets → Export configuration*: whole types across modules, grouped by change area.

## Consequences
- Moving configuration is export, import, review and approve. The usual maker-checker change sets still guard every
  change, one per area.
- Packages are plain JSON. They can be kept in version control next to `deploy/config-packages`, and the import
  check is a dry run usable from scripts.
- Branding images and other stored files are referenced by key. They do not travel in a package.
- A published version can only be re-imported unchanged. A different definition under the same number is refused,
  as before; the target needs the next version number instead.

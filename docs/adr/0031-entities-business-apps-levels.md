# ADR-0031: Entities, business apps and the level of every configuration row

**Status:** Accepted. Replaces "single entity per installation" in [04-apps-control](../04-apps-control.md) and
[01-vision-and-scope](../01-vision-and-scope.md).

## Context
An installation can serve more than one government entity (a ministry and its agencies), and an entity runs several
business lines, such as Classification, Permits or Licensing, each with its own services, workflows, document types,
roles and staff. Staff of one business line should see and change only its configuration and its work, while shared
rows (lists of values, settings, the organization) stay common. Until now there was one entity and nothing tied a row
to a business line. The "apps" that existed are channels (internal system, portal, field app), not business lines.

## Decision
- **Entities** are a list (`apps.entity`). The installation starts with `ENTITY`, the default entity, whose branding the
  apps show. The default entity cannot be deleted, nor an entity that still has business apps.
- **Business apps** (`apps.business-app`) belong to an entity (`entityCode`, the default entity when none is given) and
  have an icon, descriptions and an order. Channel apps (`apps.app`) stay as they are and show the business apps a user
  may work in.
- **Every configuration row has a level** through two kernel columns on `ConfigEntity`, in every module's tables:
  - `entity_code` and `business_app` both empty: **system** level, shared by the whole installation.
  - `entity_code` only: **entity** level.
  - `business_app` (with its entity filled from the app): **app** level.

  Payloads carry `entityCode` and `businessApp`. Both missing keeps a row's level; a new row then takes its maker's
  only entity and app (decided from the change set's maker, also when someone else publishes it). An empty string
  means none. The kernel contract `IConfigScopes` (Apps Control) checks that the entity and the app exist (also when
  created earlier in the same change set) and that the app belongs to the entity.
- **A user's scope**: their entity (the user row's `entityCode`; empty for every entity) and the business apps of
  their roles (every app when one of their roles has none). `ConfigApplier` lists, reads and exports only rows the user
  may see, and validates that rows they add, change or delete are within their scope: system rows need every entity,
  entity rows every app.
- **App filters**: configuration lists take `?app=` (that app's rows and the shared ones; users are listed under their
  roles' apps). Runtime lists take `app` too: requests (the app's services), Action Center tasks (through the service
  catalog), issued documents (the app's document types) and parties (with a request for one of the app's services).
- A service's workflow and its versions follow the service's level when the service is published.
- **Screens**: a header selector (`dc-business-app-selector`) chooses the business app; configuration tables show a
  *Level* column and pass the app, and new rows start in it; the generic editor has a *Level* section (entity, business
  app); the role editor chooses the role's business app; the Installation screen lists entities and business apps.

## Consequences
- Existing rows are system level and existing roles span every app, so installations without business apps behave as
  before.
- One database serves every entity; separating entities physically is not part of this change.
- Builders that save rows without a level (designer versions, service builder versions) keep the parent's visibility
  in practice: a version is listed under its service or workflow, and an app-limited maker's rows go to their app.
- Privileges stay global: the level narrows what is seen and changed, not which screens open.

## Update: rows of several business apps
Some configuration serves more than one business app of the same entity (a role for staff of two apps, a document type
issued by two apps). A row keeps its own app (`business_app`) and may list other apps of its entity it is shared with
(`shared_apps`, a `text[]` column on every configuration table). Payloads carry `businessApps` (the row's app first,
then the shared ones); `businessApp` alone still sets the row's own app and keeps its shared apps. A shared row is
listed under each of its apps and seen by staff of any of them; changing it needs all of them. A role of several apps
gives its holders all of them, and runtime app filters (services, requests, tasks, documents, parties) include shared
rows. The editor's *Level* section and the role editor choose several apps.


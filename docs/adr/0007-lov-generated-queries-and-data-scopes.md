# ADR-0007: LOV as generated queries with grants applied as data scopes

**Status:** Accepted

## Context
In the existing Apps Control, an LOV registers a SQL statement and acts as the security and
filter layer: roles are granted values per LOV and screen. Free SQL is powerful but risky
(privileges, injection, database-specific syntax, breaks on schema change, unsafe on the
external host), and grants only filtered dropdowns.

## Decision
- An LOV is a definition over a registered source: Class, Data Object (catalogued table/view),
  Integration operation, or restricted SQL (read-only role, `lov` schema views only, bound
  parameters, limits).
- The platform generates the query and returns value + Arabic + English display.
- Grants per role × LOV: `ALL`, `VALUES`, `RULE` (JSONLogic over user context), `NONE`;
  optional per-screen override; default value.
- `data_scope_binding` applies the same grants to data (inboxes, searches, reports, exports,
  APIs) through the query layer.

## Consequences
- Same concept as today, enforced consistently on the server.
- Rule-based grants remove per-user maintenance (e.g. "own region").
- Complex legacy SQL LOVs must be rewritten as views in the `lov` schema.

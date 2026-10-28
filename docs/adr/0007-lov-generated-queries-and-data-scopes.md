# ADR-0007: LOV as generated queries with grants applied as data scopes

**Status:** Accepted

## Context
In the existing Apps Control, an LOV registers a SQL statement and acts as the security and
filter layer: roles are granted values per LOV and screen. Free SQL is powerful but risky
(privileges, injection, database-specific syntax, breaks on schema change, unsafe on the
external host), and grants only filtered dropdowns.

## Decision
- An LOV is a definition over a registered source: Class, Data Object (catalogued table/view),
  Integration operation, or restricted SQL (read-only database role, bound parameters, limits).
- The platform generates the query and returns value + Arabic + English display.
- Grants per role × LOV: `ALL`, `VALUES`, `RULE` (JSONLogic over user context), `NONE`;
  optional per-screen override; default value.
- `data_scope_binding` applies the same grants to data (inboxes, searches, reports, exports,
  APIs) through the query layer.

## Consequences
- Same concept as today, enforced consistently on the server.
- Rule-based grants remove per-user maintenance (e.g. "own region").
- Existing SQL LOVs can be reused: the LOV names the query's value and label columns, and the query
  may read any table or view the read-only role is allowed to read (no separate schema needed).

## Refinement (Phase 1a implementation)
- Every grant **resolves to a value set** for the signed-in user — "all values" or an explicit
  list. A `RULE` is JSONLogic over the user context that *returns* the granted value(s) (a value,
  an array, `true` for all, `false`/null for none), e.g. `{"var": "user.orgUnitAttributes.REGION"}`.
  The resulting predicate is always `value IN (…)`, so one mechanism works for class, data-object
  and SQL sources, in memory and in SQL.
- Per role, a screen-specific grant replaces the global one; results are unioned across roles.
  Users with no grant on an LOV see all values unless the LOV is marked **requires grant**.
- LOVs are served on the external host only when marked **public**.
- Restricted SQL runs on a separate read-only connection (`AppsControl:LovSqlConnectionString`)
  in a read-only transaction with a statement timeout; it is disabled when that setting is empty,
  and editing SQL LOVs needs `EDIT` on `apps-control.lov-sql`.

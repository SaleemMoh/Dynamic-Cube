# ADR-0039: Lists of values built in a visual query builder

**Status:** Accepted (asked for by the owner; implemented). The owner chose: registered tables and classes only, and role
grants saved on the roles (Security approval).

## Context
A list of values came from one class, one registered data object (three column names typed by hand) or restricted SQL.
Joining two sources, filtering, sorting or limiting a list to the user's own data meant writing SQL, which needs a
separate privilege and a read-only database role. Which values each role sees was set on each role, away from the list.

## Decision
- **A new source type, `QUERY`.** The list keeps a query definition: a source (a class or a registered data object, with
  a short alias), joins (`INNER` or `LEFT`, on equal column pairs), the columns mapped to the value, the Arabic and
  English labels and the parent, a filter (conditions joined by all/any, with nested groups), sorts and *distinct*.
- **Only registered columns, only parameters.** Classes are read through their typed views (`apps.v_class_*`), keeping
  what a class list shows: active values, in effect today, serving the user's company. Data objects offer only their
  registered columns. The compiler checks every alias, column and comparison (by the column's type: text, number,
  date, yes/no; lists and JSON only for empty), quotes the names and binds every value. The query runs on the main
  connection inside a read-only transaction with a 5-second timeout. The user's grant on the value, and the data
  scopes of the data objects read, apply on top. A definition that no longer compiles (a source or column removed)
  shows no values rather than failing the screen.
- **Conditions on the user's data** compare a column with the signed-in user's company, branches (passing when they
  have every branch), unit, units, units below, user name, app or roles. They are kept apart from the filter and always
  joined to it with *and*, so a filter of *any* can never widen what a user sees. Portal visitors have none of these
  facts, so such conditions show them nothing.
- **The builder is the way in.** *New* and every list open it. A class or data object list opens converted (saving
  stores a query); an SQL list opens in its SQL editor, from which it can be rebuilt visually. The preview runs the
  definition as the administrator, without role grants, shows the first values and the SQL, and links each problem to
  its step. *Register a table* opens the data object editor; a table registered earlier in the same change set can be
  used by the list.
- **Role grants in the builder, saved on the roles.** The *Access* step lists each active role's own grant on the list
  (all values, some values, values from the user's data, nothing, or not set) and how many screen or company grants it
  also has. Changed roles are saved as role updates in the Security area, through its approval, after the list change.
  A new list must be approved before roles can be given access to it.

## Consequences
- Most lists no longer need restricted SQL; it stays for queries the builder cannot express.
- Saving role grants from the builder creates two changes in two areas (lists, security), each approved by its own
  approvers.
- Lists built visually still follow the registration rule: a table must be registered (and its columns chosen) before
  any list, including a public one, can read it.

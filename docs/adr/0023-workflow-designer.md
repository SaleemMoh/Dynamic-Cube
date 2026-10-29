# ADR-0023: The Workflow Designer edits the same definition the engine runs

**Status:** Accepted (Phase 3a)

## Context
Workflow versions were written as JSON in a text box, with a read-only drawing beside it. That works for the
platform team but not for the business analysts who own a service's processing. Phase 3 replaces hand-written
configuration with designers, and the first is the workflow's. Published versions are immutable and instances stay
on the version they started on, so the designer must not change how versions are stored or published.

## Decision
- **No second model.** The designer edits the workflow definition itself (`definition.nodes`), node for node; the
  JSON view shows exactly what is saved. The only addition is `definition.layout` (node code → x, y), which the
  engine ignores and the server validates (every key a node, coordinates on the canvas).
- **It always prepares the next version.** It opens a workflow's latest version, or any earlier one, as the
  starting point, and saves the result as `WORKFLOW/N+1` through the ordinary change set (maker-checker, or
  published at once where the area needs no approval). Nothing new is needed on the server to publish.
- **The palette offers what the engine runs**: human task, parallel split, join, gateway, decision, document,
  payment, end. Notification, integration and timer steps come with their runtime, not before.
- **Connections follow the node type.** Each node has handles for the targets it holds: next, a task's actions (a
  new action per connection, named approve or reject when it leads to such an end), a split's branches, a gateway's
  routes and its otherwise, a payment's paid and not-paid ways. Removing or renaming a step updates every reference
  to it.
- **Checks as you draw, the server's checks on demand.** The structural checks the server makes (one start, an end,
  targets that exist, reachability, assignments, actions, conditions) run in the browser on every change, listed
  with the step they concern; *Check on the server* runs the full publish validation through the package check
  without saving anything.
- **A visual JSONLogic editor** (shared, for later designers too): all / any of comparisons of a field with a value,
  or a field, text or number as a value; anything else stays JSON, checked by the same evaluator the server uses.
  Fields are suggested from the service's questions and levels, the workflow's tasks and their actions, decision
  results and bill statuses.
- **Test run in the browser.** Gateways are evaluated with the shared JSONLogic evaluator (identical to the
  server's), splits and joins behave as in the engine, and the person trying it chooses the task actions and
  whether the bill is paid. Rule sets are not evaluated and nothing is issued or billed.

## Consequences
- Versions drawn in the designer and versions imported from packages are the same thing; packages keep working and
  can carry layouts.
- Fixing a version's comparison on update: a stored definition is compared with the payload as definitions, so a
  version published before a field existed (e.g. `layout`, the payment fields) can still be retired.
- The test run cannot predict decisions that depend on rule-set results; a sample of those facts can be typed into
  the sample answers.

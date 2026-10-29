# ADR-0030: Routing tasks through the organization structure

**Status:** Accepted

## Context
A human task could go to a role (optionally within one fixed unit and position), a named employee or a previous actor.
Entities route by their structure: a request goes to the branch of its region, approval goes to the manager of the unit
that did the work, or to the manager one level up, and when a branch has no manager the one above approves. Fixing a
unit in the workflow does not cover any of these.

## Decision
- A new assignment target **`ORG_UNIT`**. The unit comes from one of three places (`unitFrom`):
  - `FIXED`: `orgUnit`.
  - `RULE`: `unitRule`, a JSONLogic rule over the request's facts that gives a unit code (e.g. `data.branch`, or a
    mapping from the region).
  - `ACTOR`: the unit of whoever completed `step`, or of whoever completed the last task when no step is named. An
    employee's primary unit counts first.
- `levelsUp` (0–10) moves from the found unit to the one above it, and so on.
- `position` narrows the task to the holders of that position *in* the unit (its manager…); without one, everyone in
  the unit may take it. `role` narrows it further.
- `includeSubUnits` also offers the task to the units below. `climbWhenEmpty` moves up one unit at a time while
  nobody qualifies (and is not excluded by maker-checker).
- The unit is resolved when the task is created and kept on the task: `candidateOrgUnit` (shown in inboxes) and
  `candidateOrgUnits` (the unit and, when included, the units below; `text[]`). Queues, claiming, distribution and
  automatic assignment match those units, with the position held in one of them. Existing tasks were backfilled
  with their single unit.
- A unit that cannot be found (a rule that gives an unknown code, an actor with no unit) leaves the task to the role
  alone; with no role either, only supervisors can assign it.
- The staff directory contract gives the org tree (`OrgUnitsAsync`) and each employee's placements (unit and position
  together), so Workflow needs no other access to Apps Control.

## Consequences
- The designer's assignment section offers *By the organization structure* with these options, and checks that a
  fixed unit or a rule is given.
- A change to the structure affects new tasks only; open tasks keep the units they were given (supervisors can
  reassign them).

# ADR-0029: Field visits as a module, with a workflow step, an offline field app and external inspection bodies

**Status:** Accepted

## Context
Many services need someone to look at a site before a decision: the entity's own inspectors, an external
inspection body (for example civil-defence safety inspections), or a visit made outside the platform whose result
staff type in. Inspectors work on phones, often where the connection is poor. The result must decide where the
request goes next, and what was found must stay with the request.

## Decision
- **A Visits module** (schema `vis`), like Payments: configuration in change sets, runtime records, and a kernel
  contract (`IVisits`, `IVisitListener`) so the workflow does not reference the module.
- **Visit types** (`vis.visit-type`) say who carries the visit out:
  - `FIELD`: the entity's inspectors, in the field app.
  - `EXTERNAL`: an inspection body, reached through the Integration Manager.
  - `MANUAL`: staff record the result.

  A type also lists the outcomes it can end with, each with a kind (`PASS`, `FAIL`, `OTHER`), and the fields the
  inspector fills in (text, notes, number, date, list, tick box). It sets the evidence required: a minimum number of
  photos, the inspector's position and the site representative's signature. It names the role whose holders can be
  assigned, and the days until the visit is due.
- **A visit** carries its number (`VIS-YYYY-000001`), the request it is for, the site (address and position),
  instructions, the inspector and date, its status (`PENDING`, `SCHEDULED`, `IN_PROGRESS`, `SENT`, `COMPLETED`,
  `CANCELLED`) and its result. The result is the outcome, the recorded values (jsonb), a report, photos (file
  storage), the signature and the captured position. Visits keep a history and raise notification events:
  `VISIT_REQUESTED`, `VISIT_SCHEDULED`, `VISIT_ASSIGNED`, `VISIT_COMPLETED` and `VISIT_CANCELLED`.
- **The workflow `VISIT` step** requests a visit of a type and waits.
  - The site comes from rules (for example a question of the request).
  - Each outcome can lead to its own step (`outcomes`); other outcomes take `next`, and a cancelled visit takes
    `cancelled`.
  - The outcome, its kind and the recorded values are kept as `visits.<step>`, so later gateways and documents can
    use them (for example `visits.SITE.data.area`).
  - Unlike a payment, the deadline keeps running: the visit is the entity's work, not the applicant's.
  - When the request no longer needs a visit (a join that does not wait for its branch, or the end of the
    workflow), the engine cancels it.
- **Staff** use *Field visits → Visits*: search and filters (status, type, overdue). They schedule a visit and
  assign it to a holder of the type's role, shown least busy first, record a result by hand, cancel with a reason
  (the request takes its cancelled way), or check an external result at once.
- **The field app is an offline-first PWA.** No `@angular/service-worker`: a small hand-written worker (`sw.js`)
  keeps the app shell and its settings; API calls always go to the network.
  - The inspector's visits, with each type's form, are kept in IndexedDB and refreshed when online.
  - The report being filled in is saved as a draft as it changes.
  - A finished report goes to an outbox and is sent as soon as there is a connection.
  - Each report carries a submission id, so a report sent twice is recorded once.
  - Photos are reduced on the device (at most 1600 px, JPEG) and checked on the server by their first bytes (JPEG,
    PNG, WebP; at most 8, 3 MB each).
  - The field app keeps its sign-in in local storage (`auth.storage: local`), so a session survives a restart without
    a connection. A failed renewal while offline does not drop the session.
- **External inspection bodies** are reached through the Integration Manager.
  - A *booking* operation receives exactly the inputs configured as rules over the visit and the request, and
    answers with the body's reference.
  - Results come in one of two ways: a *result* operation polled by the worker, or a signed notification to
    `/api/visits/v1/callbacks/{type}` on the external host (HMAC-SHA256 of the body with `Secrets:{name}:key`, as for
    payments).
  - One set of result paths (reference, status, finished value, outcome, findings, report) reads both, and an outcome
    map translates the body's values into outcome codes.

## Consequences
- A service gets a site inspection by adding a step to its workflow; nothing in Services changes. The request page,
  in the portal and internally, shows the request's visits.
- The field app works without a connection for a whole day of visits. It cannot *fetch* new visits offline, and a
  report the server refuses (for example the visit was reassigned) waits on the device until the inspector corrects
  or discards it.
- Inspectors' sessions persist on their devices; the device itself must be protected (screen lock, managed devices).
- Scheduling is manual. Automatic assignment, routes and calendars for inspectors, follow-up visits created from a
  result, and push notifications to the field app are not part of this change.

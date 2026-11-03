# ADR-0048: Reusable service building blocks

**Status:** Accepted (asked for by the owner after the NHCI review; implemented). The owner decided the missing service
features are built **as reusable parts of the platform, not for NHC only**: repeating rows (both typed in and picked from
the firm's records), staff inputs during review, one request with several items **each approved on its own**, packages
with a **fixed or formula** fee, and also acting for a firm on an authority, automation rules, a public calculator and
integration evidence on the request page.

## Context
The NHCI review (`docs/reviews/nhci-classification-review.md`, §5) listed what a classification service needs that the
platform did not have: tables of projects with files per row, a firm's projects registry imported from Etimad, review
fields the employee fills in (a credit rating) with the grade worked out again, several sectors in one request with a
result each, service packages with SLA, features and a capped fee formula, Balady and Najiz authorities, distribution
and reminder rules, a public calculator and the integration calls behind a request. Each is a general need of
government services, so each was built as configuration any service can use.

## Decision

### SF-1 Repeating rows (question type `ROWS`)
- A question of type *Repeating rows* has **columns** of their own type (text, number, date, choice or list of values,
  yes/no, files), each required, or required / shown **when a rule over the row's values** (on top of the answers) is
  true, and the **fewest and most rows**.
- The server keeps only shown columns, checks every row like a question (*Row 2, Approval letter: …*), and keeps a row's
  files under `QUESTION/COLUMN` (only the request's own files). Version rules can add rows up (`reduce`, `map`,
  `filter`).
- The shared dynamic form shows rows as a table with a form per row (the same form, so lists, files and rules work in a
  row); request pages show them as a table with file links.

### SF-2 Record lists of a party
- `party.record-list`: columns (text, number, date, yes/no, choice), whether the applicant adds records by hand, and an
  optional **import** (an operation called with the party's identifier, the items path, a key column and where each
  column is). A new import updates records by key and removes imported ones no longer returned; typed records stay.
- Portal *My records*; staff see a party's lists on its page. A rows question may name a record list: the portal offers
  *Pick from my records*, copying records into rows by column code.

### SF-3 Packages and formula fees
- Service levels carry an icon, *chosen at first*, a **recommend-when** rule and **features** compared across levels;
  new drafts start on the default level.
- Fees by formula or bracket are kept between a **minimum and a maximum**, with a **note** explaining how the fee is
  worked out. The portal shows level cards with features, the *recommended* badge and the fee.

### SF-4 Staff inputs on workflow tasks
- A human task declares **fields** (text, long text, number with least/most, date, tick box, choice) with a starting
  value rule. What the employee fills in is saved on the task while working and **recorded as
  `inputs.<task>.<field>`** when the task moves on; required fields are needed only for actions that move on.
- A task can name **decision steps to work out again**: the task page shows the recorded result beside the result with
  the inputs, and the new result is recorded on completion.

### SF-5 One request with several items
- A human task can list **items** from a rule (e.g. the project rows or the sectors of a request); the employee approves
  or rejects each, a rejection with a reason. Decisions are recorded as `items.<task>` (`list`, `approved`, `rejected`,
  `approvedItems`, counts) for later rules.
- The applicant sees each item's decision and reason: subject contract `IWorkflowSubject.ItemsDecidedAsync` keeps them
  on the request (`svc.request.item_results`).
- A document step can issue **one document per element** of a list (`forEach` / `forEachAs`), e.g. a certificate per
  approved item; `documents.<step>` then lists them.
- This answers the review's question 1: one request with a result per item (sector or project), not only bundles.

### SF-6a Acting for a firm on an authority
- `party.authority-type`: the relationship it records (a class value that allows acting), the firm's identifier, the
  number label, whether an expiry is needed, the services covered, and an optional **check operation** (inputs from the
  number, the firm, the applicant or the expiry; validity, expiry and firm-name outputs).
- The applicant adds an authority on the portal (*Authorizations*): it is checked with the provider, the firm is
  registered from the provider's data when unknown, and a dated relationship (source *Authority*) makes it an acting
  context in the header; adding it again renews it, and it can be ended.
- Requests record the acting relationship and authority (`TYPE:NUMBER`); rules read
  `applicant.relationship`, `applicant.authority` and `applicant.authorityNumber`; the staff request page shows them.

### SF-6b Automation rules for tasks
- `wf.automation-rule`: the workflows and steps it watches (empty: all), a JSONLogic condition over the task
  (`task.step`, `workflow`, `service`, `role`, `assigned`, `ageHours`, `unassignedHours`, `assignedHours`,
  `dueInHours`, `overdue`, `sla`, `escalated`) and an action: **assign to the least loaded employee** (unassigned tasks
  only), **remind**, or **escalate to a role**.
- The worker runs the active rules every minute; each action is taken under the instance lock (the task is read again,
  so one that moved on is left alone) and written to the **automation log**, the task's events (`ASSIGNED`, `REMINDED`,
  `ESCALATED`, with the rule's code) and the notifications `TASK_ASSIGNED`, `TASK_REMINDER`, `TASK_ESCALATED`.
- A rule acts **once per task**, or again after its **repeat hours**; a failed or skipped try is retried after an hour,
  not every minute.
- **Test**: a rule's open tasks now and what it would do, without changing anything (`POST /automation/test`); log:
  `GET /automation/log`.

### SF-6c Calculator page
- A **public** rule set can carry a calculator (`dec.rule_set.calculator`): titles, intro, icon, fields (number, text,
  choice, tick box, date; hints, ranges, choices), the results to show (text, number, amount, yes/no, one main result)
  and a note.
- A field can start from the signed-in firm's data (a prefill rule over the party's facts, e.g.
  `party.profile.CR_CAPITAL`). Only the calculator's own fields reach the rule set.
- Portal *Calculators*; public endpoints list, read and calculate; prefill needs a sign-in.

### SF-6d Integration evidence on the request page
- The gateway tags each logged call with the **subject** it was made for (set around request and workflow calls);
  kernel contract `IIntegrationEvidence` lists a subject's calls with masked request and response. Cache hits log the
  cached value, so the evidence shows the value used.
- The staff request page and task page show the calls, cached repeats behind a toggle.

## Consequences
- The classification service of the review can be configured without code, except the classification engine itself
  (the review's CE-1 / CE-2), which stays a separate decision.
- Every part goes through change sets and configuration packages like the rest of the configuration.
- New migrations: `svc` (rows questions and the record list they pick from, request item results, the acting
  relationship and authority), `party` (record lists, authority types), `wf` (task inputs, item decisions, automation
  rules and log), `dec` (calculator), `integ` (call subject). Packages and formula fees live in the version definition
  and needed none.
- NHC demo packages 28–36 show each part on the *Projects and experience statement* service, the capacity calculator, the
  Balady / Najiz authorities and three classification automation rules (see `deploy/demo-data/README.md`).

Later: automation actions beyond tasks (closing abandoned drafts, reminding applicants), a rules test against an unsaved
rule from its editor, record lists shared between firms of one group, the classification engine (review §6).

# ADR-0046: Sub-requests — prerequisites, bundled services and requests a workflow opens

**Status:** Accepted (roadmap slice 3f; asked for by the owner; implemented). The owner decided: build **all three**
kinds; prerequisites are **configurable** per service version; a workflow that opens a request may **wait for it or carry
on**, chosen per step.

## Context
Some services depend on others. An establishment can register as a supplier only with a valid classification
certificate; a registration can be asked for together with an alerts subscription; a reviewer's approval may need a
financial statement the applicant has to provide as a request of its own. Until now each request stood alone: eligibility
rules saw only the applicant's profile, and nothing linked one request to another.

## Decision

### Prerequisites (service version → *Rules* → *Prerequisites*)
- Each prerequisite has a code, a kind and its Arabic and English message:
  - **Request** of a service, in chosen statuses (APPROVED when none are chosen), made or closed within N days if set.
  - **Document** of a type, in chosen statuses (ACTIVE when none are chosen), still valid for N more days if set.
- It may name the **service to start** to meet it (for a request, the required service unless another is given). The
  portal shows the message with a *Start that service* link.
- Prerequisites are checked when the applicant opens the service, starts a draft and submits. A failure is reported as
  `PREREQUISITE_{code}`, like any eligibility failure.

### Bundled services (service version → *Rules* → *Bundled services*)
- A version lists other services asked for **with** it: *required* ones always go, optional ones are ticked by the
  applicant on the draft's page. Each may carry a note.
- On submit, every bundled service the applicant gets (required ones and the ticked ones) must be open to them: its own
  prerequisites and eligibility are checked first, and a failure stops the submission as
  `BUNDLE_{service}_{code}`.
- After the main request is submitted, one request is made per bundled service. Its answers come from its own prefill
  and from the main request's answers to the **questions they share**. When nothing is missing (no errors, the level
  known, no declaration, no Nafath confirmation, no integration step to run), it is **submitted** and its workflow
  starts; otherwise it is kept as a **draft** for the applicant to complete, who is notified (`REQUEST_OPENED`).

### Requests a workflow opens (workflow step `SUB_REQUEST`)
- The step names the **child service**, whether to **submit** it straight away when complete, extra **values** for its
  answers (rules over the facts), and whether to **wait**:
  - **Wait:** the request shows *Awaiting a sub-request* (status `PENDING_EXTERNAL`). When the child is **approved** the workflow goes to *next*; when it is rejected, cancelled or expires it goes to
    *failed* (required when waiting).
  - **Carry on:** the workflow goes to *next* at once; the child runs on its own.
- The child's number is in the facts as `subrequests.<step>.requestId / number / status` for later rules. The child
  belongs to the same applicant and beneficiary; its answers are filled as for bundled services.
- The child is made **after** the parent's step is saved (in the same unit of work), so a child that is approved at
  once (a workflow with no review) moves the parent on straight away.
- The child's request never waits on its parent; only a parent waits, and only on children it opened with *wait*.

### Links
- A request keeps its parent (`parent_request_id`), how it was made (`Bundled` or `Spawned`) and the parent's step.
- The request pages (portal and internal) show **Linked requests**: the request it was made from, and the ones bundled
  with it or opened by its steps, with their status and a link. A draft is marked *Draft to complete*.

### Contracts
- `IRequestSpawner` (Services) makes a child request for another module; `IChildRequestListener` (Workflow) is told when
  a spawned child closes. Neither module references the other.

## Consequences
- The Service Builder's *Rules* tab gains *Prerequisites* and *Bundled services*; the workflow designer gains the
  *Sub-request* step (handles *child approved* and *child not approved* when waiting), and its test run lets you pick the
  child's outcome.
- Publishing checks that every linked service exists (or is created in the same change set), that a service does not
  require or bundle itself, that a bundled service is not bound to a document, and that document types exist.
- NHC demo package `26-sub-requests`: *Supplier registration* needs a classification certificate valid for 30 more days
  (start: *New classification certificate*), offers *Tender alerts subscription* as an optional bundled service
  (approved at once), and its workflow, after the branch manager's review, opens a *Supplier financial statement* the
  establishment completes and the financial analyst reviews; the registration is approved when the statement is.

Later: a prerequisite on the applicant's own profile fields as a rule, several children from one step (one per row of
a list), cancelling waiting children when the parent is cancelled, and a bundle option chosen per level.

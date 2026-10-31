# ADR-0038: Integration result card, draft refresh, autosave and the request's place in its workflow

**Status:** Accepted (asked for by the owner; implemented). The "document scanner" in the same request is not built yet:
the owner meant reading a document's contents into answers, which needs their choices first (see *Open*).

## Context
An integration step in the service form ([ADR-0037](0037-archetypes-question-fill-integration-steps.md)) ran only when
the applicant moved past it, and the applicant saw a line of text and the answers it filled, not what the provider said.
A draft kept the answers fetched when it was saved, even if the provider's data changed before the applicant came back.
Answers were saved only when the applicant moved between steps, so closing the page by accident lost what was typed on
the current step. Staff saw a request's workflow as a list of tasks and a timeline, not where the request stands in the
workflow's design.

## Decision
- **Result card.** An integration step has *show the result* (default on) and *values to show*: rows of an output (an
  operation output, or a path in the provider's result) with an Arabic and English label, set in the Service Builder's
  step panel (*Result card*). The step is called **when the applicant arrives on it**, and the card shows success or
  failure in colour, the provider's message, when it was checked, the chosen values (or, with none chosen, the answers
  the step filled) and *Check again* / *Try again*. Moving on after a success does not call it again; moving on after a
  failure, or *Try again*, does. Each call is kept on the request (`integrations.<step>`: success, code, messages, time,
  `values`). A required step that failed still stops the applicant, and submission checks it again.
- **A draft reads its integrations again when opened.** Opening a draft (or a request returned for correction) fills the
  locked integration answers again and calls again each integration step already called. Answers that changed are saved
  and returned as `changes` (question, labels, from, to). The wizard shows them in a notice above the steps ("Some of your
  data has changed": label, old value struck through → new value), until the applicant closes it. Answers the applicant
  may change are not overwritten.
- **Autosave.** A moment (1.5 s) after each change on a form step, or a change of service level, the portal saves the
  answers as they are: no step is checked and the current step stays. When the page is hidden or left, a waiting change
  is saved at once (a request that may outlive the page). Saves of one draft never overlap: moving between steps drops a
  waiting autosave (the move saves the same answers) and waits for one already running. Under the wizard: *Saving the
  draft…*, *Draft saved HH:mm*, or a red line when it failed (the next change tries again). Going back a step keeps what
  was typed on the step being left.
- **Where the request is.** A workflow instance records every step its tokens reach (`visited_nodes`, in the order
  first reached), including starts, gateways, splits, joins, automatic steps and the end. The Action Center's instance
  view returns the version's definition, the steps the request is at now (live tokens: open tasks, waiting at a join, for
  the applicant, a payment or a visit) and the steps it went through. The staff request page draws the workflow (*Where
  the request is in its workflow*): the current step outlined in blue with a soft pulse, steps done with a green tick,
  arrows taken in green, and a legend. Instances started before this change fall back to the steps named by their events
  and tasks.

## Consequences
- Integration steps are called more often: on arrival, on *Try again*, and each time a draft is opened. Calls are logged
  and cached by the Integration Manager as before; a provider with strict quotas should have caching on its operation.
- An autosave that fails is not retried until the next change; the move to the next step saves everything anyway.
- `wf.instance.visited_nodes` is added by a migration (empty for existing instances).

## Open
- **Document reading** (the owner's "document scanner"): on a file question, read the uploaded document's contents and
  fill answers from it. Undecided: the reading engine (self-hosted OCR, an Integration Manager operation to an external
  reader, or an AI model), how fields are mapped to answers, and whether the applicant must confirm what was read. A
  camera scanner built first by mistake was taken out; its code is kept outside the repository in case it is wanted.

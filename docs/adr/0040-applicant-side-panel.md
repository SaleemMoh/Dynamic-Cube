# ADR-0040: The applicant's side panel — assistant, readiness check, request summary and live result

**Status:** Accepted (asked for by the owner; implemented). The owner asked for the four sections of the classification
reference (*وكيل التصنيف*, readiness check, request summary, live result) as reusable parts any service built in the
Service Builder can use. A configurable classification engine for any business (not only NHC) is **not now**: it is
planned for later, and this panel works with today's rule sets so it can show that engine's results when it comes.

## Context
The classification reference shows, beside the applicant's form, an assistant that explains each step, a readiness
check, a summary of the request and the grade as it would be worked out from the answers given so far. Dynamic Cube
builds every service from the same parts (questions, steps, rules, a workflow with decision steps), so these sections
had to be configured per service version, not written for one service.

## Decision
- **An optional `panel` on the service version** with four parts, each turned on separately: `assistant`, `readiness`,
  `summary` and `result`. It is part of the version (published with it, checked with it, carried by packages) and is
  edited in a new *Side panel* tab of the Service Builder; the preview tab shows it beside the wizard.
- **Assistant.** A title and subtitle (e.g. *وكيل التصنيف*), and lines of six kinds: the step's greeting (*MESSAGE*, typed
  out), notes (*MANDATORY*, *CONDITIONAL*, *IMPACT*, *ADVICE*, each with its rule under *Why?*) and questions with
  their answers (*QUESTION*, offered as chips). A line belongs to one step or to every step, and may have a JSONLogic
  condition over `data` (the answers on screen), `level`, `step`, `readiness` (%), `missing` and `result` (the live
  result's outputs). Texts may use `{missing}`, `{readiness}` and `{service}`. The applicant can also type a question:
  it is matched to the closest configured question by shared words (a word of the question weighs 3, of the answer 1,
  at least 3 to answer; Arabic marks, letter forms and the article are ignored); nothing is sent to an AI service.
  *Automatic notes* (on by default) add what the step still lacks, what may become required, its optional uploads and
  the problems the server reported.
- **Readiness check** — worked out in the browser from the same rules the wizard and the server apply: each question
  shown for the answers (asked once, where it first shows) that is required now is complete or missing (a pattern not
  matched counts as missing); one required only for other answers is *conditional*; optional uploads are listed but not
  counted; a required integration step counts until its call succeeds; the service level counts when the version has a
  level step. The share is complete ÷ (complete + missing). Open items are grouped by step; a group opens its step
  (going back keeps what was typed; going on checks the step being left, like *Next*).
- **Request summary** — the service, version, request number, level, answers given of those shown, attachments, fees,
  the live result's headline and the answers of the questions chosen in the builder.
- **Live result** — the decision step of the service's workflow (its rule set and its input mapping), evaluated on the
  answers on screen through a new `IDecisionService.PreviewAsync` that records nothing. The portal calls
  `POST /requests/{id}/live-result` a moment (450 ms) after the answers change, the latest answers winning; only drafts
  and requests returned for correction may, and only when the version turns the result on. The builder's preview calls
  `POST /admin/live-result` with sample answers. The panel shows a headline on a **scale** (the values a result takes,
  best first, with their names, drawn as a ladder; a value off the scale shows the *empty result* name, e.g. *غير مصنف*),
  other values (an Arabic and an English output each when the texts differ) and, for rule sets that output a list,
  **a card per element** (e.g. per sector) with its result on the scale and its columns. It is labelled as an estimate,
  not the decision; once submitted, the workflow decides as before and its evaluation is recorded.
- **Where it shows.** The portal's request page, while the applicant edits the request (draft or returned): the
  readiness check above the wizard, the assistant, summary and live result in the side column, stacked on narrow
  screens. Every component is shared (`dc-panel-assistant`, `dc-panel-readiness`, `dc-panel-summary`,
  `dc-panel-result`, `dc-service-panel`), so other channels can use them.

## Consequences
- Any service gets the panel by turning parts on in the builder; a service without one is unchanged.
- The live result uses the workflow's latest published version; a service whose workflow has no decision step can use
  the other three parts. The input mapping of the decision step must read the answers (`data.…`), which are the only
  facts on screen besides the level and the service (no party facts or earlier decisions).
- The NHC demo adds *version 2* of the new-classification service with the panel (`17-classification-panel.json`):
  greetings and notes for each step, five questions with answers, and the grading rule set's best grade on the 1–5
  scale with a card per sector (technical, performance, governance and financial grades).
- Later: the configurable classification engine; an assistant backed by a language model, if the owner wants one, would
  plug in behind the same lines and matching.

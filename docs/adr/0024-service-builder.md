# ADR-0024: The Service Builder edits the version definition, and layout joins the runtime first

**Status:** Accepted (Phase 3b)

## Context
A service version was authored as JSON: levels, wizard steps of sections of bank questions with their show/require
rules, eligibility, cross-field checks, fees, availability and the portal card. The roadmap's Service & Form Builder
asks for that without JSON, plus form layouts (tabs, accordion, columns, field widths), a live preview, required
documents, output and related services. Some of these had no runtime yet: the form renderer laid questions out on an
automatic grid only, attachments cannot be uploaded, and related services (prerequisite, alternative) are not modelled.

## Decision
- **Layout is part of the version, rendered by the runtime before the builder offers it.** A form step has a
  `display` (STACKED, TABS, ACCORDION), a section `columns` (1–3) and a question use a `width` (FULL, HALF, THIRD). The
  shared form renderer lays sections on six tracks so one, two or three columns and half / third widths line up; a
  form narrower than 600px (a phone, a side panel) shows one column, by a container query so a preview in a phone frame
  behaves like a phone. On submit, the tab or panel of the first problem is opened. Versions without layout look as
  before.
- **No second model.** The builder edits the version definition the server stores, field for field, and saves the
  next version through the ordinary change set; the JSON view shows exactly what is saved.
- **One designer per service, in tabs:** Form (steps, sections and bank questions placed by drag and drop, the
  inspector for each, conditions with the shared visual rule editor), Rules & fees (levels, eligibility, cross-field
  checks, fees as a fixed amount, a formula or a bracket table, VAT, the availability window of document services),
  Service card (with a preview of the portal page), Workflow (read from the service's own workflow: review tasks,
  documents issued, decision rules, payments, outcomes, and the way to the Workflow Designer), Preview (the applicant's
  wizard resolved on the server as it is now, desktop or phone) and JSON.
- **Rules are offered the variables they really see**: section and question rules the answers by question code;
  eligibility, checks and fees `data.*`, the level, the beneficiary's facts (`party.*`, profile fields), today and the
  document of a follow-up request.
- **Bank questions stay shared.** The builder lists the bank, creates or edits a question through the bank's own
  editor (a change set like any other) and reads the bank again; it never copies a question into a version.
- **Output is the workflow's.** What a service produces (documents, decisions, bills, notifications) is configured in
  its workflow and shown in the builder read-only, not duplicated in the version.
- **Not yet:** required documents are described on the card but not uploaded (attachments need a file runtime first),
  and related services beyond follow-ups on a document wait for their runtime.

## Consequences
- Services built in the builder and services imported from packages are the same thing; packages can carry layouts.
- The service applier compares a published version with its update as definitions, so versions published before the
  card, availability or layout fields existed can still be retired.
- The preview shows the server's validation message while the version cannot be resolved (e.g. an incomplete card
  step); the builder's own problem list says the same thing first.

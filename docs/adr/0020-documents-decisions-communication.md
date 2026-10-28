# ADR-0020: Issued documents, decision rules and notifications

**Status:** Accepted (implements [ADR-0014](0014-pluggable-decision-engine.md); refines [ADR-0009](0009-outbox-no-message-broker.md) for notifications)

## Context
Phase 1e closes the classification journey: the grade is calculated, the general manager approves, a
certificate is issued that anyone can verify, and applicants and staff are told what happened. Three modules
take part (Documents, Decisions, Communication) and they are reached from Workflow and Services, which must not
depend on them directly.

## Decision
- **Kernel contracts, module implementations.** `IDocumentIssuer`, `IDecisionService` / `IDecisionEngine` and
  `CommunicationEvent` live in the kernel. The workflow resolves the issuer and the decision service only when a
  DOCUMENT or DECISION step runs, so a host without those modules still starts.
- **Documents** (schema `doc`): types and print templates are configuration; an issued document has a number
  (`PREFIX-YYYY-000001`, an upsert sequence per prefix and year), a random 20-character verification code and
  immutable versions (data, line items and the holder as they were). A one-per-holder type is *amended* by a
  second issue: same number, new version. Templates are HTML with Liquid (Fluid), printed by **Gotenberg** with
  IBM Plex Sans Arabic bundled; the worker prints after issuing (outbox), the APIs print on first download and
  again if the stored file is missing. Verification is anonymous and shows only fields marked public.
- **Decisions** (schema `dec`): rule sets name an engine; versions (`CODE/N`) are checked by that engine and never
  change once published; the latest version that is not retired is used. The built-in `RULES` engine evaluates
  named JSONLogic variables, per-item variables and outputs (e.g. per sector) and an overall output, recording
  every step in a trace. Every evaluation is logged with input, output and trace; *try* runs are logged as `TRY`;
  public rule sets answer an anonymous calculator without logging. Other engines register as services.
- **Workflow steps:** a DECISION node maps request facts to the input and keeps the output as
  `decisions.<result>` in the instance's facts, where gateways and later steps read it; a DOCUMENT node issues
  to a holder (the request's party by default) with data and line maps (lines from a list, e.g. the classified
  sectors) and keeps `documents.<node>`. Both record a timeline event.
- **Notifications are events plus rules.** Modules raise `CommunicationEvent`s in the same transaction as the
  change (the outbox), each with facts for templates and conditions; event types are registered with a sample
  of their facts, against which rule conditions and templates are tried when they are published. A rule names
  an event, a JSONLogic condition, a template, channels and recipients (the party, users or roles, each a rule
  over the facts). Dispatch is idempotent per event; in-app notifications are written at once, email (SMTP via
  MailKit) and SMS (an Integration Manager operation) go through the outbox with retries; missing addresses and
  unconfigured channels are recorded as *skipped*. Party addresses come from profile fields (`EMAIL`, `MOBILE`
  by default), staff addresses from their accounts.
- **Document dates** are a worker job: documents past their date expire, and holders are reminded once on each
  reminder day of the type (a late run sends only the nearest reminder).

## Consequences
- Adding a channel, an engine or an event type is a registration, not a change to the dispatcher or the engine.
- Notifications never block the change that caused them; delivery problems show in the message log, where a
  failed message can be sent again.
- The classification grading shipped in the package is a simplified reading of the guide (§6.5); the full
  band tables, the financial axis and the adjusters belong in a dedicated engine or a later rule-set version.

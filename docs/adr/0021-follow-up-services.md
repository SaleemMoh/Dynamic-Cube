# ADR-0021: Follow-up services act on issued documents

**Status:** Accepted (Phase 2a)

## Context
Most follow-up services of the reference service (renewal, extension, modification, appeal) are not new
applications: they change a certificate the establishment already holds, and each is open only at certain times in
the certificate's life (renewal within the 90 days before expiry, extension until 90 days after it, appeal within 60
days of issue, upgrades 90 days after issue). The Services module owns requests; the Documents module owns issued
documents; the Workflow module runs processing. None may reach into another's tables.

## Decision
- **A service names the document type it acts on** (`documentType`). Its requests are made for one of the
  beneficiary's documents of that type, recorded on the request (`sourceDocumentId`, `sourceDocumentNumber`).
- **Availability is part of the service version**: accepted document statuses and a window of days from the issue
  or expiry date. Versions are immutable, so a request keeps the rules it started under; the window is checked when
  the draft starts, when listing, and again on submission.
- **One open request per document and service.** A second one is refused with the open request's number, and the
  portal offers to continue it instead.
- **Documents are read through a kernel contract** (`IDocumentDirectory`: a document, a holder's documents of a type,
  a type's names). Rules, prefill and the workflow see the document as `document` (number, status, dates, data,
  lines).
- **Amendments go through the workflow**, not the service: a document step with `target: SOURCE` amends the request's
  document through `IDocumentIssuer`, with a validity change (KEEP, RESET, RENEW, EXTEND). Renewing or extending an
  expired document makes it active again; suspended and cancelled documents cannot be amended.

## Consequences
- Renewal, extension and later follow-ups are configuration: a service, a version with its window, and a workflow
  version that amends the document; packages carry them.
- What a follow-up changes is decided by its workflow (data fields, lines, validity), so a modification can add a
  sector line while a renewal only moves the date.
- The portal can show every follow-up service of a document as available or locked with dates, without knowing the
  services in advance.

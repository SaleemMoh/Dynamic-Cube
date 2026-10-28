# ADR-0008: Versioned configuration; instances pinned to their version

**Status:** Accepted

## Context
Services, forms, workflows, templates and rule sets change while requests are in flight.
Requests must finish under the rules they started with (e.g. "bound to the published guide
version").

## Decision
Versioned configuration follows draft → published (immutable) → retired. Drafts, requests and
workflow instances store the version id they use. New versions never alter running instances;
per service, existing drafts either continue on the old version (default) or restart.

## Consequences
- Predictable behaviour and auditability.
- Several versions of a service may run concurrently; reports group by service, not version.

## Implementation notes
- Service versions are configuration rows keyed `SERVICE/N`: a draft version is an item of a draft change
  set; publishing makes it immutable and stores it with snapshots of the questions it uses, so later edits
  of the question bank never reach running requests (1c).
- Requests store the version id; drafts of services with the *restart* policy move to the newest version
  when opened, keeping the answers to questions it still asks.

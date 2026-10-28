# ADR-0006: Permissive open-source licenses only

**Status:** Accepted

## Context
The product is redistributed to many government customers on-prem. Several popular .NET
libraries moved to commercial licenses (MediatR, AutoMapper, MassTransit v9, FluentAssertions
v8) or revenue-based licenses (QuestPDF, Duende IdentityServer). In 2026 PrimeNG moved to the commercial
PrimeUI License, including the latest patches of older major versions (see ADR-0015).

## Decision
Use only MIT, Apache-2.0, BSD, PostgreSQL or similarly permissive dependencies. GPL components
are allowed only as separate processes that are not linked (e.g. ClamAV). Every new dependency's
license is checked in review.

## Consequences
- Some conveniences are written by hand (mapping, in-process dispatch).
- No licensing surprises for customers.

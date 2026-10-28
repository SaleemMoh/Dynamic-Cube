# ADR-0004: Separate API hosts for internal and external zones

**Status:** Accepted

## Context
The internal system must be reachable only from the entity network; the external system is on
the internet.

## Decision
Two host projects, `Internal.Api` and `External.Api`, built from the same modules. Each
registers only its zone's endpoints. Separate database roles with least privilege. Uploads from
the internet are virus-scanned (ClamAV).

## Consequences
- The internet-facing host contains no admin/builder/employee code paths.
- Some endpoint duplication is avoided by sharing module application services.
- Optional later mode: external host calls the internal API instead of the database.

# ADR-0002: PostgreSQL with JSONB payloads, promoted columns and generated views

**Status:** Accepted

## Context
Service forms are dynamic; lookups need extensible attributes; reports must be easy to build
and fast. EAV models perform poorly; generating DDL per service complicates migrations.

## Decision
- PostgreSQL as the only database.
- Request payloads and Class/Domain attributes in **JSONB** with GIN indexes.
- Frequently queried fields **promoted** to real columns.
- The platform generates **typed views** (`v_class_<code>`, request datasets) for reporting.
- `ltree` for org-unit hierarchies.

## Consequences
- Flexible schema without EAV; reporting sees normal typed columns.
- Promoted-column and view generation must be part of publishing configuration.
- Supporting other databases is out of scope.

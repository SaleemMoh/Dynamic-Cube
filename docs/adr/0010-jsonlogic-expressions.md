# ADR-0010: JSONLogic as the shared expression format

**Status:** Accepted

## Context
Conditions appear everywhere: field visibility, validations, routing, eligibility,
availability windows, LOV grant rules, notification conditions. They must run in the browser
(instant feedback) and on the server (authoritative), and be produced by visual builders.

## Decision
Store conditions as JSONLogic (MIT libraries for .NET and JavaScript), with platform-provided
custom operations (dates/working days, lookups, user context). Advanced script steps use a
sandboxed JavaScript engine (Jint), privileged and opt-in.

## Consequences
- One language for all builders; identical evaluation client- and server-side.
- Custom operations must be implemented in both runtimes and covered by shared tests.

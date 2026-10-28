# ADR-0014: Decision engine behind a platform contract

**Status:** Accepted

## Context
Classification grading is complex and sector-specific. An implementation will be supplied
separately; the platform must not over-engineer its own engine.

## Decision
The platform defines a decision-step contract: rule-set code and version, input mapping,
output mapping, explanation trace, stateless invocation (also used by the public calculator).
The engine implementation plugs in behind this contract.

## Consequences
- The platform stays simple; engines can evolve or be replaced.
- Rule-set configuration still follows versioning and maker-checker.

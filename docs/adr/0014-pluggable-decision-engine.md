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
- **A rule set used by a workflow must stay evaluable.** A workflow version whose decision step names a rule set with
  no published version in use is refused when it is checked (unless that version is created earlier in the same
  change set). Retiring a rule set's last version in use, or deactivating the rule set, is refused while a published,
  not retired workflow version decides with it. Each refusal names the step or the workflow versions and lists the
  steps to fix it. If a workflow still meets an unusable rule set (data from before these checks), submission stops
  with a clear *the service is not ready* message, the draft is kept, and the cause is given for the entity to fix;
  it is no longer a server error.

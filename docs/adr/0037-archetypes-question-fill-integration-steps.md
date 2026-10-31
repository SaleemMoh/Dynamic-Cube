# ADR-0037: Service archetypes, question fill sources, service-level questions and integration steps

**Status:** Accepted (asked for by the owner; implemented).

## Context
The archetype of a service was a fixed list in code (Issuance, Amendment, Visit Request, Attestation) that only labelled
the service. Each new service started from an empty definition and the platform's no-review workflow, so the same form
skeleton and review workflow were rebuilt every time. Question prefill was a free-text path, typed by hand. Answers could
not come from another agency's system, and an answer filled from the party could always be changed by the applicant.
Every question in the bank was offered to every service, so a question that only made sense for one service still
crowded every other service's list. Neither the service wizard nor the workflow could call an Integration Manager
operation.

## Decision
- **Archetypes are declared** (`svc.archetype`, *Service catalog → Archetypes*): code, names, description, icon, order,
  a **default service template** (a version definition, shared questions only) and a **default workflow** (a workflow
  definition, or none). Both can be copied from an existing service (*template from*, *workflow from*), then edited. A
  service names its archetype. A new service opens its archetype's template in the Service Builder. When the service is
  first published it gets the archetype's workflow (otherwise the no-review workflow, as before). An archetype in use
  cannot be deleted. The four seeded archetypes keep their codes and belong to the organization (not locked as system rows), so their names, template and workflow can be edited, or they can be removed. Issuance, Amendment and Visit Request review by role
  `SERVICE_REVIEWER`; Attestation has no review. The old Class/Domain class `SVC_ARCHETYPE` is no longer seeded: the
  table replaces it (an existing installation keeps the old class until it is deleted in Class/Domain; nothing reads it).
- **How a question is filled** (`fill`): *none*, *party* (a field picked from the list the party registry publishes:
  identifiers, profile and credentials of the beneficiary) or *integration* (an operation, its inputs each taken from a
  path such as `party.identifiers.CR_NUMBER` or `data.<question>`, and the output to use). The answer is filled when
  the draft is made. **Allow manual entry** (default on): when off, the answer is locked. The wizard shows it
  read-only and the server fills it again from its source on every save (an integration answer with inputs from the
  current answers), so a locked answer cannot be forged.
- **Service-level questions:** a question is shared (`serviceCode` empty, shown as `*`) or belongs to one service.
  A version may use shared questions and its own service's questions, never another service's. A question used by
  several services cannot be moved to one of them. The Service Builder's question bank shows shared questions plus the
  service's own, and *New question for this service* creates one there. (The owner confirmed that "service level
  questions" means questions of one service, not questions per service level such as Regular or Premium.)
- **Integration step in the service form** (`INTEGRATION`): an operation, its inputs (from the party, the applicant or
  answers), outputs copied into answers, and *required*. It runs when the applicant moves past the step; the outcome is
  kept on the request (`integrations`) and shown. A required step that failed stops the applicant with the provider's
  message, and submission checks it again.
- **Integration step in the workflow** (`INTEGRATION` node): `operation`, `parameters` (JSONLogic over the workflow
  facts), `result` (default: the node's code), `next` and an optional `failed` route. The outcome is a fact
  (`integrations.<result>`: `success`, `code`, messages, `outputs`, `data`) that later gateways and steps can use. With
  no `failed` route, a failure stops the step with the message. The designer has an *Integration call* step with two
  exits and an *Add the operation's parameters* button, and the test run asks whether the call succeeded.

## Consequences
- Services start from a working skeleton and workflow. Changing an archetype later does not change services already
  made from it, because each service owns its copy.
- `SERVICE_REVIEWER` is not created by the platform. Review tasks of services made from a reviewing archetype go to
  that role's queue, which nobody sees until a role with that code is given to staff (or the archetype's or service's
  workflow names another role).
- Integration answers and steps depend on the Integration Manager. A call is logged there like any other call, and
  masking and caching apply.
- Existing services keep their archetype (values converted to codes), and existing prefill paths become fill *party*
  with manual entry allowed, so nothing changes for applicants until a question is edited.

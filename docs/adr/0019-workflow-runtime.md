# ADR-0019: How the workflow engine runs

**Status:** Accepted (refines [ADR-0003](0003-custom-workflow-engine.md) for the first node types; revised in 1f: workflows belong to services)

## Context
Phase 1d delivers the engine for human work: distribution, parallel reviews with maker-checker, returns to
the applicant, approval, and deadlines on the working calendar. Several employees act on one request at the
same time (three analysts in parallel), the SLA monitor changes tasks in the background, and a request's status
lives in the Services module while its workflow lives in the Workflow module.

## Decision
- **Definitions are JSON graphs in change sets** (`wf.workflow`, `wf.workflow-version` `CODE/N`), checked on
  save (reachable nodes, an end, known targets, splits closed by joins) and immutable once published. A request
  starts on its service's workflow at the latest published version and stays on it.
- **Every service owns one workflow** (revised in 1f). `wf.workflow.serviceCode` ties a workflow to exactly one
  service; it cannot move to another service, nor be deleted while the service exists, and workflows are not
  shared. Publishing a new service creates its workflow in the same transaction with version 1 *Start → Completed
  (approved)*, which needs no review, so every service can take requests from the start. A service version is
  refused at validation unless its service's workflow has a published version; versions no longer name a workflow.
  The modules stay apart through kernel contracts (`IServiceCatalog` in Services, `IServiceWorkflows` in
  Workflow), and change-set publishing runs in a unit of work so a module's publish hook can write another's
  tables atomically.
- **Tokens move through the graph.** A parallel split forks tokens that carry a stack of fork frames; a join
  (all, any or a count) waits for the branches of the nearest open frame. When the instance ends, open tokens
  and tasks are cancelled.
- **One operation at a time per instance.** Every change (start, act, resume, claim, release, assign and each
  SLA change) runs in a unit of work and first locks the instance row (`SELECT … FOR UPDATE`). Branches
  arriving at a join see each other, and nobody's read is changed under them. Tasks also carry a row version.
- **Subjects stay in their module.** The engine talks to a subject through `IWorkflowSubject` (rule context;
  status updates). The Services module's request is one such subject; its update runs in the same unit of work,
  so the request's status and the workflow never disagree.
- **Assignment is data:** a role (optionally within an org unit or position), a named user or the previous
  actor of a step, by queue (claim), distribution (a distributor assigns) or automatically (least-loaded).
  `excludeActorsOf` keeps maker and checker apart. Delegations let a stand-in act on someone's assigned tasks,
  recorded as *on behalf of*.
- **Returns pause the clock.** A return sends the request back to the applicant with a reason they see; the
  instance waits and its deadline stops. On resubmission the deadline moves by the working days waited and the
  task goes back to the employee who returned it.
- **Deadlines** come from the service level (overall) and the node (per level), counted on the working
  calendar (installation weekend and holidays). A worker job runs every minute: tasks turn amber at their
  warning time and red when due (escalated to the node's role); instances turn red when overdue. Quartz is
  not needed yet.
- **Event ids increase in recording order** within a process, so a timeline whose events share a time (one
  step records several) reads in the order things happened.

## Consequences
- A service is never published without processing, and adding review is a new workflow version: requests in
  progress keep the version they started on. Reusing a workflow means copying its definition.
- Parallel work is safe without distributed locks; contention is limited to one request's operations.
- Integration, decision, payment, visit, document, notification and timer nodes are added to the same model
  in later phases.
- The JSON graph is authored by hand (with a live drawing) until the visual Workflow Builder arrives.

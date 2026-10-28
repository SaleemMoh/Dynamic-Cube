# ADR-0001: Modular monolith with in-process module calls

**Status:** Accepted

## Context
Every customer runs its own on-prem installation, often operated by a small government IT
team. Deployment must work on Kubernetes and on plain N-tier servers. Many microservices per
customer would multiply operational cost.

## Decision
One codebase organized in modules with strict boundaries (own schema, public interfaces).
Deploy as three processes: `Internal.Api`, `External.Api`, `Worker`. Modules communicate
through in-process interfaces; asynchronous work goes through the outbox (ADR-0009).

## Consequences
- Simple deployment, debugging and transactions.
- Boundaries must be enforced (architecture tests) so modules can be split later if needed.
- Scaling is per process, which is sufficient for single-entity workloads.

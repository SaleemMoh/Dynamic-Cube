# ADR-0003: Custom PostgreSQL-backed workflow engine

**Status:** Accepted

## Context
Government workflows are human-task centric (distribute, review, maker-checker, approve,
return) with parallel reviews, timers/SLA on a working calendar, and integration steps. We need
full control over versioning and instance pinning, an Angular designer, and permissive
licensing. Camunda 8 is not free for production; Elsa's designer is not Angular.

## Decision
Build a focused engine: workflow definitions as versioned JSON graphs (human task, parallel
split/join, exclusive gateway, integration, decision, payment wait, visit, sub-request, timer,
document, notification, script). State persisted in PostgreSQL; timers via Quartz.NET.

## Consequences
- Exactly the features we need, no more; full control of migration of running instances.
- We own correctness of parallelism, timers and compensation — requires thorough tests.
- Elsa remains a fallback option if scope grows beyond the focused design.

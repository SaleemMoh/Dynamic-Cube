# ADR-0009: Transactional outbox, no message broker

**Status:** Accepted

## Context
Notifications and integration pushes must not be lost and must not block user transactions.
Adding RabbitMQ/Kafka per customer site increases operational burden.

## Decision
Write outbox records in the same PostgreSQL transaction as the business change. The Worker
processes them with retries, backoff and idempotency keys.

## Consequences
- Exactly-once business change + at-least-once delivery, with no extra infrastructure.
- Throughput is bounded by PostgreSQL, sufficient for single-entity volumes.

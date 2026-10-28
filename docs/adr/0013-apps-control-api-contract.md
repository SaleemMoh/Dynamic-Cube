# ADR-0013: Apps Control owns its schema and a versioned API

**Status:** Accepted

## Context
Apps Control serves several apps built on Dynamic Cube and may later serve other products.

## Decision
Apps Control owns the `apps` schema and exposes a versioned REST API
(`/api/apps-control/v1`, OpenAPI). Dynamic Cube apps call it in-process; the same contract can
be published to other products and, if needed, deployed as a standalone service.

## Consequences
- API compatibility must be maintained once published externally.
- No other module reads `apps` tables directly.

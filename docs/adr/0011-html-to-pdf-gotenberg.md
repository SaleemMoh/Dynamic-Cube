# ADR-0011: HTML → PDF rendering with Gotenberg

**Status:** Accepted

## Context
Certificates and letters must render Arabic correctly (shaping, RTL) and be designed by
administrators. Many .NET PDF libraries struggle with Arabic or have restrictive licenses.

## Decision
Templates are HTML + Liquid (Fluid). PDFs are rendered by Gotenberg (headless Chromium, MIT)
running as an internal service.

## Consequences
- Pixel-accurate Arabic output using standard web skills.
- One more container/service to run.

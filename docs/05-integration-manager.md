# 5. Integration Manager

The Integration Manager lets administrators connect to third-party systems **by
configuration**. Adding one of the dozens of government APIs a service needs should mean
configuring and mapping it — not writing code. Module `Integration`, schema `integ`.

## 5.1 Findings from the MoMRAH integration guides

The 22 integration guides received (MCI, NIC, SCA, SCE, DGA, NCA, MOF, Masdr, Qawaem, Balady
services, delegation, labor housing, laborer inquiry, GEOSA, MOE qualifications, CIS
inspection, RabbitMQ services, …) share one template:

- **One gateway** — `https://apiservicesstg.balady.gov.sa/` (staging) and
  `https://apiservices.balady.gov.sa/` (production); one exception uses another host.
- **Auth** — OAuth 2.0 client credentials: Basic auth (consumer key/secret) to
  `/oauth/v1/token`, response `access_token`, `expires_in ≈ 17999` s; APIs called with
  `Bearer` token.
- **Envelope** — every response carries `statusDetails { code, message }` and
  `data { responseCode, … }` with documented custom business response codes.
- **Provider lookups** — guides ship their own lookup lists (e.g. MCI relation types, owner
  ID types).
- **Style** — REST + JSON; GET/POST/PUT.

This confirms the design below: a shared connection and envelope, many operations.

## 5.2 Model

| Concept | Description |
|---|---|
| **Provider** | The external party (e.g. `MCI`, `NIC`, `DGA`); used by `domain_external_code` mappings |
| **Connection** | Base URL per environment, timeouts, TLS settings, auth profile, retry & circuit-breaker policy. Shared by many operations |
| **Auth profile** | `NONE` · `BASIC` · `API_KEY` · `OAUTH2_CLIENT_CREDENTIALS` (token cached until expiry) · `MTLS`. Credentials in the secret store, never in plain tables |
| **Envelope profile** | How to read a response: success condition (e.g. `statusDetails.code` + `data.responseCode`), business-error path, message path. Defined once per provider family |
| **Operation** | Method, relative URL, headers, input schema, output schema, response codes. REST or SOAP (WSDL) |
| **Response code map** | Provider code → outcome (`SUCCESS` / `NOT_FOUND` / `BUSINESS_ERROR` / `TECHNICAL_ERROR`) + **bilingual user-facing message**. Fixes "unclear system messages" |
| **Mapping** | Declarative request mapping (from request / party / context to operation input) and response mapping (operation output to request data / party profile / credentials). Lookup translation via external codes |
| **Cache policy** | Per operation (e.g. ISIC activity list cached daily; CR info cached per request) |
| **Call log** | Every call: operation, correlation id, request/response (PII masked), duration, outcome. Viewable in the internal system |

## 5.3 Usage points

- **Service Builder** — wizard "go and get" steps (auto-fill), eligibility checks.
- **Workflow** — integration nodes (inquire / push); pushes go through the **outbox**
  (retries, idempotency keys).
- **Party Registry** — profile refresh and external credentials (e.g. DGA vendor
  classification and certificate download).
- **LOVs** — integration-sourced lists.
- **Callbacks** — inbound endpoints for asynchronous results (payment confirmations, external
  inspection results).

## 5.4 Tooling

- **Test console** — run an operation with sample input against staging; see raw and mapped
  output.
- **Import** — from OpenAPI/WSDL where available; manual definition otherwise.
- **API Endpoint Builder** — expose selected platform data/operations as REST endpoints for
  other parties (later phase), with the same auth and data-scope rules.

## 5.5 Initial connectors for the reference service

| Provider | Operations (examples) |
|---|---|
| MCI | CR info by ID number, CR info by CR number, owners by CR number |
| NIC | Person basic info, active delegations, verify owner or delegate, check suspensions |
| Delegation (Balady) | Commissioner by service type |
| Masdr (GOSI) | Establishment info, detailed periods |
| Laborer Inquiry | All laborers, laborer information |
| Bayan Qawaem | Financial statements data |
| SCE / Bayan SCE | Classification by license number, verify by ID |
| SCA | Contractor info, cancel classification |
| DGA | Vendor classification details, download vendor technical certificate, update certificate dates |
| NCA | Get vendor classification |
| MOF | Contract details, payment orders, financial claims |
| BaladyBusiness | Providers, Enjaz projects |
| LaborHousing | Licenses by owner |
| Balady Services | Bill info (payments) |
| CIS | Create inspection |

Applicability and priority to be confirmed per sector.

## 5.6 Implementation (1b)

Module `Integration`, schema `integ`. All configuration is edited through change sets (area
`integ.integrations`) in the Apps Control inbox; screens are under **Integrations** in the internal
system.

| Table | Holds |
|---|---|
| `provider` | External parties (`MCI`, `NIC`, …) |
| `connection` | Base URL, auth type, **secret name**, token URL, timeout, retries, circuit breaker, fixed headers |
| `envelope_profile` | Paths of status code / message, business code / message, result; success codes; shared code map |
| `operation` | Method, path template, parameters (path / query / header / body, masked or not), outputs (paths), code map, masked response fields, cache seconds, idempotency |
| `call_log` | Every call: operation, user, app, correlation id, duration, attempts, HTTP status, code, outcome, masked URL and bodies |

**Paths** are dot paths into the provider's JSON: `data.result`, `Licenses[0].Commercial_NO`,
`Licenses[*].Commercial_NO` (all elements). A property read on an array reads its first element, so
`statusDetails.code` works for MCI (array) and NIC (object) alike.

**Outcome** of a call: the operation's code map, then the envelope's, decide when they know the code
(business code, else status code, else `HTTP_<status>`). Otherwise: 5xx, 401/403/408/429, a gateway
status outside the success list, or an unreadable body → `TECHNICAL_ERROR`; 404 without a business code →
`NOT_FOUND`; other 4xx → `BUSINESS_ERROR`; a business code in the success list → `SUCCESS`, another one →
`BUSINESS_ERROR`. Technical errors always show the same bilingual "service not available" message;
the provider's raw text is kept in the call log only.

**Credentials** never live in tables: a connection names a secret, and the API hosts read
`Secrets:{name}:{key}` (`clientId`/`clientSecret`, `username`/`password` or `apiKey`) from their
configuration — environment variables, Kubernetes secrets or a vault provider.

**Resilience**: timeout per connection; retries with exponential back-off only for idempotent
operations (GET/PUT/DELETE unless configured otherwise) after timeouts, network errors and 5xx; an
OAuth2 token rejected with 401 is fetched again once; after *N* consecutive technical failures the
connection's circuit opens for a while and calls fail fast (per API instance).

**Caching**: OAuth2 tokens until shortly before `expires_in`; successful and not-found results for the
operation's cache seconds (keyed by inputs). Publishing integration configuration clears both.

**Use from other modules**: the kernel interface `IIntegrationGateway.InvokeAsync(operation, inputs)`
returns the outcome, code, bilingual message, mapped outputs and the result data — never URLs or
credentials. The Party Registry uses it for acting contexts and profile fields.

**Configuration packages**: `deploy/config-packages/momrah-mci.json` holds the MCI connectors (two
operations with the guide's business codes and messages) plus the Party Registry settings that use them;
importing it creates draft change sets for review. `src/Tools/MockGateway` answers like the MoMRAH gateway
(token endpoint and MCI operations with sample data) for development and demos.

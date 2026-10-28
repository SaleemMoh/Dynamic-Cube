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

# ADR-0042: Public API endpoints for partner systems (API Endpoint Builder)

**Status:** Accepted (asked for by the owner; implemented). The last Integration Manager item proposed in ADR-0041. The
owner decided: (1) partners sign in with **OAuth2 client credentials** in the identity provider; (2) endpoints **read
data** and use **read-only system functions** such as a request's place in its workflow; (3) **read-only** — nothing a
partner calls changes data; (4) the demo endpoint reads a **class** (Class/Domain).

## Context
Partners (other government systems, contractors' platforms, banks) ask for data the platform holds: lists such as the
regions, a request's status, whether a certificate is genuine. Each such need meant code: a controller, its
authorization, paging and documentation. The query builder (ADR-0039) already describes reads over registered tables
and classes safely, and the modules already know how to answer their own questions.

## Decision
- **Endpoints are configuration** (`integ.api-endpoint`, change sets): a code, names, a **path** under
  `/api/public/v1` on the **external host** with `{placeholders}`, and **parameters** — each placeholder is a path
  parameter (required), others come from the query string — with a type (text, number, date, yes/no, checked before the
  endpoint runs), a sample, a description and **masked** (written `***` in the log). A path is answered once (paths of the
  same shape conflict); `limit` and `offset` are kept for paging.
- An endpoint does one of two things:
  - **Read data** — a definition built like a query-builder list over **registered tables and classes only** (never an
    unregistered table): a source, joins, the **fields** returned (a name in the response and the column it reads),
    conditions comparing a column with a fixed value, a **parameter** (left out when the caller does not send it), or
    the **calling client's companies or branches**, sort and distinct. Class values keep their rules (active, in effect
    today, serving the client's companies). A page of rows (`items`, `limit`, `offset`, `hasMore`; the page size is
    also the most a caller may ask) or **one row** (404 when none). The query runs read-only with the query builder's
    time limit; parameters are only ever bound as values.
  - **System function** — a read-only function a module contributes (`IApiFunction`), each input given a parameter or a
    fixed value: **CLASS_VALUES** (Apps Control: a class's values in effect, optionally the children of a value),
    **LOOKUPS** (Apps Control: several classes and several lists of values in one call — codes separated by commas or
    repeated parameters, at most 20 of each; only lists marked **public**, the ones the portal serves without sign-in,
    for the client's companies; an unknown class or a list that is not public is refused),
    **REQUEST_STATUS** (Services with the Workflow: the status the applicant sees, the steps the request is at now with
    their deadlines, how many it went through, the timeline — without comments or people) and **DOCUMENT_VERIFY**
    (Documents: what the QR verification page shows, public fields only). Every function applies the caller's
    companies and branches itself.
- **API clients** (`integ.api-client`, change sets): a partner system — its **sign-in client** (`client_id`, `dc-api-…`
  by default), the **endpoints** it may call, the **companies and branches** whose data it sees (all chosen
  explicitly, `*` for all, as for roles), a **rate limit** per minute and a contact. Data scopes, which follow roles,
  do not apply to API clients: the endpoint's definition and the client's companies and branches decide.
- **Sign-in.** The client lives in the realm the external host trusts (the portal's), as a **service account only**
  (client credentials, no one signs in with it), its tokens carrying the public API's audience
  (`dynamic-cube-public-api`, `PublicApi:Audience`). *Sign-in and credentials* on the client sets it up or brings it in
  line through Keycloak's admin API (the portal accounts' service account, which now also manages clients) and shows
  the **secret once**; the platform never stores it. Where clients are maintained in the identity provider directly,
  the screen says so.
- **Isolation.** The external host accepts the public API's audience besides its own. A token issued for the public API
  only reaches the public API (the default policy refuses it), and the public API needs that audience (a portal
  user's token cannot call it). The client is identified by the token's `azp`; an unknown or inactive client, an
  endpoint not given to it, or an inactive endpoint are refused.
- **Every call is logged** (`integ.api_call`): client, endpoint, the path and query with masked parameters as `***`,
  status, rows, duration, the caller's address and, for refused or failed calls, why. A partner sees a generic message
  for a failure; the designer's try shows the cause.
- **Documentation.** Each client gets an **OpenAPI 3.0** description of its own endpoints (paths, parameters, the
  fields with their types, function results from their samples, client-credentials security, problem responses): from
  the API (`GET /api/public/v1/openapi.json` with its token) and from *Sign-in and credentials* to hand over.
- **Screens** (Integrations): *API endpoints* opens the **endpoint designer** — path and inputs (placeholders become
  inputs; the URL a partner calls), *What it returns* (read data: source, joins, fields, conditions, sort, paging; or
  a function with its inputs and a sample of its result), details, and **Try it**: the unsaved endpoint called as a
  partner would, within a client's companies and branches or with no limits. *API clients* (with *Sign-in and
  credentials*) and *API calls* (the log, by client, endpoint and result).

## Consequences
- New partner reads are configured and approved like any configuration; nothing a partner calls can change data.
- The rate limit counts per instance of the external host; with several instances, each allows the limit.
- Read-data endpoints are limited to the query builder's reach (one level of conditions in the designer, registered
  sources); anything more is a system function written in a module.
- Writes (submitting a request for a partner, callbacks with business effects) are outside this decision.
- Docker: the realm revision is raised so Keycloak re-imports the realm with the public API's audience, the sample
  partner's client (`dc-api-partner-demo`, development secret) and the portal service account's client roles.

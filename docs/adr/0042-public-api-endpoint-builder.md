# ADR-0042: Public API endpoints for partner systems (API Endpoint Builder)

**Status:** Accepted (asked for by the owner; implemented). The last Integration Manager item proposed in ADR-0041. The
owner decided: (1) partners sign in with **OAuth2 client credentials** in the identity provider; (2) endpoints **read
data** and use **read-only system functions** such as a request's place in its workflow; (3) **read-only** — nothing a
partner calls changes data; (4) the demo endpoint reads a **class** (Class/Domain). Later the owner decided that an API
client is a **service account** like the one a system uses to reach a database in production: (1) its roles and
privileges are kept on the **same user** whether opened from *Users* or from *API clients*; (2) **endpoints are
resources** that roles give; (3) its companies and branches come from its roles, and on some endpoints the client may
send the identifier of a person registered in Apps Control, whose roles then apply — otherwise the service account is
treated like any other user; (4) every class is listed, and class values follow data grants where one is set.

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
    the **calling account's company or branches**, sort and distinct. Class values keep their rules (active, in effect
    today, serving the call's company). The account's **data scopes** (for the endpoint's resource) filter the data
    objects read, and a table's rows are those of the call's **company and branches** (the same rule as reports). A page of rows (`items`, `limit`, `offset`, `hasMore`; the page size is
    also the most a caller may ask) or **one row** (404 when none). The query runs read-only with the query builder's
    time limit; parameters are only ever bound as values.
  - **System function** — a read-only function a module contributes (`IApiFunction`), each input given a parameter or a
    fixed value; an **optional input may be left empty** (not given), which is how "get all or get some" works: an
    optional input bound to an optional parameter means *all* when the caller leaves it out and *only those* when the
    caller sends codes (separated by commas, or the parameter repeated). Functions: **CLASSES** (Apps Control: classes
    and their values — every active class when no codes are given, or only those asked for, in that order; `values`
    `false` leaves the values out; an unknown class is refused), **CLASS_VALUES** (Apps Control: a class's values in
    effect, optionally the children of a value, and optionally only the values whose codes are given) — class values
    in both follow the caller's **data grants**: each active list of values built on the class that limits the account
    (a grant of some values, or a list that requires a grant the account lacks) keeps only what it allows,
    **LOOKUPS** (Apps Control: several classes and several lists of values in one call — codes separated by commas or
    repeated parameters, at most 20 of each; only lists marked **public**, the ones the portal serves without sign-in,
    for the call's company, with the caller's data grants; an unknown class or a list that is not public is refused),
    **REQUEST_STATUS** (Services with the Workflow: the status the applicant sees, the steps the request is at now with
    their deadlines, how many it went through, the timeline — without comments or people) and **DOCUMENT_VERIFY**
    (Documents: what the QR verification page shows, public fields only). Every function applies the caller's
    company and branches itself.
- **API clients** (`integ.api-client`, change sets): a partner system — its **sign-in client** (`client_id`, `dc-api-…`
  by default), the **service account** it acts as, a **rate limit** per minute and a contact.
- **Service accounts.** A user of Apps Control of kind **SERVICE** (`apps.user` `kind`): no person, no sign-in account,
  never signs in (a sign-in under its name is refused, and *Sign-in account* is not offered), placed in no unit, and
  its kind never changes. It holds **roles, personal privileges and data grants exactly like a person** — by company
  and branches — kept on one user: *Users* edits it, and *Roles and privileges* on the API client opens the same
  editor. A client names its account, or gets its own (`svc-{code}`), created with no roles when the client is
  published. Nothing about access is stored on the client.
- **Endpoints are resources.** Each published endpoint is a resource `integration.api.{code}` (VIEW) under *Public API
  endpoints*, kept in line on publish (deactivated with its endpoint). A role gives VIEW on it to the service accounts
  that may call it; the role editor lists them like any screen.
- **Each call runs as an account.** The client's service account (active, kind SERVICE) is resolved like a person for
  the internal system's roles: the companies its roles open, the **company of the call** — the one sent in
  **`X-Company`**, or its only company (several and none named: 400 listing them; one it does not work in: 403) — the
  branches of it, VIEW on the endpoint (otherwise 403), and its data grants and scopes. On an endpoint marked
  **acts for a person**, the client may send **`X-Acting-User`** with a username, national ID or email: a registered
  **person** found by it has their roles and data apply instead, within the company of the call (403 when they do not
  work in it, or are not active) and the branches both of them work in; a name that matches nobody leaves the service
  account's access, one that matches several people must be the username (400), and the header on an endpoint that
  does not allow it is refused (400). The endpoint itself is always checked against the service account. The log
  records whom the client acted for.
- **Sign-in.** The client lives in the realm the external host trusts (the portal's), as a **service account only**
  (client credentials, no one signs in with it), its tokens carrying the public API's audience
  (`dynamic-cube-public-api`, `PublicApi:Audience`). *Sign-in and credentials* on the client sets it up or brings it in
  line through Keycloak's admin API (the portal accounts' service account, which now also manages clients) and shows
  the **secret once**; the platform never stores it. Where clients are maintained in the identity provider directly,
  the screen says so.
- **Isolation.** The external host accepts the public API's audience besides its own. A token issued for the public API
  only reaches the public API (the default policy refuses it), and the public API needs that audience (a portal
  user's token cannot call it). The client is identified by the token's `azp`; an unknown or inactive client, an
  endpoint its account's roles do not give, or an inactive endpoint are refused.
- **Every call is logged** (`integ.api_call`): client, endpoint, the path and query with masked parameters as `***`,
  status, rows, duration, the caller's address, the person acted for and, for refused or failed calls, why. A partner sees a generic message
  for a failure; the designer's try shows the cause.
- **Documentation.** Each client gets an **OpenAPI 3.0** description of the endpoints its account may call (paths, parameters, the
  fields with their types, function results from their samples, client-credentials security, problem responses): from
  the API (`GET /api/public/v1/openapi.json` with its token) and from *Sign-in and credentials* to hand over.
- **Screens** (Integrations): *API endpoints* opens the **endpoint designer** — path and inputs (placeholders become
  inputs; the URL a partner calls), *What it returns* (read data: source, joins, fields, conditions, sort, paging; or
  a function with its inputs and a sample of its result), details (with *the client may act for a registered person*),
  and **Try it**: the unsaved endpoint called as a partner would — as a client's service account (optionally in a named
  company, or for a person) or with no limits. *API clients* (with *Roles and privileges* and *Sign-in and
  credentials*), *Users* (person or service account) and *API calls* (the log, by client, endpoint and result).

## Consequences
- New partner reads are configured and approved like any configuration; nothing a partner calls can change data.
- The rate limit counts per instance of the external host; with several instances, each allows the limit.
- Read-data endpoints are limited to the query builder's reach (one level of conditions in the designer, registered
  sources); anything more is a system function written in a module.
- Writes (submitting a request for a partner, callbacks with business effects) are outside this decision.
- Access is decided once, in Apps Control, for people and systems alike; a partner's access is reviewed on *Users*
  with everyone else's. A client whose account holds no role calls nothing.
- Acting for a person trusts the client to name the person truthfully: give the option only to endpoints and clients
  trusted with it. The endpoint is still checked against the client's own account.
- Upgrading: the migration gives each existing client its account (`svc-{code}`) and keeps what it had; the migrator
  then creates the account with a role of its own (`API_{code}`) opening the endpoints it had, held in the companies and
  branches it had, so it calls exactly what it called before. Read-data endpoints on tables with a company or branch
  now return only the call's company's rows.
- Docker: the realm revision is raised so Keycloak re-imports the realm with the public API's audience, the sample
  partner's client (`dc-api-partner-demo`, development secret) and the portal service account's client roles.

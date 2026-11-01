# ADR-0043: Nafath — signing in to the portal and confirming a request

**Status:** Accepted (asked for by the owner; implemented). One of the Integration Manager items proposed in ADR-0041.
The owner decided: (1c) Nafath both **signs applicants in** to the portal and **confirms** a step; (2a) signing in goes
**through Keycloak**, Nafath being one of its identity providers; (3) the flows follow **Nafath's own documentation**,
redirection included; (4) a **mock Nafath** stands in for it in development.

## Context
Nafath (the National Single Sign-On, operated by Elm) is how people in Saudi Arabia prove who they are to government
services. Elm's **Rabet** API offers two flows to a service provider:

- **Web sign-in** — `GET /api/v2/oidc/session` returns the address of Nafath's sign-in page (with a `state`) and a
  hash of the state; the browser goes there, the person signs in, and Nafath sends the browser back to the provider's
  callback with the `state`; `POST /api/v2/oidc/jwt` with the state returns the person as a signed token (`401-033-024`
  while they have not finished).
- **Requests in the Nafath app** — `POST /api/v1/mfa/request` with the national ID and the provider's service returns a
  transaction and a **number**; the person opens the app and picks that number; `POST /api/v1/mfa/request/status`
  says WAITING, COMPLETED, REJECTED or EXPIRED; Nafath **posts the signed answer** (`token`, `transId`, `requestId`)
  to the provider's callback. The keys tokens are signed with are at `/api/v1/mfa/jwk`.

Every call carries `APP-ID`, `APP-KEY` and `X-Forwarded-For` (the person's address, then the server's). Sandbox, staging
and production differ by the address prefix (`/nafath-sandbox`, `/stg`, none). A request lives 200 seconds; a person
with an open request gets `400-034-050`.

Until now, portal accounts were Keycloak users whose token carried `national_id` (a stand-in for Nafath).

## Decision
- **Settings** (`Nafath` on the external host): Rabet's address with the environment's prefix, the server's outbound
  address (sent in `X-Forwarded-For`), the locale, the default service of app requests (`OpenAccount`) and how long a
  request lives. Credentials are **never in the database**: `Secrets:NAFATH:appId` and `appKey` from the secret store.
  An empty address means Nafath is not set up: a service asking for a Nafath confirmation says so, and the realm's
  identity provider should then be turned off.
- **Every answer is checked**: the signed token is verified with Nafath's published keys (kept 12 hours, read again
  for an unknown key), its lifetime and, for app requests, its transaction; the person (national ID or iqama, names in
  Arabic and English, nationality, date of birth, gender) is read from its claims. A status of COMPLETED alone is never
  taken as proof: only the signed answer at the callback verifies a request.
- **Requests are recorded** (`integ.nafath_request`): kind (sign-in or verification), status (waiting, verified,
  rejected, expired, used), the subject it is for (e.g. a service request), Nafath's transaction and number, and the
  times. The national ID, the person and the sign-in state are **encrypted** (ASP.NET Core data protection); only a hash
  of the state and of the one-time code are searchable.
- **Signing in — a bridge Keycloak trusts.** The external host serves a small **OpenID Connect provider**
  (`/api/nafath/v1/oidc`: discovery, `authorize`, `callback`, `token`, `jwks`) that Keycloak's realm uses as the identity
  provider *نفاذ (Nafath)*:
  1. Keycloak's login page offers *Sign in with Nafath*; Keycloak sends the browser to the bridge's `authorize`
     (client `keycloak`, only to the redirect addresses registered in `Nafath:Bridge:RedirectUris`).
  2. The bridge opens a Nafath web session and sends the browser to **Nafath's sign-in page**.
  3. Nafath sends the browser back to the bridge's `callback` with the state; the bridge asks Nafath for the person's
     token (a page that waits and asks again while Nafath has not finished) and sends the browser back to Keycloak with a
     **one-time code** (single use, two minutes).
  4. Keycloak exchanges the code with the bridge's **client secret** (`Secrets:NAFATH_BRIDGE:clientSecret`, compared in
     constant time) for an **ID token signed by the bridge** (RS256; `Secrets:NAFATH_BRIDGE:signingKey`, a PEM RSA key;
     without one a key is made at start-up and a warning logged): `sub`, `preferred_username` and `national_id` are the
     national ID or iqama, with the names, `amr` and the nonce.
  5. Keycloak's *nafath first login* flow links the account with the same user name or creates it, with the names and
     `national_id` — the portal then works as for any portal account.
- **Confirming a request — requests in the app.** A service version may ask the applicant to **confirm the request
  with Nafath before it is submitted** (Service Builder, *Rules* tab, *Submitting*; `confirmWithNafath` in the
  definition):
  1. The portal submits; the server checks everything else first. When the confirmation is all that is missing, the
     portal opens *Confirm the request with Nafath* and asks the server to start one
     (`POST /api/services/v1/requests/{id}/nafath`): a request goes to the Nafath app of the applicant — the national ID
     or iqama in **their own profile** — and the dialog shows the **number** with the seconds left.
  2. The dialog asks every 3 seconds where it stands (`GET …/nafath/{checkId}`); the server asks Nafath's status while it
     waits. A rejected or expired request can be sent again.
  3. Nafath posts the signed answer to `/api/nafath/v1/callbacks/mfa`; the answer must name the person the request was
     sent to. The dialog closes and the portal submits with the confirmation.
  4. On submit the server accepts the confirmation only if it is verified, for this request, completed in the last
     **15 minutes** and by the **applicant** (the identifier in the answer equals theirs). The request records the
     confirmation and when it was given, and its timeline shows *NAFATH_CONFIRMED*.
- **Kernel contract** `INafathVerifications` (start, check), so any module can ask for a confirmation in the app without
  knowing Nafath's API.
- **Mock Nafath** in the mock gateway (`/nafath`): Rabet's endpoints with the development `APP-ID`/`APP-KEY`, signed
  tokens with the names of the NIC sample, a page standing in for **Nafath's sign-in page** (`/nafath/portal`: type a
  national ID, approve or reject) and one standing in for the **Nafath app** (`/nafath/app?id={nationalId}`: the
  waiting requests, answered by picking a number; a wrong number rejects it). Docker and the development settings point
  the external host at it.

## Consequences
- Applicants can sign in with Nafath or, where kept, with a password; both land on the same portal account.
- Services that need certainty about who submits turn on the confirmation per version; versions already in use do not
  change.
- **Production needs**, outside the code: an agreement with Elm and the APP-ID/APP-KEY in the secret store; the
  callback addresses **reachable from Nafath and hosted in the Kingdom**; the **server's outbound address allowlisted**
  by Elm and set in `Nafath:ServerIp`; a **signing key** for the bridge in the secret store (otherwise Keycloak must
  re-read the keys after every restart); the realm's identity provider pointing at the bridge's public address. Elm
  charges per request: services should ask for a confirmation only where it matters.
- The bridge's `authorize` has no rate limit of its own yet; each call opens a Nafath session. A limit per address
  belongs in front of it (gateway or reverse proxy) until one is added.
- Confirming a step of the workflow (an employee or a delegate confirming with Nafath) uses the same contract and is
  left for when a service asks for it.
- Docker: the realm revision is raised so Keycloak re-imports the realm with the identity provider, its mappers and the
  first-login flow.

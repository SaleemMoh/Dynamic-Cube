# Keycloak realms

Two realms, one per network zone (see [ADR-0004](../../docs/adr/0004-separate-api-hosts-per-zone.md) and
[ADR-0005](../../docs/adr/0005-keycloak-authn-app-authz.md)):

| Realm | Used by | Token audience |
|---|---|---|
| `dynamic-cube-internal` | Internal system, field app, `Internal.Api` | `dynamic-cube-internal-api` |
| `dynamic-cube-external` | E-services portal, `External.Api` | `dynamic-cube-external-api` |

Each API accepts only tokens from its own realm and audience. The SPAs sign in with authorization code + PKCE
through the public clients `dynamic-cube-internal-web` (internal system on :4200 and field app on :4202) and
`dynamic-cube-external-web` (portal on :4201).

The files in `realms/` are **development seeds**: they are imported by `docker compose` (or
`kc.sh start-dev --import-realm`) and contain test users and a password-grant client.

## Login themes

Both themes extend Keycloak's `keycloak.v2` theme with stylesheets and small scripts only. Docker Compose mounts them;
on servers, copy both folders into Keycloak's `themes/` directory. For an existing realm, choose the theme in
*Realm settings → Themes*.

- `themes/dynamic-cube` gives the portal's sign-in pages the DGA design: IBM Plex Sans Arabic, DGA greens and the
  government bar. The external realm uses it (`"loginTheme": "dynamic-cube"`).
- `themes/dynamic-cube-internal` gives the internal system's sign-in pages the internal design: neutral gold, 8px
  controls, a 12px card and an image panel, with no government bar. The internal realm uses it
  (`"loginTheme": "dynamic-cube-internal"`).

Both apply the entity's branding from Apps Control (logo, sign-in image, theme colour) with `js/branding.js`. The
script reads `/api/apps-control/v1/branding` through the application that started the sign-in (its `redirect_uri`).

## Account management

The internal API creates employees' accounts, enables and disables them and sets passwords through Keycloak's admin
API (see [ADR-0026](../../docs/adr/0026-look-accounts-json-import.md)). It signs in as the confidential client
`dynamic-cube-accounts`, whose service account holds these realm-management roles: `manage-users`, `view-users`,
`query-users`, `view-realm` and `view-identity-providers`.

The development realm defines the client with the secret `dev-accounts-secret`. Per installation:

- Generate a new secret and put it in the internal API's secret store as `Secrets__KEYCLOAK_ADMIN__clientSecret`.
- Set `AppsControl__Identity__Provider=Keycloak` and `AppsControl__Identity__BaseUrl`.
- When employees come from Active Directory, LDAP or Entra ID, also set `AppsControl__Identity__Mode=Federated`, so
  Apps Control only enables, disables and signs out.

## Development users

Password for all: `Dev@12345`

| Realm | Username | Notes |
|---|---|---|
| internal | `admin`, `maker`, `checker` | Receive `SYSTEM_ADMIN`, `CONFIG_MAKER`, `CONFIG_CHECKER` on first sign-in (`AppsControl:BootstrapUsers` in development) |
| internal | `manager`, `analyst1`, `analyst2`, `auditor1`, `director` | Classification staff; their roles come from the `classification-workflow.json` package (see `deploy/config-packages`) |
| internal | `inspector1`, `supervisor` | Field inspector (field app) and visits supervisor; their roles come from the `site-visits.json` package |
| external | `1000000001`, `2000000002` | Applicants; `national_id` attribute mapped into tokens |

The development Keycloak keeps no data: every fresh start re-imports both realms. Apps Control links each account to
the user's Keycloak ID (the token's `sub`), so the realm files give every user a fixed `id`, and a re-import keeps them.
Development also sets `AppsControl:RelinkAccountsByUserName`. An account whose Keycloak ID did change, for example one
created before the IDs were fixed or added later from Apps Control, moves to the new ID on its next sign-in. In
production this stays off. A username that comes back with a different ID is refused with 403, so a reused username
cannot inherit someone else's roles. An administrator relinks the account instead.

Get a token for API testing (development only):

```bash
curl -s -X POST http://localhost:8080/realms/dynamic-cube-internal/protocol/openid-connect/token \
  -d grant_type=password -d client_id=dynamic-cube-dev-tools -d username=maker -d password='Dev@12345'
```

## Production

Per customer installation:

- **Remove** the test users and the `dynamic-cube-dev-tools` client; give `dynamic-cube-accounts` a new secret.
- Set real redirect URIs / web origins on the SPA clients.
- **Internal realm**: add LDAP / Active Directory user federation for employees.
- **External realm**: add **Nafath** as identity provider and map the national ID to the `national_id`
  user attribute (the claim the platform already reads). Until Nafath is connected, the dev users stand in for it.
- Run Keycloak in production mode (`start`) behind TLS with its own database.

# Keycloak realms

Two realms, one per network zone (see [ADR-0004](../../docs/adr/0004-separate-api-hosts-per-zone.md) and
[ADR-0005](../../docs/adr/0005-keycloak-authn-app-authz.md)):

| Realm | Used by | Token audience |
|---|---|---|
| `dynamic-cube-internal` | Internal system, field app, `Internal.Api` | `dynamic-cube-internal-api` |
| `dynamic-cube-external` | E-services portal, `External.Api` | `dynamic-cube-external-api` |

Each API accepts only tokens from its own realm and audience.

The files in `realms/` are **development seeds**: they are imported by `docker compose` (or
`kc.sh start-dev --import-realm`) and contain test users and a password-grant client.

## Development users

Password for all: `Dev@12345`

| Realm | Username | Notes |
|---|---|---|
| internal | `admin`, `maker`, `checker` | Maker / checker for change-set approval tests |
| external | `1000000001`, `2000000002` | Applicants; `national_id` attribute mapped into tokens |

Get a token for API testing (development only):

```bash
curl -s -X POST http://localhost:8080/realms/dynamic-cube-internal/protocol/openid-connect/token \
  -d grant_type=password -d client_id=dynamic-cube-dev-tools -d username=maker -d password='Dev@12345'
```

## Production

Per customer installation:

- **Remove** the test users and the `dynamic-cube-dev-tools` client.
- Set real redirect URIs / web origins on the SPA clients.
- **Internal realm**: add LDAP / Active Directory user federation for employees.
- **External realm**: add **Nafath** as identity provider and map the national ID to the `national_id`
  user attribute (the claim the platform already reads). Until Nafath is connected, the dev users stand in for it.
- Run Keycloak in production mode (`start`) behind TLS with its own database.

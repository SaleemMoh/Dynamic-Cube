# Configuration packages

Ready-made configuration that an installation can import instead of typing it in. Importing creates **draft change
sets** (one per configuration area); they are reviewed, submitted and approved like any other change, so the
before/after record and maker-checker apply.

Import in the internal system: **Apps Control → Change sets → Import package**, or with the API:

```bash
curl -X POST http://localhost:5080/api/apps-control/v1/change-sets/import \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" --data @momrah-mci.json
```

| Package | Contents |
|---|---|
| `momrah-mci.json` | Provider `MCI`, connection `MOMRAH_GATEWAY` (OAuth2 client credentials, secret name `MOMRAH_GATEWAY`), the MoMRAH response envelope, operations `MCI_CR_BY_ID_NUMBER` and `MCI_CR_INFO` with the MCI guide's business codes and bilingual messages, the acting-context source `MCI_CRS` and five establishment profile fields |

After importing `momrah-mci.json`:

1. Set the connection's **base URL**: `http://localhost:5090` is the development mock gateway
   (`src/Tools/MockGateway`; in Docker Compose use `http://mock-gateway:8080`); staging is
   `https://apiservicesstg.balady.gov.sa`, production `https://apiservices.balady.gov.sa`.
2. Put the consumer key and secret in the secret store of the API hosts — never in the database:
   `Secrets__MOMRAH_GATEWAY__clientId` and `Secrets__MOMRAH_GATEWAY__clientSecret`.

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

| `classification-workflow.json` | Processing of the classification service ([§6.7](../../docs/06-reference-service-classification.md#67-processing-workflow)): eight roles, five development staff users (`manager`, `analyst1`, `analyst2`, `auditor1`, `director`, matching the Keycloak development realm), the workflow `CLASSIFICATION` (the request manager reviews and distributes; governance, projects and financial analyses run in parallel, each checked by an auditor who did not do the analysis; the general manager approves; deadlines per service level) and `CLASSIFICATION_NEW/2`, version 1 processed by this workflow. Import `classification-new.json` first |
| `classification-certificate.json` | Grading and the certificate ([§6.5](../../docs/06-reference-service-classification.md#65-evaluation-decision-engine-inputs-and-outputs), [§6.8](../../docs/06-reference-service-classification.md#68-certificate), [§6.10](../../docs/06-reference-service-classification.md#610-notifications)): establishment profile fields `EMAIL` and `MOBILE` (where notifications go), the print template and document type `CLS_CERTIFICATE` (one per establishment, 24 months, reminders 90 and 15 days before expiry, a line per classified sector), the rule set `CLS_GRADING` (technical, performance and governance grades per sector — simplified; the financial axis comes later), `CLASSIFICATION/2` (the grade is calculated after the analyses; an approved request with a classified sector gets the certificate, otherwise it ends rejected) and ten message templates and rules (returned, rejected with an advisory offer, completed, certificate issued or amended, renewal and extension open, expired; staff: task assigned, task overdue). Import `classification-workflow.json` first |
| `classification-new.json` | The reference service *City service providers classification — new certificate* (`CLASSIFICATION_NEW`): 20 questions, three service levels (Regular free; Premium and Diamond with fees by annual revenue, VAT included), sector-dependent sections with shared questions, cross-field checks, the *active CR* eligibility rule and the pledge. Import `momrah-mci.json` first: the CR number, city and status come from MCI |

After importing `momrah-mci.json`:

1. Set the connection's **base URL**: `http://localhost:5090` is the development mock gateway
   (`src/Tools/MockGateway`; in Docker Compose use `http://mock-gateway:8080`); staging is
   `https://apiservicesstg.balady.gov.sa`, production `https://apiservices.balady.gov.sa`.
2. Put the consumer key and secret in the secret store of the API hosts — never in the database:
   `Secrets__MOMRAH_GATEWAY__clientId` and `Secrets__MOMRAH_GATEWAY__clientSecret`.

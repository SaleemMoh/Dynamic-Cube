# ADR-0041: Visual operation designer and OpenAPI import

**Status:** Accepted (asked for by the owner; implemented). Of the Integration Manager items proposed — (a) a visual
operation designer, (b) SOAP, (c) OpenAPI import, (d) Nafath, (e) the API Endpoint Builder — the owner chose (a) and
(c) first.

## Context
Operations were edited in the generic configuration form: a path typed by hand, inputs, outputs and codes as row
tables, and outputs written as dot paths (`Licenses[*].Commercial_NO`) without seeing the provider's response. The test
console worked only on published operations, so a mistake was found after approval. Providers that publish an OpenAPI
or Swagger description still had every operation typed in.

## Decision
- **The operation designer is the way in.** *New* and every operation on *Integrations → Operations* open it. It has
  four parts and a side panel:
  - **Request** — connection, response envelope, method, and the path. Each `{placeholder}` typed in the path becomes
    a path input, and renaming or removing a placeholder renames or removes its input. Other inputs go in the query, a
    header, or a dot path of the JSON body, each with a sample value, required and masked. A preview shows the full URL
    and the body the provider receives.
  - **Response & outputs** — the response is shown as a tree: from the last *Try it* (the part the envelope reads as
    the result) or from a pasted sample. Pressing + next to a field makes it an output with its path and a name. Lists
    are read for every element (`[*]`). Outputs can also be added or edited by hand.
  - **Provider codes** — the operation's own code map (code, outcome, Arabic and English messages), with the envelope's
    code path shown.
  - **Details** — descriptions, organization and apps, cache, whether a call may be retried, and fields masked in the
    log.
  - **Try it** (side panel) runs the operation **as it is now, not saved** (`POST /operations/try`). It runs on the
    published connection and envelope it names, with the inputs typed there, and shows the outcome, the request sent
    and the raw response. The definition is checked as saving would check it, and the problems come back per field.
    Tries bypass the response cache and are written to the call log like other calls. Nothing is saved.
  Saving adds the operation to a change set, as before.
- **OpenAPI import.** The Operations screen offers *Import from OpenAPI*. The description is pasted or chosen as a file
  (JSON or YAML; OpenAPI 3.x or Swagger 2.0) and read on the server (`POST /openapi/read`, Microsoft.OpenApi with its
  YAML reader, references resolved). Nothing is saved at this stage. The import suggests:
  - the **provider** (from the title);
  - the **connection** — base URL from the servers or host; authentication from the security schemes: OAuth2 client
    credentials with its token URL, an API key header, or basic. Only the name of the credentials is kept; the values
    go to the secret store, never the database;
  - the **operations** — code from the operation id (or the method and path), name from the summary, and inputs from
    the path, query and header parameters and the JSON body's fields (as body dot paths). Authorization and content
    headers are left to the connection. Fields with personal data names (national ID, mobile, email, birth date…) are
    masked. Sample values come from the examples.
  Each operation also lists the fields of its success response, which can become outputs. An existing provider or
  connection can be used instead of new ones. Choosing a response envelope reads outputs from its data (e.g. under
  `data.result`), and an envelope whose result path holds every response is chosen at once. Codes get a prefix and
  are flagged when already in use. The ticked operations, with their chosen outputs, are imported as a configuration
  package. They can be added as a draft or sent for approval.
- What the import does not do: response-code messages (added in the designer), list fields of a request body (inputs
  are single values), cookies, bearer-token or OpenID Connect schemes (noted, chosen by hand), and fetching a
  description from a URL (pasting or a file avoids the server calling arbitrary addresses).

## Consequences
- Operations are designed against real responses before approval; the approver sees an ordinary operation change.
- `Try it` needs the test console privilege; reading a description needs the right to create integrations.
- The mock gateway serves its own description at `http://localhost:5090/openapi.yaml` (MCI, NIC) to show the import.
- Next in the Integration Manager, when the owner chooses: SOAP/WSDL operations, Nafath sign-in, the API Endpoint
  Builder.

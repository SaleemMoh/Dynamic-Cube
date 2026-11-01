# ADR-0044: SOAP operations, WS-Security and WSDL import

**Status:** Accepted (asked for by the owner; implemented). The SOAP item proposed in ADR-0041. The owner decided:
(1) SOAP operations are designed in the **same operation designer**; (2a) **WSDL import** now; (3a) **WS-Security
UsernameToken** plus basic authentication (client certificates later); (4) a **mock SOAP service** for development.

## Context
Many government services in the Kingdom still speak SOAP (Yakeen, older ministry services, bank and payment gateways).
Until now the Integration Manager built REST calls with JSON bodies only, and an envelope profile, code maps and outputs
read JSON. A SOAP provider meant code.

## Decision
- **An operation has a protocol**: `REST` (as before) or `SOAP`. A SOAP operation is always a POST and carries its
  **SOAP call** (`soap` on `integ.operation`): the envelope **version** (1.1 or 1.2), the **SOAPAction**, the
  **operation element** of the body (e.g. `GetCitizenInfo`), its **namespace** and whether child elements are in that
  namespace (`elementFormDefault="qualified"`). The path is the service's path after the connection's address, or empty
  for the address itself.
- **The envelope is built from the inputs.** Body inputs become child elements of the operation element at their dot
  path (`request.nin` → `<request><nin>…</nin></request>`), in the order of the inputs; values are XML-escaped. SOAP 1.1
  is sent as `text/xml` with a quoted `SOAPAction` header; SOAP 1.2 as `application/soap+xml` with the action in the
  content type. Header inputs stay HTTP headers; path and query inputs work as for REST.
- **The response is read as JSON**, so everything that reads JSON keeps working — outputs, envelope profiles, code maps,
  the designer's response tree, the call log masking: the content of the body's element becomes an object whose
  properties are the child elements' local names (namespaces dropped); text elements are strings, empty or `xsi:nil`
  ones null, repeated elements lists, attributes properties, text beside attributes `$text`. A path with `[*]` reads a
  single element as a list of one, so `addresses.address[*].city` works whether one address comes back or several.
  XML is parsed without DTDs or external resolution.
- **Faults are outcomes.** A SOAP fault's code (SOAP 1.1 `faultcode`, SOAP 1.2 the most specific `Subcode` value, the
  prefix dropped — e.g. `PersonNotFound`) is looked up in the operation's and the envelope's code maps like any code,
  with the fault's text as the provider's message. Unmapped, a fault the provider blames on itself (`Server`,
  `Receiver`) is a technical error and any other a business error. A fault is an answer: it is never retried, even when
  it comes with HTTP 500.
- **WS-Security** is a connection authentication, `WS_SECURITY`: a `UsernameToken` header (OASIS username token profile
  1.0) from the secret store's `username` and `password` (`Secrets:{name}:*`, never in the database), with a fresh nonce
  and time on every attempt, the password sent as **text** or as a **digest** (Base64 of SHA-1 of nonce, time and
  password, as the profile defines) by the connection's choice. **Basic** authentication works for SOAP as for REST. The
  security header never reaches the call log or the designer's trace; a REST operation on a WS-Security connection is
  refused before anything is sent.
- **WSDL import.** The import of ADR-0041 also reads **WSDL 1.1** with its inline XML schemas (the same screen; XML text
  is recognised): the services' SOAP ports (SOAP 1.1 preferred when a port type has both bindings), document/literal
  (wrapped or not) and RPC/literal bindings, the operations with their SOAPAction, the request's elements as inputs
  (dot paths, required from `minOccurs`, personal data masked by name; lists and deep nesting left out with a warning),
  the response's elements as fields to choose outputs from (`[*]` for repeated ones, types from XSD), `extension` and
  `ref` followed. The connection's address comes from the port's address; a policy naming a `UsernameToken` suggests
  WS-Security. Schemas outside the description (`xs:import` with a location) are not fetched — the import says so.
  WSDL 2.0 is refused with a message.
- **Designer.** The request step has a REST / SOAP switch; for SOAP the version, operation element, namespace, action and
  qualified elements, inputs named *XML element*, and a preview of the **envelope** sent. *Try it*, the response tree and
  the outputs work as for REST; a pasted sample may be a SOAP envelope, read as the platform reads it.
- **Mock SOAP service** in the mock gateway: a Yakeen-like *person service* at `/soap/yakeen` with its WSDL at `?wsdl`
  (SOAP 1.1 and 1.2 bindings, a UsernameToken policy), WS-Security checked (text or digest, the time within five
  minutes), `GetCitizenInfo` (national ID and date of birth, Hijri or Gregorian) and `GetCitizenAddress` (a list of
  addresses) from the NIC sample, and faults with its own codes (`PersonNotFound`, `DateOfBirthMismatch`, `InvalidNin`,
  `wsse:FailedAuthentication`).

## Consequences
- SOAP providers are configured, tried and approved like REST ones, and their operations serve integration steps, the
  party registry and workflows unchanged (the contract `IIntegrationGateway` does not change).
- Envelopes are built from simple values: lists in requests, attributes in requests, SOAP header elements other than
  WS-Security, MTOM attachments and SOAP encoding (`use="encoded"`) are not supported; an operation needing them is
  written in code or waits for a later step.
- Signed or encrypted WS-Security messages (X.509 tokens, XML signature) and client certificates (mutual TLS) are not
  supported yet (owner's choice 3a).
- The `[*]` change reads a single value as a list of one for JSON providers too; before, it read nothing.
- Existing operations are REST (migration default) and existing connections are unchanged.

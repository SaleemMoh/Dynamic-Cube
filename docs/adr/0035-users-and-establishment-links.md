# ADR-0035: How users are created and managed, and how applicants are linked to establishments

**Status:** Accepted. Builds on [ADR-0005](0005-keycloak-authn-app-authz.md) (Keycloak signs people in, the
application decides what they may do) and [ADR-0026](0026-look-accounts-json-import.md) (Apps Control maintains
accounts through the identity provider).

## Context
Feedback asked for one clear approach: adding users, activating and deactivating them and managing passwords from Apps
Control unless an identity provider (Nafath, AD, LDAP, Entra ID…) does it, and a way to link applicants to the
establishments they act for. The pieces existed — the Users screen, Portal accounts, the Party registry — but the screens
did not say where accounts came from, and links to establishments could only be added in the registry after the person
had signed in once.

## Decision

### Two kinds of people, three places
| | Employees (internal system) | Applicants (portal) |
|---|---|---|
| Who they are in the system | An Apps Control **user** (roles, unit and position, entity and apps, data access) | A **party** in the registry (an individual), who may act for other parties |
| Their sign-in account | Keycloak realm `dynamic-cube-internal` | Keycloak realm `dynamic-cube-external` |
| Screen | Apps Control → Users | Apps Control → Portal accounts (and the Party registry) |
| Settings | `AppsControl:Identity` | `AppsControl:PortalIdentity` |

The sign-in account is never stored by Apps Control; Keycloak keeps it, and through Keycloak any directory or identity
provider connected to the realm.

### Where accounts come from: one setting per audience
- **`Provider` = `Keycloak`, `Mode` = `Managed`** — accounts are local to Keycloak and maintained from Apps Control:
  - *Employees:* **New user** creates the sign-in account at once (temporary password, or an email with a link to choose
    one; the password must be changed at the first sign-in) and drafts the user's roles, placement and data access in a
    change set. The account has no access until the change set is approved.
  - *Applicants:* **New account** creates the portal account; the user name is the national ID or iqama number (also the
    `national_id` attribute the portal's tokens carry).
  - On both, *activate / deactivate* acts at once on the account (and, for employees, on the user's status: the next call
    is refused), *set password* gives a temporary password or an email link, *sign out* ends every session.
- **`Mode` = `Federated`** — accounts come from a directory or identity provider connected to the realm (AD or LDAP
  federation, Entra ID or Nafath as identity providers). They are created, renamed and given passwords there. Apps
  Control does not offer creating accounts or setting passwords; it still activates and deactivates them, gives employees
  their roles and placement (the Apps Control user carries the same user name), and links applicants to establishments.
  Each account shows where it comes from (`LOCAL`, the federation or the identity provider).
- **`Provider` = `None`** — another team or system maintains accounts. Apps Control changes nothing on them; employees
  still get roles and placement here, and disabling the Apps Control user blocks access to the system.

The Users and Portal accounts screens open with a notice saying which of these applies and what can be done there.

### Linking applicants to establishments
An applicant acts on the portal for themselves and for every party they have an active relationship with (owner,
partner, manager, authorized delegate… — the `PARTY_RELATIONSHIP_TYPE` class says which types may act), limited to the
relationship's services and validity dates. Relationships come from two sources:
- **Registry integration (automatic).** At sign-in, and when refreshed, the context sources (e.g. the commercial
  registry) report the establishments the person owns or manages. These links are kept in step with the source: they
  end when the source no longer reports them and cannot be ended by hand.
- **Staff (manual).** Delegations and any link the registry does not know are added by staff, kept until ended or
  expired, and never touched by refreshes. They are added in either place:
  - **Portal accounts → the account → *Establishments they act for*:** by national ID, **also before the person's first
    sign-in**. The establishment is found by its CR or unified (700) number; one not registered yet is registered with
    the names given. A person not registered yet is registered with the account's name; at the first sign-in the portal
    finds the same party. Installations where applicants sign in with Nafath have no local accounts: searching a
    national ID offers to open the person to link them.
  - **Party registry → the establishment → *Add delegate or relationship*** (the person must be registered).

API: `GET /api/party/v1/people/{nationalId}/links`, `POST …/links` (CR or 700 number, relationship type, services,
validity, and names for parties not registered yet), `POST …/links/{id}/end`, with the Party registry's view and edit
privileges.

## Consequences
- One rule for every installation: the identity provider owns credentials, Apps Control owns access (roles, placement,
  data, acting contexts). Moving from local accounts to AD or Nafath changes `Mode`, not the screens.
- A person can be prepared before their first visit: account, establishments and delegations are ready when they sign in.
- Self-service requests to link an establishment (an applicant asking, staff approving) can be offered later as an
  ordinary service whose outcome adds the relationship.

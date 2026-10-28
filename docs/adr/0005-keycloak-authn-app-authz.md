# ADR-0005: Keycloak for authentication, Apps Control for authorization

**Status:** Accepted

## Context
Employees authenticate against AD/LDAP; applicants via Nafath. Authorization is rich
(roles, resource actions, field rules, value-level grants, data scopes) and business-owned.

## Decision
Keycloak (Apache-2.0) handles authentication with two realms (internal with AD/LDAP
federation, external with Nafath). Apps Control owns authorization; decisions are made
server-side and cached per session, keeping tokens small.

## Consequences
- One more component to operate (JVM), but standard OIDC everywhere.
- Authorization changes take effect without re-login (cache invalidation).

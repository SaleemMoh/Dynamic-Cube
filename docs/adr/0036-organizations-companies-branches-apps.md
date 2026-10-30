# ADR-0036: Organizations, companies, branches and apps; one place for a user's access

**Status:** Proposed — for the owner's approval before implementation. Replaces entities, business apps, channel apps
and the System / Entity / App levels of [ADR-0031](0031-entities-business-apps-levels.md) once accepted.

## Context
The existing Apps Control (the one being replaced) works with *instances*: one company group plus one product, each with
its own database, picked from a flat list after login. Companies are defined inside each instance (so again for every
product), apps are granted to instance/company pairs, and a user's access is spread over five screens (instance users,
company users, user roles per company, role menus per app/company, LOVs per screen). Small companies end up with one role
per person. Dynamic Cube today has entities, business apps and channel apps, which the owner finds too complicated.

## Decision

### Structure
```
Organization  (a client or group: eSpire Group, ADEC, a ministry)
 ├─ Companies   (legal entities: number, names, default branch)
 │   └─ Branches (business units: DXBADMCO, RUHADMCO, regional offices)
 └─ Apps        (each enabled for some companies, and within them for some branches)
```
- One organization is the usual case; an installation may hold several, and a user may work in several.
- With a single company or branch, that level is hidden in the screens.
- Customers (parties) belong to one or more companies and branches. Requests, tasks, projects and documents belong to
  one company and branch.

### Apps: one list, two kinds
| Kind | What it is | Data |
|---|---|---|
| **Dynamic Cube app** | Built with the platform: services from the Service Builder, portal, branding | In the platform database, tagged with organization, company and branch |
| **Connected app** (LedgerVault, DocVault, HRVault, PMS…) | An outside application using the platform as its provider | In its own database |

A connected app uses, as it needs: sign-in (single sign-on), users, roles, menus and branches (Apps Control), the
Action Center, workflow (cases started by the app), parties, notifications (email, SMS, in-app) and branding. It is
registered with API credentials (client id and secret in the secret store) and its menu tree. The internal system, the
portal and the field app are fixed product channels, not configured apps; "mobile" is a channel of an app, not another
app. Database connections are server configuration, never typed into screens.

### Access: one tab per user
| Company | Branches | Roles |
|---|---|---|
| Qimmat Al Khaleej | Riyadh | Manager Level 1, Accountant |
| eSpire Trading LLC | All | Viewer |

- A role given in a company grants access to that company, its chosen branches and the role's apps. There are no
  separate instance or company grants, so no inconsistent states.
- **Roles are defined once** (menu tree per app with actions, and data/LOV permissions per screen; "copy from another
  role" kept) and mean the same in every company. **Rarely, a role differs in one company:** the role editor has a
  company selector, *All companies* by default; editing for one company stores an override for that company only,
  marked on the role.
- **Personal privileges** cover small companies where everyone has their own responsibilities: on the user's Access tab,
  extra screens and actions for that person (per company), edited with the same tree. They do not appear in the role
  list, so no per-person roles.
- Bulk assignment adds or removes roles for many users; it never overwrites silently.
- Employees still have their placement in the organization structure (unit and position) for task routing.

### After sign-in: workspace switcher
Only when a user has more than one company or app: companies grouped by organization, apps as tiles, the last choice
remembered, switching from the header without signing out. Inactive combinations are never shown.

### Configuration level
Configuration (services, workflows, document types, lists of values…) belongs to an organization and optionally to some
apps; a few settings may be set per company (e.g. holidays, fees) where a module allows it. Work data is filtered by
company and branch.

## What disappears
Instances, instance and company user grants, Ref Instance, DB type / name / connection fields, business apps, the channel
apps screen, the System / Entity / App wording and the header app selector.

## Consequences
- Existing Dynamic Cube data moves over: the entity becomes the organization with one company, business apps become
  Dynamic Cube apps, users' roles become Access rows in that company.
- Connected apps need a published integration contract (sign-in, menus and privileges of the signed-in user in the
  chosen company and branch, Action Center and workflow APIs); it is a later phase after this structure.
- Physically separate databases for Dynamic Cube apps are not part of this design.

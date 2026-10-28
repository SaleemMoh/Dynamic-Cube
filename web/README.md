# Dynamic Cube — web

Angular workspace with three apps and a shared library:

| Project | Purpose | Dev server |
|---|---|---|
| `internal` | Internal system: Action Center, builders, Apps Control | `npm run start:internal` → :4200 (API :5080) |
| `external` | E-services portal for applicants | `npm run start:external` → :4201 (API :5081) |
| `field` | Field inspections (PWA later) | `npm run start:field` → :4202 (API :5080) |
| `shared` | `@dynamic-cube/shared`: i18n (Arabic-first, RTL), theme, app shell, JSONLogic, `DynamicForm` | — |

```bash
npm ci
npm test      # all projects (Vitest)
npm run lint
npm run build # all apps
```

UI components come from Angular Material + CDK (see `docs/adr/0015-angular-material.md`).
Fonts and icons are bundled from npm so on-prem deployments need no internet access.

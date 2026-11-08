# Legacy Apps Control export

Put the unzipped CSV files of the legacy Apps Control export here (`SYS_INSTANCEPROFILE.csv`, `SYS_COMPANY_PROFILE.csv`,
`DD_CLASSES.csv`, …), then start Docker as usual:

    docker compose -f deploy/docker/docker-compose.yml up -d --build

The migrator imports them the first time (replacing the installation's data) and writes here:

- `import-report.txt` — what was imported, and every row left out or changed, with the reason;
- `one-time-passwords-<date>.csv` — the sign-in accounts it created, each with a one-time password the user changes at
  first sign-in. Hand them out and delete the file.

The first run takes about 15 minutes (follow it with `docker compose -f deploy/docker/docker-compose.yml logs -f migrator`).
Check the result:

    docker compose -f deploy/docker/docker-compose.yml exec postgres psql -U dynamic_cube -d dynamic_cube -c "\dt apps.*"
    docker compose -f deploy/docker/docker-compose.yml exec postgres psql -U dynamic_cube -d dynamic_cube -c "select count(*) from apps.sys_company_profile"

Later runs leave the data alone and only create missing sign-in accounts. Docker's Keycloak keeps no data between
restarts, so after a restart the accounts are created again with new one-time passwords (a new file here).

Nothing in this folder is committed (see `.gitignore`). Plan and mapping: `docs/handover/legacy-data-import-plan.md`.

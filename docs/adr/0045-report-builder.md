# ADR-0045: Report builder and scheduled delivery

**Status:** Accepted (asked for by the owner as the first item of Phase 4; implemented). The owner decided: (1a) reports
read **registered tables and classes through the visual query builder**, with data permissions and company/branch limits
always applied; (2a) a **table with filters, grouping and totals, sorting, a chart and number tiles** (dashboards in a
second round); (3a) **Excel and CSV** export now (PDF through the print template designer later); (4a) makers build
reports through **change sets**, each report is an **Apps Control screen** roles grant and menus open, and users **save
their own filters**; (5b) **scheduled email delivery** now; (6) NHC demo reports.

## Context
Until now every list in the platform was a fixed screen of its module (requests, tasks, documents, messages). Managers
asked for numbers across them — requests per service and branch, overdue work, certificates per grade, drafts left
unfinished — and each would have meant code. The visual query builder (ADR-0039) already reads registered tables and
classes safely (registered columns only, parameters, read-only transaction, statement timeout, data scopes) for lists of
values and public endpoints (ADR-0042).

## Decision
- **A report is configuration** (`rpt.report`, area `rpt.reports`, resource `reports.config`), edited in the **report
  designer** and saved through change sets like any configuration. It holds:
  - the **query** built in the query builder (a class or registered table, joins, conditions); a condition compares a
    column with a fixed value, with one of the report's **filters** by name, or with the user's data;
  - the **columns**, each reading a column of the query, with labels and an optional **total**: count, count of distinct
    values, sum, average (numbers), minimum, maximum (not yes/no). When a column is totalled the report **groups the rows
    by the other columns**; with no totals the rows are shown as they are;
  - the **sort** (by report columns), a **totals row** (the totals over all the rows the filters keep, not only the
    page), the page size (10–500);
  - up to 12 **filters** users fill: text, number, date, a list of values (an Apps Control list, one or several values)
    or yes/no, optionally required, with a default — for dates also `TODAY`, `TODAY-n`, `TODAY+n`, `MONTH_START`,
    `LAST_MONTH_START`, `LAST_MONTH_END`, `YEAR_START`, resolved on each run in the installation's time zone. An empty
    filter is not applied;
  - up to 8 **tiles** (an aggregate over all the filtered rows, or their number) and an optional **chart** — bars, lines
    or a pie — drawing number columns along one column, over the first 100 rows (groups).
- **The query engine** is Apps Control's query compiler in a report mode, behind the kernel contract `IReportQueries`:
  an inner query of the chosen columns and an outer one grouping and totalling, plus a count, totals and tiles query, all
  parameterised, read-only, with a statement timeout and a row cap (100,000 for a run, 50,000 for an export).
- **Data permissions and company and branches always apply.** A report runs for a **viewer**: the signed-in user, or a
  stored user (a scheduled recipient) whose access is built as at sign-in. Their **data scopes** on the tables read apply
  (the same rules that filter lists and inboxes); classes keep active values serving their company; and a registered
  table with a `company_code` column keeps the rows of the company they work in, one with a `branch_code` column the
  rows of their branches (rows without a branch belong to the whole company). A viewer holding all branches passes.
- **Each report is a screen.** Publishing a report registers the resource `reports.r.{code}` (actions VIEW and EXPORT)
  under `reports`, granted to the system administrator; roles grant it like any screen, and menus may open
  `/reports/view/{code}`. Deleting or deactivating the report retires the resource. The **reports list** shows the
  reports the user may view, grouped by their category.
- **Viewer.** Filters (list filters show their list's values), the user's **saved filters** (one may be their default,
  applied on opening), tiles, the chart (drawn as SVG, no chart library), the paged table with its totals row, and
  **export** to Excel (title, filters used, header, rows, totals, right-to-left for Arabic, auto-filter) or CSV (UTF-8
  with BOM; values starting with `=`, `+`, `-`, `@` are guarded against formula injection) — export needs EXPORT.
- **Designer.** Tabs for the data, columns and totals, filters, tiles and chart, and details, with problems counted per
  tab, and a **preview** running the unsaved report with the designer's own access (`/admin/preview`).
- **Scheduled delivery** (`rpt.schedule`, resource `reports.schedules`): a report, daily / weekly (a day) / monthly
  (day 1–28) at a local time, a file format and language, filter values (empty: the report's defaults) and the
  **recipients** — users and the holders of roles. A worker job checks every minute; each slot is claimed once (a unique
  delivery per schedule and time, so several workers never send twice) and a missed slot is sent once when the worker is
  back. **Each recipient gets the report run with their own access** — a branch manager gets their branch's rows — by
  email through the Communication module with the file attached; recipients without VIEW on the report, without an
  email or inactive are skipped with the reason. The **delivery log** lists each run with each recipient's outcome, and
  a schedule can be **sent now**.
- **Email attachments.** Communication gained direct emails with attachments (`IDirectMessages`): the files go to the
  file store and the message to the outbox like any message, so it is retried and logged; up to 20 MB a message.
- **Reporting views.** Modules may offer read-only views for reports. The Documents module offers `doc.v_document_fact`:
  each document's current version as one row per simple value of its data (`grade`, `specialty`, …) with the
  document's type, number, status, dates, company and branch, so certificates can be grouped by any value they record.

## Consequences
- Managers' numbers are configuration: a new report needs no release, goes through maker/checker, and is granted per
  role like any screen.
- Reports read only registered tables and views; a table must be registered (with its columns) before a report reads it.
- Reports are read-only and bounded (timeout, row caps); an export stops at 50,000 rows.
- Not yet: dashboards of several reports, PDF output, drilling down from a group to its rows, conditional totals (such
  as "approved of all"), totals of totals across groups other than the totals row, user-chosen subscriptions.
- The NHC demo (`22-reports.json`) registers the requests, services, workflow tasks and document values tables, a list
  of services, four reports (requests by service, status and branch; overdue tasks; certificates by grade; abandoned
  drafts by step), a weekly delivery of overdue tasks to the branch managers and the audit manager, and grants the
  reports to those roles.

# ADR-0047: Dashboards and drill-down

**Status:** Accepted (report builder round 2, asked for by the owner; implemented). The owner decided: dashboards are
built **both** by makers (shared, as configuration) **and** by each user (personal, their own); a dashboard holds
**tiles, charts and short tables** of reports; it has **shared filters** that fill each report's filters; clicking a group
opens **the rows behind it**.

## Context
The report builder (ADR-0045) gives one report per screen: its tiles, charts and table. Managers asked for one page with
the numbers of several reports, filtered together, and for a way to see which requests make up a number — a bar of
"approved in March", a branch's row in a grouped table, a tile's count — without writing another report.

## Decision

### Drill-down: the rows behind a group
- A report may list the **columns of the rows behind a group** (*Report design → Columns → Columns of the rows behind a
  group*). With none, the report's columns that are not totalled and those summed, averaged or at a minimum or maximum
  are shown as they are (counted columns are left out).
- A **grouped row** of the table, a **bar, point or slice** of a chart grouped by a column, a **tile** or the **totals
  row** opens the rows behind it in a panel: paged, with the values it is narrowed by, and exported to Excel or CSV when
  the user may export the report.
- The browser sends **where** it was clicked and the group's **values by their key** (a table row's columns, a chart
  point's `x0` / `y0` and `s`). The server takes the columns and periods from the report's own definition and adds each
  value as a **parameter** condition (`column = value`, by its kind; a date grouped by a month compares its month; an empty
  value matches empty columns), up to 12. So nothing from the browser but values reaches the query, and the report's
  filters, data permissions, company and branches apply exactly as they do to the report.
- Kernel contract: `ReportShape.Matches` (`ReportMatch(Column, Bucket, Value)`), compiled by Apps Control's query
  compiler. Endpoints: `POST /reports/{code}/drill` (VIEW), `POST /reports/{code}/drill/export` (EXPORT).

### Shared dashboards (configuration)
- `rpt.dashboard` (area `rpt.reports`, resource `reports.dashboards`), edited in the **dashboard designer** and saved
  through change sets. Each published dashboard is an Apps Control resource **`reports.d.{code}`** (VIEW) under
  `reports`, granted to the system administrator; roles grant it and menus open `/reports/dashboards/{code}`. Deleting or
  deactivating it retires the resource. A report shown on a dashboard cannot be deleted.
- A dashboard has up to 12 **filters** (as a report's: text, number, date with relative defaults, a list of values,
  yes/no) and up to 24 **widgets**. A widget is a report's **tile**, **chart** or **table** (its first 1–50 rows), an
  optional title, a width in twelfths of the row (a quarter, a third, half, two thirds, the whole row).
- **How a report filter is filled**, per widget: a **fixed value**; else the **dashboard filter bound** to it — by
  default the one of the same name and kind (a list filter: the same list; one value into a one-value filter); an empty
  binding keeps the report's default even when a dashboard filter has its name; else the **report's default**. An empty
  dashboard filter leaves the report's default.
- Publishing checks the reports exist and are active (or are created in the same change set), the tile or chart exists,
  bound filters fit and fixed values are valid.

### Personal dashboards
- `rpt.personal_dashboard`: the same filters and widgets, kept for one user (up to 20, unique names, one opens first),
  saved at once — no change set. Only reports the user may view can be put on one.
- **Pin to my dashboard** on a report: its table, a tile or a chart, to an existing dashboard of their own or a new one,
  keeping the filter values the user chose (not the defaults, so relative dates stay relative) or none.

### Running
- `GET /dashboards` lists the user's own and the shared ones they may view. A dashboard's view returns the widgets of
  reports the user may **view**; the others are hidden and counted ("2 widgets are hidden").
- Each widget runs **on its own** (`POST /dashboards/{code}/widgets/{name}`, `/my-dashboards/{id}/widgets/{name}`)
  through its report, with the user's access to it, so one slow or failing report does not hold the page; it returns the
  report filter values it ran with, which a drill-down from it uses.

## Consequences
- Managers build one page per audience without code; each person can keep the numbers they watch.
- A dashboard never shows more than its reports would: every widget and every drill-down runs as the viewer, through the
  report's resource, data permissions, company and branches.
- NHC demo `27-dashboards.json`: the dashboard *Classification overview* (filters *Since* and *Service*; tiles of
  requests, fees, overdue tasks and abandoned drafts; requests per month by status and by branch; overdue tasks by step;
  the busiest services; active certificates by grade) granted to the branch requests manager and the audit manager, and
  detail columns on the requests and overdue-tasks reports. The branch manager does not see the certificates chart (not
  granted that report).

Later: dashboards sent on a schedule, a dashboard as a user's home page, dragging widgets on the grid, a widget per
saved filter, drill-down from a point drawn as a record (rows charts) to its record.

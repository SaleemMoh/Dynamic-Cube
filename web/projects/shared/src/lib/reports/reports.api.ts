import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const REPORTS_API = '/api/reports/v1';

export type ReportAggregate = 'COUNT' | 'COUNT_DISTINCT' | 'SUM' | 'AVG' | 'MIN' | 'MAX';
export type ReportFilterType = 'TEXT' | 'NUMBER' | 'DATE' | 'LIST' | 'YES_NO';
export type ReportChartType = 'BAR' | 'LINE' | 'AREA' | 'SCATTER' | 'PIE';
export type ReportBucket = 'DAY' | 'WEEK' | 'MONTH' | 'QUARTER' | 'YEAR';
export type ReportChartSort = 'X_ASC' | 'X_DESC' | 'Y_ASC' | 'Y_DESC';
export type ReportFormat = 'XLSX' | 'CSV';

export const REPORT_AGGREGATES: ReportAggregate[] = [
  'COUNT',
  'COUNT_DISTINCT',
  'SUM',
  'AVG',
  'MIN',
  'MAX',
];
export const REPORT_FILTER_TYPES: ReportFilterType[] = ['TEXT', 'NUMBER', 'DATE', 'LIST', 'YES_NO'];
export const REPORT_CHART_TYPES: ReportChartType[] = ['BAR', 'LINE', 'AREA', 'SCATTER', 'PIE'];
export const REPORT_BUCKETS: ReportBucket[] = ['DAY', 'WEEK', 'MONTH', 'QUARTER', 'YEAR'];
export const REPORT_CHART_SORTS: ReportChartSort[] = ['X_ASC', 'X_DESC', 'Y_ASC', 'Y_DESC'];

/** A column of a report: the column it reads (alias.column) and how it is totalled (none: the rows are grouped by it). */
export interface ReportColumnDef {
  name: string;
  labelAr: string;
  labelEn: string;
  column: string;
  aggregate: ReportAggregate | null;
}

/** A filter users fill; the query's conditions compare with it by name. */
export interface ReportFilterDef {
  name: string;
  labelAr: string;
  labelEn: string;
  type: ReportFilterType;
  lov: string | null;
  multiple: boolean;
  required: boolean;
  /** A value, or for dates TODAY, TODAY-30, MONTH_START, LAST_MONTH_START, LAST_MONTH_END, YEAR_START. */
  default: string | null;
}

/** A number above the report: an aggregate over the filtered rows (no column: their number). */
export interface ReportTileDef {
  name: string;
  labelAr: string;
  labelEn: string;
  column: string | null;
  aggregate: ReportAggregate;
}

export interface ReportSortDef {
  column: string;
  descending: boolean;
}

/**
 * A field on a chart axis: a column (alias.column) as it is — the rows are grouped by it, a date by a period — or
 * totalled (an aggregate; counting rows needs no column).
 */
export interface ReportChartField {
  column: string | null;
  aggregate: ReportAggregate | null;
  bucket: ReportBucket | null;
  labelAr: string | null;
  labelEn: string | null;
}

/**
 * A chart of the report's filtered rows. What is on X runs across and what is on Y runs up: one axis holds the column
 * the rows are grouped by and the other the totalled values — or each axis one column with no totals (the rows).
 */
export interface ReportChartDef {
  name: string;
  titleAr: string;
  titleEn: string;
  type: ReportChartType;
  x: ReportChartField[];
  y: ReportChartField[];
  series: ReportChartField | null;
  stacked: boolean;
  sort: ReportChartSort;
  limit: number;
  width: 'HALF' | 'FULL';
}

/** A report as it is designed (the payload of an rpt.report change). */
export interface ReportDefinition {
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  categoryAr: string | null;
  categoryEn: string | null;
  icon: string | null;
  query: Record<string, unknown> | null;
  columns: ReportColumnDef[];
  filters: ReportFilterDef[];
  tiles: ReportTileDef[];
  sort: ReportSortDef[];
  totals: boolean;
  charts: ReportChartDef[];
  pageSize: number;
  /** The columns of the rows behind a group (drill-down); empty: chosen from the report's columns. */
  details?: ReportColumnDef[];
}

/** A report in the reports list. */
export interface ReportSummary {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  categoryAr: string | null;
  categoryEn: string | null;
  icon: string | null;
}

export interface SavedReportFilter {
  id: string;
  name: string;
  values: Record<string, string | null>;
  isDefault: boolean;
}

/** What the viewer needs to show a report. */
export interface ReportView {
  code: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  filters: ReportFilterDef[];
  columns: ReportColumnDef[];
  tiles: ReportTileDef[];
  charts: ReportChartDef[];
  pageSize: number;
  canExport: boolean;
  savedFilters: SavedReportFilter[];
  /** Whether the report's groups open to the rows behind them. */
  canDrill: boolean;
}

export interface ReportRunColumn {
  name: string;
  labelAr: string;
  labelEn: string;
  type: string;
  aggregate: ReportAggregate | null;
}

export interface ReportTileValue {
  name: string;
  labelAr: string;
  labelEn: string;
  value: unknown;
}

/** A field of a chart's data: its key in the rows (x0, y0, s), labels, the kind of value, total and period. */
export interface ReportChartFieldData {
  name: string;
  labelAr: string;
  labelEn: string;
  type: string;
  aggregate: ReportAggregate | null;
  bucket: ReportBucket | null;
}

/** A chart with its rows; category names the axis grouped by (X or Y), or ROWS when the rows themselves are drawn. */
export interface ReportChartData {
  name: string;
  titleAr: string;
  titleEn: string;
  type: ReportChartType;
  stacked: boolean;
  width: 'HALF' | 'FULL';
  category: 'X' | 'Y' | 'ROWS';
  x: ReportChartFieldData[];
  y: ReportChartFieldData[];
  series: ReportChartFieldData | null;
  rows: Record<string, unknown>[];
  hasMore: boolean;
}

/** A page of a report with the number of rows, the totals row, the tiles and the chart's rows. */
export interface ReportRun {
  columns: ReportRunColumn[];
  rows: Record<string, unknown>[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
  totals: Record<string, unknown> | null;
  tiles: ReportTileValue[];
  charts: ReportChartData[];
  /** The filter values applied (defaults filled in). */
  filters: Record<string, string | null>;
}

/** Where a drill-down starts (ADR-0047). */
export type DrillSource = 'TABLE' | 'CHART' | 'TILE' | 'TOTALS';

/** Opening the rows behind a group: the report's filters, where it was clicked and the group's values by their key. */
export interface DrillRequest {
  filters: Record<string, string | null>;
  source: DrillSource;
  chart: string | null;
  values: Record<string, unknown> | null;
  page?: number;
  pageSize?: number | null;
}

/** A value the rows are narrowed by, with its column's label. */
export interface DrillValue {
  labelAr: string;
  labelEn: string;
  value: unknown;
  bucket: ReportBucket | null;
}

/** A page of the rows behind a group. */
export interface DrillRun {
  columns: ReportRunColumn[];
  rows: Record<string, unknown>[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
  filters: Record<string, string | null>;
  narrowed: DrillValue[];
}

export type DashboardWidgetKind = 'TILE' | 'CHART' | 'TABLE';

/**
 * A widget of a dashboard: a report's tile, chart or first rows; its width in twelfths of the row; the report's filters
 * filled by fixed values or bound dashboard filters (by default the one of the same name).
 */
export interface DashboardWidgetDef {
  name: string;
  kind: DashboardWidgetKind;
  report: string;
  item: string | null;
  titleAr: string | null;
  titleEn: string | null;
  span: number;
  rows: number;
  bind: Record<string, string>;
  values: Record<string, string | null>;
}

/** A shared dashboard as it is designed (the payload of an rpt.dashboard change). */
export interface DashboardDefinition {
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  icon: string | null;
  filters: ReportFilterDef[];
  widgets: DashboardWidgetDef[];
}

/** A dashboard in the list: shared ones by code, the user's own by id. */
export interface DashboardSummary {
  code: string | null;
  id: string | null;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  icon: string | null;
  personal: boolean;
  isDefault: boolean;
}

export interface DashboardWidgetView {
  name: string;
  kind: DashboardWidgetKind;
  report: string;
  reportNameAr: string;
  reportNameEn: string;
  item: string | null;
  titleAr: string;
  titleEn: string;
  span: number;
  rows: number;
  canDrill: boolean;
  canExport: boolean;
}

/** A dashboard to show: the widgets of reports the user may view; hidden counts the others. */
export interface DashboardView {
  code: string | null;
  id: string | null;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  filters: ReportFilterDef[];
  widgets: DashboardWidgetView[];
  personal: boolean;
  hidden: number;
  /** A personal dashboard's own definition, to edit. */
  definition: DashboardWidgetDef[] | null;
  isDefault: boolean;
}

export interface WidgetTable {
  columns: ReportRunColumn[];
  rows: Record<string, unknown>[];
  total: number;
  totals: Record<string, unknown> | null;
  grouped: boolean;
}

/** A widget's data with the report filter values it ran with. */
export interface WidgetData {
  name: string;
  kind: DashboardWidgetKind;
  report: string;
  filters: Record<string, string | null>;
  tile: ReportTileValue | null;
  chart: ReportChartData | null;
  table: WidgetTable | null;
}

export interface SavePersonalDashboard {
  name: string;
  filters: ReportFilterDef[];
  widgets: DashboardWidgetDef[];
  isDefault: boolean;
}

export interface ReportDeliveryRecipient {
  userName: string;
  /** SENT, SKIPPED or FAILED. */
  outcome: string;
  message: string | null;
  rows: number;
}

/** One run of a schedule: who got the report and who did not. */
export interface ReportDelivery {
  id: string;
  scheduleCode: string;
  reportCode: string;
  dueAt: string;
  startedAt: string;
  finishedAt: string | null;
  sent: number;
  skipped: number;
  failed: number;
  recipients: ReportDeliveryRecipient[];
  error: string | null;
}

/** Reports (ADR-0045): the user's reports, running, exporting and saved filters; the designer's preview; deliveries. */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);

  mine(): Observable<ReportSummary[]> {
    return this.http.get<ReportSummary[]>(`${REPORTS_API}/reports`);
  }

  view(code: string): Observable<ReportView> {
    return this.http.get<ReportView>(`${REPORTS_API}/reports/${encodeURIComponent(code)}`);
  }

  run(
    code: string,
    filters: Record<string, string | null>,
    page = 1,
    pageSize?: number,
  ): Observable<ReportRun> {
    return this.http.post<ReportRun>(`${REPORTS_API}/reports/${encodeURIComponent(code)}/run`, {
      filters,
      page,
      pageSize: pageSize ?? null,
    });
  }

  export(
    code: string,
    filters: Record<string, string | null>,
    format: ReportFormat,
    lang: 'ar' | 'en',
  ): Observable<Blob> {
    const params = new HttpParams().set('format', format).set('lang', lang);
    return this.http.post(`${REPORTS_API}/reports/${encodeURIComponent(code)}/export`, filters, {
      params,
      responseType: 'blob',
    });
  }

  saveFilter(
    code: string,
    name: string,
    values: Record<string, string | null>,
    isDefault: boolean,
  ): Observable<SavedReportFilter> {
    return this.http.post<SavedReportFilter>(
      `${REPORTS_API}/reports/${encodeURIComponent(code)}/saved-filters`,
      {
        name,
        values,
        isDefault,
      },
    );
  }

  deleteFilter(code: string, id: string): Observable<void> {
    return this.http.delete<void>(
      `${REPORTS_API}/reports/${encodeURIComponent(code)}/saved-filters/${id}`,
    );
  }

  /** The rows behind a group of the report (ADR-0047). */
  drill(code: string, request: DrillRequest): Observable<DrillRun> {
    return this.http.post<DrillRun>(
      `${REPORTS_API}/reports/${encodeURIComponent(code)}/drill`,
      request,
    );
  }

  exportDrill(
    code: string,
    request: DrillRequest,
    format: ReportFormat,
    lang: 'ar' | 'en',
  ): Observable<Blob> {
    const params = new HttpParams().set('format', format).set('lang', lang);
    return this.http.post(
      `${REPORTS_API}/reports/${encodeURIComponent(code)}/drill/export`,
      request,
      {
        params,
        responseType: 'blob',
      },
    );
  }

  /** The dashboards the user may open: their own, then the shared ones granted to them. */
  dashboards(): Observable<DashboardSummary[]> {
    return this.http.get<DashboardSummary[]>(`${REPORTS_API}/dashboards`);
  }

  dashboard(code: string): Observable<DashboardView> {
    return this.http.get<DashboardView>(`${REPORTS_API}/dashboards/${encodeURIComponent(code)}`);
  }

  myDashboard(id: string): Observable<DashboardView> {
    return this.http.get<DashboardView>(`${REPORTS_API}/my-dashboards/${id}`);
  }

  /** One widget run with the dashboard's filter values (a shared dashboard by code, the user's own by id). */
  runWidget(
    dashboard: { code: string | null; id: string | null },
    widget: string,
    filters: Record<string, string | null>,
  ): Observable<WidgetData> {
    const base = dashboard.id
      ? `${REPORTS_API}/my-dashboards/${dashboard.id}`
      : `${REPORTS_API}/dashboards/${encodeURIComponent(dashboard.code ?? '')}`;
    return this.http.post<WidgetData>(`${base}/widgets/${encodeURIComponent(widget)}`, { filters });
  }

  saveMyDashboard(id: string | null, dashboard: SavePersonalDashboard): Observable<DashboardView> {
    return id
      ? this.http.put<DashboardView>(`${REPORTS_API}/my-dashboards/${id}`, dashboard)
      : this.http.post<DashboardView>(`${REPORTS_API}/my-dashboards`, dashboard);
  }

  deleteMyDashboard(id: string): Observable<void> {
    return this.http.delete<void>(`${REPORTS_API}/my-dashboards/${id}`);
  }

  /** Adds a report's tile, chart or table to one of the user's dashboards (a new one named newName when none). */
  pin(
    dashboard: string | null,
    newName: string | null,
    widget: DashboardWidgetDef,
  ): Observable<DashboardView> {
    return this.http.post<DashboardView>(`${REPORTS_API}/my-dashboards/pin`, {
      dashboard,
      newName,
      widget,
    });
  }

  /** Runs an unsaved report within the designer's own access (first page). */
  preview(report: ReportDefinition, filters: Record<string, string | null>): Observable<ReportRun> {
    return this.http.post<ReportRun>(`${REPORTS_API}/admin/preview`, { report, filters });
  }

  /** One chart of an unsaved report (the chart designer's live preview). */
  previewChart(
    report: ReportDefinition,
    chart: string,
    filters: Record<string, string | null>,
  ): Observable<ReportChartData> {
    return this.http.post<ReportChartData>(`${REPORTS_API}/admin/preview-chart`, {
      report,
      chart,
      filters,
    });
  }

  deliveries(
    schedule?: string,
    skip = 0,
    take = 50,
  ): Observable<{ items: ReportDelivery[]; total: number }> {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (schedule) params = params.set('schedule', schedule);
    return this.http.get<{ items: ReportDelivery[]; total: number }>(
      `${REPORTS_API}/admin/deliveries`,
      { params },
    );
  }

  sendNow(schedule: string): Observable<ReportDelivery> {
    return this.http.post<ReportDelivery>(
      `${REPORTS_API}/admin/schedules/${encodeURIComponent(schedule)}/send-now`,
      {},
    );
  }
}

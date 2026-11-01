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

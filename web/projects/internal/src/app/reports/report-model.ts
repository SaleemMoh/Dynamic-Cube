import {
  REPORT_AGGREGATES,
  ReportAggregate,
  ReportColumnDef,
  ReportDefinition,
  ReportFilterDef,
  ReportTileDef,
} from '@dynamic-cube/shared';
import { QuerySourceInfo } from '../apps-control/lovs/lov-builder-model';
import {
  EndpointQuery,
  endpointColumns,
  fromQuery,
  toQuery,
} from '../integration/api-endpoint-model';
import { chartPayload, checkCharts } from './report-chart-model';

/** The parts of the report designer, each with its own problems. */
export type ReportPart = 'data' | 'columns' | 'filters' | 'tiles' | 'charts' | 'details';
export const REPORT_PARTS: ReportPart[] = [
  'data',
  'columns',
  'filters',
  'tiles',
  'charts',
  'details',
];

export interface ReportProblem {
  part: ReportPart;
  key: string;
  params?: Record<string, string | number>;
}

/** Relative dates a date filter may default to (the server resolves them on each run). */
export const DATE_DEFAULTS = [
  'TODAY',
  'TODAY-7',
  'TODAY-30',
  'MONTH_START',
  'LAST_MONTH_START',
  'LAST_MONTH_END',
  'YEAR_START',
];

export const MAX_FILTERS = 12;
export const MAX_TILES = 8;
const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;
const NAME = /^[A-Za-z][A-Za-z0-9_]{0,63}$/;
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const RELATIVE = /^(TODAY([+-]\d{1,4})?|MONTH_START|LAST_MONTH_START|LAST_MONTH_END|YEAR_START)$/;

/** A new report: no data yet, 50 rows a page, a totals row. */
export function emptyReport(): ReportDefinition {
  return {
    nameAr: '',
    nameEn: '',
    descriptionAr: null,
    descriptionEn: null,
    categoryAr: null,
    categoryEn: null,
    icon: 'analytics',
    query: null,
    columns: [],
    filters: [],
    tiles: [],
    sort: [],
    totals: true,
    charts: [],
    pageSize: 50,
  };
}

/** A stored report with every list present (copies, so edits never touch the row). */
export function normalizeReport(row: Record<string, unknown>): ReportDefinition {
  const def = { ...emptyReport(), ...row } as ReportDefinition;
  return {
    ...def,
    query:
      typeof def.query === 'string'
        ? (JSON.parse(def.query) as Record<string, unknown>)
        : def.query,
    columns: (def.columns ?? []).map((c) => ({ ...c, aggregate: c.aggregate || null })),
    filters: (def.filters ?? []).map((f) => ({
      ...f,
      lov: f.lov || null,
      default: f.default || null,
    })),
    tiles: (def.tiles ?? []).map((t) => ({ ...t, column: t.column || null })),
    sort: (def.sort ?? []).map((s) => ({ ...s })),
    charts: (def.charts ?? []).map((c) => ({
      ...c,
      x: (c.x ?? []).map((f) => ({ ...f })),
      y: (c.y ?? []).map((f) => ({ ...f })),
      series: c.series ? { ...c.series } : null,
    })),
    pageSize: def.pageSize || 50,
  };
}

/** The builder's query state of a report. */
export function reportQuery(def: ReportDefinition): EndpointQuery {
  return fromQuery(def.query);
}

/** The payload saved or previewed: trimmed texts, the query from the builder, empty optional parts left out. */
export function reportPayload(def: ReportDefinition, query: EndpointQuery): ReportDefinition {
  const text = (v: string | null | undefined) => v?.trim() || null;
  const built = toQuery(query);
  return {
    ...def,
    nameAr: def.nameAr.trim(),
    nameEn: def.nameEn.trim(),
    descriptionAr: text(def.descriptionAr),
    descriptionEn: text(def.descriptionEn),
    categoryAr: text(def.categoryAr),
    categoryEn: text(def.categoryEn),
    icon: text(def.icon),
    // The report's own sort orders its rows; the query's sort is not used.
    query: { ...built, sort: [] },
    columns: def.columns.map((c) => ({
      ...c,
      name: c.name.trim(),
      labelAr: c.labelAr.trim(),
      labelEn: c.labelEn.trim(),
      aggregate: c.aggregate || null,
    })),
    filters: def.filters.map((f) => ({
      ...f,
      name: f.name.trim(),
      labelAr: f.labelAr.trim(),
      labelEn: f.labelEn.trim(),
      lov: f.type === 'LIST' ? text(f.lov) : null,
      multiple: f.type === 'LIST' && f.multiple,
      default: text(f.default),
    })),
    tiles: def.tiles.map((t) => ({
      ...t,
      name: t.name.trim(),
      labelAr: t.labelAr.trim(),
      labelEn: t.labelEn.trim(),
      column: text(t.column),
    })),
    sort: def.sort.filter((s) => s.column),
    charts: def.charts.map(chartPayload),
  };
}

/** The column category (TEXT, NUMBER, DATE, BOOL, JSON) of a source column. */
export type SourceColumnRef = ReturnType<typeof endpointColumns>[number];

/** Aggregates that fit a column of this category (as the server checks them). */
export function aggregatesFor(category: string | undefined): ReportAggregate[] {
  if (category === 'JSON') return ['COUNT', 'COUNT_DISTINCT'];
  return REPORT_AGGREGATES.filter((a) => {
    if ((a === 'SUM' || a === 'AVG') && category !== 'NUMBER') return false;
    if ((a === 'MIN' || a === 'MAX') && category === 'BOOL') return false;
    return true;
  });
}

/** Whether the report is grouped: some column is totalled, so rows are grouped by the others. */
export function isGrouped(columns: ReportColumnDef[]): boolean {
  return columns.some((c) => !!c.aggregate);
}

/** The kind of value a report column holds: counts, sums and averages are numbers, others keep their column's kind. */
export function resultCategory(column: ReportColumnDef, sourceColumns: SourceColumnRef[]): string {
  if (column.aggregate && column.aggregate !== 'MIN' && column.aggregate !== 'MAX') return 'NUMBER';
  return sourceColumns.find((c) => c.ref === column.column)?.category ?? 'TEXT';
}

/** The report's number columns (what a chart can draw). */
export function numberColumns(
  def: ReportDefinition,
  sourceColumns: SourceColumnRef[],
): ReportColumnDef[] {
  return def.columns.filter((c) => resultCategory(c, sourceColumns) === 'NUMBER');
}

/** A unique camelCase name from a column ref (r.service_code → serviceCode), with a suffix when taken. */
export function uniqueName(base: string, taken: string[]): string {
  const camel = (base.split('.').pop() ?? base)
    .replace(/[^A-Za-z0-9_]/g, '')
    .replace(/_([a-z0-9])/g, (_, c: string) => c.toUpperCase());
  const start = /^[A-Za-z]/.test(camel) ? camel : 'c' + camel;
  let name = start || 'column';
  for (let i = 2; taken.includes(name); i++) name = `${start}${i}`;
  return name;
}

/** A report column reading the source column, named and labelled after it. */
export function columnFor(
  ref: string,
  sourceColumns: SourceColumnRef[],
  existing: ReportColumnDef[],
  aggregate: ReportAggregate | null = null,
): ReportColumnDef {
  const source = sourceColumns.find((c) => c.ref === ref);
  const info = source?.source.columns.find((c) => c.name === source.name);
  const name = uniqueName(
    aggregate ? `${ref.split('.').pop()}_${aggregate.toLowerCase()}` : ref,
    existing.map((c) => c.name),
  );
  return {
    name,
    labelAr: info?.labelAr || source?.name || name,
    labelEn: info?.labelEn || source?.name || name,
    column: ref,
    aggregate,
  };
}

/** A new filter with a free name. */
export function newFilter(existing: ReportFilterDef[]): ReportFilterDef {
  const name = uniqueName(
    'filter',
    existing.map((f) => f.name),
  );
  return {
    name,
    labelAr: '',
    labelEn: '',
    type: 'TEXT',
    lov: null,
    multiple: false,
    required: false,
    default: null,
  };
}

/** A new tile counting the rows. */
export function newTile(existing: ReportTileDef[]): ReportTileDef {
  const name = uniqueName(
    'rows',
    existing.map((t) => t.name),
  );
  return { name, labelAr: 'العدد', labelEn: 'Rows', column: null, aggregate: 'COUNT' };
}

/** Renames a column everywhere the report refers to it (the sort; charts read the data's columns, not the report's). */
export function renameColumn(def: ReportDefinition, from: string, to: string): ReportDefinition {
  if (from === to) return def;
  return { ...def, sort: def.sort.map((s) => (s.column === from ? { ...s, column: to } : s)) };
}

/** Removes a column and what refers to it (the sort). */
export function removeColumn(def: ReportDefinition, index: number): ReportDefinition {
  const name = def.columns[index]?.name;
  return {
    ...def,
    columns: def.columns.filter((_, i) => i !== index),
    sort: def.sort.filter((s) => s.column !== name),
  };
}

/** Whether a filter default fits the filter's kind (a relative date for dates). */
export function defaultFits(filter: ReportFilterDef): boolean {
  const value = filter.default?.trim();
  if (!value) return true;
  switch (filter.type) {
    case 'NUMBER':
      return !Number.isNaN(Number(value));
    case 'DATE':
      return DATE.test(value) || RELATIVE.test(value.toUpperCase());
    case 'YES_NO':
      return value === 'true' || value === 'false';
    default:
      return true;
  }
}

/** The checks the server makes that can be made here (the server check covers the query itself). */
export function checkReport(
  code: string,
  def: ReportDefinition,
  query: EndpointQuery,
  isNew: boolean,
  sources: QuerySourceInfo[],
): ReportProblem[] {
  const problems: ReportProblem[] = [];
  const p = (part: ReportPart, key: string, params?: ReportProblem['params']) =>
    problems.push({ part, key, params });
  const sourceColumns = endpointColumns(query, sources);

  if (isNew && !CODE.test(code)) p('details', 'rp.problem.code');
  if (!def.nameAr?.trim() || !def.nameEn?.trim()) p('details', 'rp.problem.names');

  if (!query.from.code) p('data', 'rp.problem.source');
  const filterNames = new Set(def.filters.map((f) => f.name));
  query.conditions.forEach((c, i) => {
    if (
      c.source === 'PARAM' &&
      c.op !== 'IS_EMPTY' &&
      c.op !== 'IS_NOT_EMPTY' &&
      !filterNames.has(c.param)
    )
      p('data', 'rp.problem.conditionFilter', { n: i + 1 });
  });

  if (!def.columns.length) p('columns', 'rp.problem.columns');
  const names = new Set<string>();
  def.columns.forEach((c, i) => {
    const n = i + 1;
    if (!NAME.test(c.name)) p('columns', 'rp.problem.columnName', { n });
    else if (names.has(c.name)) p('columns', 'rp.problem.columnTwice', { name: c.name });
    names.add(c.name);
    if (!c.column) p('columns', 'rp.problem.columnSource', { n });
    if (!c.labelAr?.trim() || !c.labelEn?.trim()) p('columns', 'rp.problem.columnLabels', { n });
    const category = sourceColumns.find((s) => s.ref === c.column)?.category;
    if (c.aggregate && c.column && !aggregatesFor(category).includes(c.aggregate))
      p('columns', 'rp.problem.aggregate', { n });
  });
  def.sort.forEach((s, i) => {
    if (s.column && !names.has(s.column)) p('columns', 'rp.problem.sort', { n: i + 1 });
  });
  if (!(def.pageSize >= 10 && def.pageSize <= 500)) p('columns', 'rp.problem.pageSize');

  if (def.filters.length > MAX_FILTERS)
    p('filters', 'rp.problem.filterCount', { max: MAX_FILTERS });
  const seen = new Set<string>();
  def.filters.forEach((f, i) => {
    const n = i + 1;
    if (!NAME.test(f.name)) p('filters', 'rp.problem.filterName', { n });
    else if (seen.has(f.name)) p('filters', 'rp.problem.filterTwice', { name: f.name });
    seen.add(f.name);
    if (!f.labelAr?.trim() || !f.labelEn?.trim()) p('filters', 'rp.problem.filterLabels', { n });
    if (f.type === 'LIST' && !f.lov) p('filters', 'rp.problem.filterLov', { n });
    if (!defaultFits(f)) p('filters', 'rp.problem.filterDefault', { n });
  });

  if (def.tiles.length > MAX_TILES) p('tiles', 'rp.problem.tileCount', { max: MAX_TILES });
  const tileNames = new Set<string>();
  def.tiles.forEach((t, i) => {
    const n = i + 1;
    if (!NAME.test(t.name)) p('tiles', 'rp.problem.tileName', { n });
    else if (tileNames.has(t.name)) p('tiles', 'rp.problem.tileTwice', { name: t.name });
    tileNames.add(t.name);
    if (!t.labelAr?.trim() || !t.labelEn?.trim()) p('tiles', 'rp.problem.tileLabels', { n });
    if (t.aggregate !== 'COUNT' && !t.column) p('tiles', 'rp.problem.tileColumn', { n });
    const category = sourceColumns.find((s) => s.ref === t.column)?.category;
    if (t.column && !aggregatesFor(category).includes(t.aggregate))
      p('tiles', 'rp.problem.tileAggregate', { n });
  });
  for (const c of checkCharts(def.charts)) p('charts', c.key, { n: c.chart + 1 });
  return problems;
}

// ---------------------------------------------------------------------------------------------- viewer helpers

/** A cell's text: numbers grouped (up to two decimals), dates as YYYY-MM-DD (with the time when there is one), yes/no. */
export function formatValue(value: unknown, type: string, lang: 'ar' | 'en'): string {
  if (value === null || value === undefined || value === '') return '';
  if (type === 'NUMBER' && (typeof value === 'number' || !Number.isNaN(Number(value)))) {
    return Number(value).toLocaleString(lang === 'ar' ? 'ar-SA-u-nu-latn' : 'en-US', {
      maximumFractionDigits: 2,
    });
  }
  if (type === 'BOOLEAN' || typeof value === 'boolean') {
    const yes = value === true || value === 'true';
    return lang === 'ar' ? (yes ? 'نعم' : 'لا') : yes ? 'Yes' : 'No';
  }
  if (type === 'DATE' && typeof value === 'string') {
    const match = /^(\d{4}-\d{2}-\d{2})(?:[T ](\d{2}:\d{2}))?/.exec(value);
    if (match) return match[2] && match[2] !== '00:00' ? `${match[1]} ${match[2]}` : match[1];
  }
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

/** A number for a chart (non-numbers count as 0). */
export function toNumber(value: unknown): number {
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : 0;
}

/** A round top for a value axis (1, 2, 2.5, 5 × 10ⁿ) and its ticks. */
export function niceScale(max: number, ticks = 4): { max: number; ticks: number[] } {
  if (!(max > 0)) return { max: 1, ticks: [0, 1] };
  const raw = max / ticks;
  const power = Math.pow(10, Math.floor(Math.log10(raw)));
  const step = ([1, 2, 2.5, 5, 10].find((m) => m * power >= raw) ?? 10) * power;
  const top = Math.ceil(max / step) * step;
  const values: number[] = [];
  for (let v = 0; v <= top + step / 2; v += step) values.push(Math.round(v * 1e6) / 1e6);
  return { max: top, ticks: values };
}

/** Slices of a pie for these values: SVG paths on a circle of radius r centred at (r, r), negative values left out. */
export function pieSlices(values: number[], r = 80): { path: string; share: number }[] {
  const positive = values.map((v) => (v > 0 ? v : 0));
  const total = positive.reduce((a, b) => a + b, 0);
  if (!total) return values.map(() => ({ path: '', share: 0 }));
  let angle = -Math.PI / 2;
  return positive.map((v) => {
    const share = v / total;
    if (!share) return { path: '', share };
    if (share >= 0.9999) {
      return { path: `M ${r} 0 A ${r} ${r} 0 1 1 ${r - 0.01} 0 Z`, share };
    }
    const start = angle;
    angle += share * 2 * Math.PI;
    const [x1, y1] = [r + r * Math.cos(start), r + r * Math.sin(start)];
    const [x2, y2] = [r + r * Math.cos(angle), r + r * Math.sin(angle)];
    const large = share > 0.5 ? 1 : 0;
    const f = (n: number) => Math.round(n * 100) / 100;
    return {
      path: `M ${r} ${r} L ${f(x1)} ${f(y1)} A ${r} ${r} 0 ${large} 1 ${f(x2)} ${f(y2)} Z`,
      share,
    };
  });
}

/** Several values of a list filter as the server takes them (comma separated), and back. */
export function joinValues(values: string[]): string | null {
  const kept = values.map((v) => v.trim()).filter(Boolean);
  return kept.length ? kept.join(',') : null;
}

export function splitValues(value: string | null | undefined): string[] {
  return (value ?? '')
    .split(',')
    .map((v) => v.trim())
    .filter(Boolean);
}

/** The filter values sent: empty ones left out. */
export function cleanFilters(values: Record<string, string | null>): Record<string, string | null> {
  const result: Record<string, string | null> = {};
  for (const [k, v] of Object.entries(values))
    if (v !== null && v !== undefined && String(v).trim() !== '') result[k] = String(v).trim();
  return result;
}

/** A file name for an export: the report's code, the date and the format's extension. */
export function exportFileName(code: string, format: 'XLSX' | 'CSV', today = new Date()): string {
  const date = today.toISOString().slice(0, 10);
  return `${code}-${date}.${format === 'CSV' ? 'csv' : 'xlsx'}`;
}

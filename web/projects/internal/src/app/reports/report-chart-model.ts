import { ReportAggregate, ReportChartDef, ReportChartField } from '@dynamic-cube/shared';
import { aggregatesFor, uniqueName } from './report-model';

/** Where a field is dropped in the chart designer. */
export type ChartZone = 'x' | 'y' | 'series';

/** A column the chart designer offers: its ref (alias.column), kind and labels. */
export interface ChartSourceField {
  ref: string;
  category: string;
  labelAr: string;
  labelEn: string;
}

/** The "number of rows" field: a count with no column. */
export const ROW_COUNT = '__rows__';

export const MAX_CHARTS = 6;
const NAME = /^[A-Za-z][A-Za-z0-9_]{0,63}$/;

export interface ChartProblem {
  chart: number;
  key: string;
}

/** A new chart: bars, half width, along X ascending, 50 points. */
export function emptyChart(existing: ReportChartDef[]): ReportChartDef {
  const name = uniqueName(
    'chart',
    existing.map((c) => c.name),
  );
  return {
    name,
    titleAr: '',
    titleEn: '',
    type: 'BAR',
    x: [],
    y: [],
    series: null,
    stacked: false,
    sort: 'X_ASC',
    limit: 50,
    width: 'HALF',
  };
}

/** A copy of a chart (lists copied, so edits never touch the original). */
export function copyChart(chart: ReportChartDef): ReportChartDef {
  return {
    ...chart,
    x: chart.x.map((f) => ({ ...f })),
    y: chart.y.map((f) => ({ ...f })),
    series: chart.series ? { ...chart.series } : null,
  };
}

function isTotal(field: ReportChartField): boolean {
  return !!field.aggregate;
}

/**
 * The axis the rows are grouped by: X or Y (one field there, not totalled, every field of the other axis totalled),
 * ROWS (one field on each axis, neither totalled: the rows themselves), or null when the axes fit neither (as the server
 * decides).
 */
export function chartMode(chart: ReportChartDef): 'X' | 'Y' | 'ROWS' | null {
  const plain = (axis: ReportChartField[]) => axis.length === 1 && !isTotal(axis[0]);
  const totals = (axis: ReportChartField[]) => axis.length > 0 && axis.every(isTotal);
  if (plain(chart.x) && plain(chart.y)) return 'ROWS';
  if (plain(chart.x) && totals(chart.y)) return 'X';
  if (plain(chart.y) && totals(chart.x)) return 'Y';
  return null;
}

/** A field as it is (grouped by; a date by month), labelled after its column. */
export function plainField(source: ChartSourceField): ReportChartField {
  return {
    column: source.ref,
    aggregate: null,
    bucket: source.category === 'DATE' ? 'MONTH' : null,
    labelAr: source.labelAr,
    labelEn: source.labelEn,
  };
}

/** A field totalled: numbers summed, other columns counted; the "number of rows" counted with no column. */
export function totalField(source: ChartSourceField | null): ReportChartField {
  if (!source || source.ref === ROW_COUNT)
    return { column: null, aggregate: 'COUNT', bucket: null, labelAr: 'العدد', labelEn: 'Count' };
  const aggregate: ReportAggregate = source.category === 'NUMBER' ? 'SUM' : 'COUNT';
  return {
    column: source.ref,
    aggregate,
    bucket: null,
    labelAr: source.labelAr,
    labelEn: source.labelEn,
  };
}

/**
 * Drops a field on an axis or on "colour by". On an axis: when the other axis holds the column grouped by, the field
 * is a value (totalled); on an empty axis it is the column grouped by (a number dropped where the other axis is empty
 * too waits as it is — a number on each axis draws the rows themselves); an axis grouped by keeps one field, so the
 * new one replaces it; next to values it is one more value. "Number of rows" is always a value.
 */
export function dropField(
  chart: ReportChartDef,
  zone: ChartZone,
  source: ChartSourceField,
): ReportChartDef {
  const next = copyChart(chart);
  if (zone === 'series') {
    if (source.ref !== ROW_COUNT) next.series = plainField(source);
    return next;
  }
  const axis = zone === 'x' ? next.x : next.y;
  const other = zone === 'x' ? next.y : next.x;
  const otherGroups = other.length === 1 && !isTotal(other[0]);
  let field: ReportChartField;
  if (source.ref === ROW_COUNT || (otherGroups && axis.every(isTotal))) field = totalField(source);
  else if (axis.length === 0 || (axis.length === 1 && !isTotal(axis[0])))
    field = plainField(source);
  else field = totalField(source);

  if (!isTotal(field)) axis.splice(0, axis.length, field);
  else if (axis.length === 1 && !isTotal(axis[0]) && !otherGroups) {
    // A value dropped on the axis grouped by: the grouped column moves to the other axis when it is empty.
    if (other.length === 0) other.push(axis[0]);
    axis.splice(0, axis.length, field);
  } else axis.push(field);
  return next;
}

/** Removes a field from an axis (or "colour by"). */
export function removeField(chart: ReportChartDef, zone: ChartZone, index = 0): ReportChartDef {
  const next = copyChart(chart);
  if (zone === 'series') next.series = null;
  else (zone === 'x' ? next.x : next.y).splice(index, 1);
  return next;
}

/** Changes a field of an axis (or "colour by"); a totalled field loses its period, a field as it is keeps none but dates. */
export function setField(
  chart: ReportChartDef,
  zone: ChartZone,
  index: number,
  change: Partial<ReportChartField>,
  category?: string,
): ReportChartDef {
  const next = copyChart(chart);
  const fields =
    zone === 'series' ? (next.series ? [next.series] : []) : zone === 'x' ? next.x : next.y;
  const field = fields[index];
  if (!field) return next;
  Object.assign(field, change);
  if (field.aggregate) field.bucket = null;
  else if (category !== undefined && category !== 'DATE') field.bucket = null;
  if (!field.column && field.aggregate !== 'COUNT') field.aggregate = 'COUNT';
  if (zone === 'series') next.series = field;
  return next;
}

/** Swaps the X and Y axes (time down the Y axis with averages across, or the other way round); the order follows. */
export function swapAxes(chart: ReportChartDef): ReportChartDef {
  const next = copyChart(chart);
  [next.x, next.y] = [next.y, next.x];
  next.sort = ((next.sort.startsWith('X') ? 'Y' : 'X') +
    next.sort.slice(1)) as ReportChartDef['sort'];
  return next;
}

/** Aggregates offered for a field of this kind ('' stands for "as it is"). */
export function fieldAggregates(
  category: string | undefined,
  hasColumn: boolean,
): ReportAggregate[] {
  return hasColumn ? aggregatesFor(category) : ['COUNT'];
}

/** The checks the server makes that can be made here (the server check covers the columns themselves). */
export function checkCharts(charts: ReportChartDef[]): ChartProblem[] {
  const problems: ChartProblem[] = [];
  const p = (chart: number, key: string) => problems.push({ chart, key });
  if (charts.length > MAX_CHARTS) p(-1, 'rp.problem.chartCount');
  const names = new Set<string>();
  charts.forEach((c, i) => {
    if (!NAME.test(c.name)) p(i, 'rp.problem.chartName');
    else if (names.has(c.name)) p(i, 'rp.problem.chartTwice');
    names.add(c.name);
    if (!c.titleAr?.trim() || !c.titleEn?.trim()) p(i, 'rp.problem.chartTitles');
    if (!(c.limit >= 1 && c.limit <= 500)) p(i, 'rp.problem.chartLimit');
    if (!c.x.length || !c.y.length) {
      p(i, 'rp.problem.chartAxes');
      return;
    }
    const mode = chartMode(c);
    if (!mode) {
      p(i, 'rp.problem.chartMode');
      return;
    }
    const values = mode === 'X' ? c.y : c.x;
    if (values.length > MAX_CHARTS) p(i, 'rp.problem.chartValuesCount');
    if (c.type === 'PIE' && (mode === 'ROWS' || values.length > 1 || c.series))
      p(i, 'rp.problem.chartPie');
    if (c.series && mode !== 'ROWS' && values.length > 1) p(i, 'rp.problem.chartSeries');
    for (const f of [...c.x, ...c.y])
      if (!f.column && f.aggregate !== 'COUNT') p(i, 'rp.problem.chartColumn');
  });
  return problems;
}

/** The chart's payload: labels trimmed (empty: the column's name), an empty bucket left out. */
export function chartPayload(chart: ReportChartDef): ReportChartDef {
  const field = (f: ReportChartField): ReportChartField => ({
    column: f.column || null,
    aggregate: f.aggregate || null,
    bucket: f.aggregate ? null : f.bucket || null,
    labelAr: f.labelAr?.trim() || null,
    labelEn: f.labelEn?.trim() || null,
  });
  return {
    ...chart,
    titleAr: chart.titleAr.trim(),
    titleEn: chart.titleEn.trim(),
    x: chart.x.map(field),
    y: chart.y.map(field),
    series: chart.series ? field(chart.series) : null,
    stacked: chart.stacked && (chart.type === 'BAR' || chart.type === 'AREA'),
  };
}

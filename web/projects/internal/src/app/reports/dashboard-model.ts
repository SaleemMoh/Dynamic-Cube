import {
  DashboardDefinition,
  DashboardWidgetDef,
  DashboardWidgetKind,
  ReportFilterDef,
  ReportView,
} from '@dynamic-cube/shared';

/** The widths a widget may take, in twelfths of the row. */
export const WIDGET_SPANS = [3, 4, 6, 8, 12];

/** A widget's default width: a quarter for a tile, half for a chart, the whole row for a table. */
export function defaultSpan(kind: DashboardWidgetKind): number {
  return kind === 'TILE' ? 3 : kind === 'TABLE' ? 12 : 6;
}

export function emptyWidget(
  name: string,
  report = '',
  kind: DashboardWidgetKind = 'CHART',
): DashboardWidgetDef {
  return {
    name,
    kind,
    report,
    item: null,
    titleAr: null,
    titleEn: null,
    span: defaultSpan(kind),
    rows: 10,
    bind: {},
    values: {},
  };
}

/** A dashboard as edited: missing parts filled in. */
export function normalizeDashboard(row: Record<string, unknown>): DashboardDefinition {
  const def = row as Partial<DashboardDefinition>;
  return {
    nameAr: def.nameAr ?? '',
    nameEn: def.nameEn ?? '',
    descriptionAr: def.descriptionAr ?? null,
    descriptionEn: def.descriptionEn ?? null,
    icon: def.icon ?? null,
    filters: (def.filters ?? []).map((f) => ({
      ...f,
      lov: f.lov || null,
      default: f.default || null,
    })),
    widgets: (def.widgets ?? []).map((w) => ({
      ...emptyWidget(w.name, w.report, w.kind),
      ...w,
      bind: { ...(w.bind ?? {}) },
      values: { ...(w.values ?? {}) },
    })),
  };
}

/** A unique widget name from a stem (letters and digits). */
export function widgetName(stem: string, taken: string[]): string {
  let base = (stem || 'w').replace(/[^A-Za-z0-9]/g, '');
  if (!/^[A-Za-z]/.test(base)) base = 'w' + base;
  base = base.slice(0, 50);
  let name = base;
  for (let n = 2; taken.includes(name); n++) name = `${base}${n}`;
  return name;
}

/**
 * How a report filter of a widget is filled: a fixed value, a bound dashboard filter (by default the one of the same
 * name and kind), or the report's default.
 */
export type FilterSource =
  { kind: 'FIXED'; value: string } | { kind: 'BOUND'; filter: string } | { kind: 'DEFAULT' };

export function fits(source: ReportFilterDef, target: ReportFilterDef): boolean {
  return (
    source.type === target.type &&
    (source.type !== 'LIST' || (source.lov === target.lov && (!source.multiple || target.multiple)))
  );
}

export function filterSource(
  widget: DashboardWidgetDef,
  filter: ReportFilterDef,
  dashboard: ReportFilterDef[],
): FilterSource {
  const fixed = widget.values[filter.name];
  if (fixed) return { kind: 'FIXED', value: fixed };
  // An empty binding keeps the report's default even when a dashboard filter has the same name.
  const bound =
    filter.name in widget.bind
      ? widget.bind[filter.name]
      : dashboard.find((d) => d.name === filter.name && fits(d, filter))?.name;
  return bound ? { kind: 'BOUND', filter: bound } : { kind: 'DEFAULT' };
}

/** A widget with one of its report filters set to be filled from a source. */
export function withFilterSource(
  widget: DashboardWidgetDef,
  filter: ReportFilterDef,
  source: FilterSource,
  dashboard: ReportFilterDef[],
): DashboardWidgetDef {
  const bind = { ...widget.bind };
  const values = { ...widget.values };
  delete bind[filter.name];
  delete values[filter.name];
  if (source.kind === 'FIXED') values[filter.name] = source.value;
  if (source.kind === 'BOUND') bind[filter.name] = source.filter;
  // The report's default over a dashboard filter of the same name: an empty binding says so.
  if (source.kind === 'DEFAULT' && dashboard.some((d) => d.name === filter.name && fits(d, filter)))
    bind[filter.name] = '';
  return { ...widget, bind, values };
}

export interface DashboardProblem {
  key: string;
  params?: Record<string, string | number>;
}

/** What stops the dashboard being saved, as translation keys. */
export function checkDashboard(
  def: DashboardDefinition,
  code: string | null,
  personal: boolean,
  views: Record<string, ReportView | null>,
): DashboardProblem[] {
  const problems: DashboardProblem[] = [];
  if (!personal && !/^[A-Z][A-Z0-9_-]{0,99}$/.test(code ?? ''))
    problems.push({ key: 'db.problem.code' });
  if (!def.nameEn.trim() || (!personal && !def.nameAr.trim()))
    problems.push({ key: 'db.problem.name' });
  def.filters.forEach((f, i) => {
    if (
      !/^[A-Za-z][A-Za-z0-9_]{0,63}$/.test(f.name) ||
      !f.labelAr.trim() ||
      !f.labelEn.trim() ||
      (f.type === 'LIST' && !f.lov)
    )
      problems.push({ key: 'db.problem.filter', params: { n: i + 1 } });
  });
  if (!def.widgets.length) problems.push({ key: 'db.problem.widgets' });
  def.widgets.forEach((w, i) => {
    const view = views[w.report];
    const itemMissing =
      w.kind === 'TILE'
        ? !view?.tiles.some((t) => t.name === w.item)
        : w.kind === 'CHART'
          ? !view?.charts.some((c) => c.name === w.item)
          : false;
    if (!w.report || (view !== undefined && itemMissing))
      problems.push({ key: 'db.problem.widget', params: { n: i + 1 } });
  });
  return problems;
}

/** The payload saved: trimmed texts, empty fixed values left out, no item for a table. */
export function dashboardPayload(def: DashboardDefinition): DashboardDefinition {
  const text = (v: string | null | undefined) => v?.trim() || null;
  return {
    nameAr: def.nameAr.trim() || def.nameEn.trim(),
    nameEn: def.nameEn.trim(),
    descriptionAr: text(def.descriptionAr),
    descriptionEn: text(def.descriptionEn),
    icon: text(def.icon),
    filters: def.filters.map((f) => ({
      ...f,
      name: f.name.trim(),
      labelAr: f.labelAr.trim(),
      labelEn: f.labelEn.trim(),
      lov: f.type === 'LIST' ? f.lov : null,
      multiple: f.type === 'LIST' && f.multiple,
      default: text(f.default),
    })),
    widgets: def.widgets.map((w) => ({
      ...w,
      item: w.kind === 'TABLE' ? null : w.item,
      titleAr: text(w.titleAr),
      titleEn: text(w.titleEn),
      values: Object.fromEntries(Object.entries(w.values).filter(([, v]) => !!v?.trim())),
    })),
  };
}

import { ReportChartData, ReportFilterDef, ReportView } from '@dynamic-cube/shared';
import { layoutChart } from './report-chart-layout';
import {
  checkDashboard,
  dashboardPayload,
  emptyWidget,
  filterSource,
  normalizeDashboard,
  widgetName,
  withFilterSource,
} from './dashboard-model';

const filter = (
  name: string,
  type: ReportFilterDef['type'] = 'DATE',
  lov: string | null = null,
): ReportFilterDef => ({
  name,
  labelAr: name,
  labelEn: name,
  type,
  lov,
  multiple: false,
  required: false,
  default: null,
});

const view = (code: string): ReportView => ({
  code,
  nameAr: code,
  nameEn: code,
  descriptionAr: null,
  descriptionEn: null,
  filters: [filter('from'), filter('branch', 'LIST', 'BRANCHES')],
  columns: [],
  tiles: [{ name: 'count', labelAr: 'العدد', labelEn: 'Count', column: null, aggregate: 'COUNT' }],
  charts: [],
  pageSize: 50,
  canExport: true,
  savedFilters: [],
  canDrill: true,
});

describe('dashboard model', () => {
  it('fills a report filter from a fixed value, a bound filter, the same name, or the report default', () => {
    const dashboard = [filter('from'), filter('since')];
    const w = emptyWidget('w', 'R', 'TILE');
    expect(filterSource(w, filter('from'), dashboard)).toEqual({ kind: 'BOUND', filter: 'from' });
    expect(filterSource(w, filter('branch', 'LIST', 'BRANCHES'), dashboard)).toEqual({
      kind: 'DEFAULT',
    });

    const bound = withFilterSource(
      w,
      filter('from'),
      { kind: 'BOUND', filter: 'since' },
      dashboard,
    );
    expect(bound.bind).toEqual({ from: 'since' });
    expect(filterSource(bound, filter('from'), dashboard)).toEqual({
      kind: 'BOUND',
      filter: 'since',
    });

    const fixed = withFilterSource(
      bound,
      filter('from'),
      { kind: 'FIXED', value: 'TODAY-7' },
      dashboard,
    );
    expect(fixed.bind).toEqual({});
    expect(fixed.values).toEqual({ from: 'TODAY-7' });

    // The report's default over a dashboard filter of the same name: an empty binding.
    const own = withFilterSource(fixed, filter('from'), { kind: 'DEFAULT' }, dashboard);
    expect(own.bind).toEqual({ from: '' });
    expect(own.values).toEqual({});
    expect(filterSource(own, filter('from'), dashboard)).toEqual({ kind: 'DEFAULT' });
  });

  it('does not bind filters of another kind or list', () => {
    const dashboard = [filter('branch', 'LIST', 'OTHER')];
    expect(filterSource(emptyWidget('w'), filter('branch', 'LIST', 'BRANCHES'), dashboard)).toEqual(
      { kind: 'DEFAULT' },
    );
  });

  it('checks the code, name, filters and widgets', () => {
    const def = normalizeDashboard({
      nameEn: 'Board',
      nameAr: '',
      widgets: [emptyWidget('a', 'R', 'TILE')],
    });
    def.widgets[0].item = 'nope';
    def.filters = [filter('bad name')];
    const keys = checkDashboard(def, 'lower', false, { R: view('R') }).map((p) => p.key);
    expect(keys).toEqual([
      'db.problem.code',
      'db.problem.name',
      'db.problem.filter',
      'db.problem.widget',
    ]);

    def.widgets[0].item = 'count';
    def.filters = [];
    expect(checkDashboard(def, null, true, { R: view('R') })).toEqual([]);
    expect(checkDashboard({ ...def, widgets: [] }, null, true, {}).map((p) => p.key)).toEqual([
      'db.problem.widgets',
    ]);
  });

  it('saves trimmed texts, drops empty fixed values and a table item, and names widgets uniquely', () => {
    const def = normalizeDashboard({
      nameEn: ' Board ',
      widgets: [
        { ...emptyWidget('t', 'R', 'TABLE'), item: 'x', values: { from: ' ', to: 'TODAY' } },
      ],
    });
    const payload = dashboardPayload(def);
    expect(payload.nameEn).toBe('Board');
    expect(payload.nameAr).toBe('Board');
    expect(payload.widgets[0].item).toBeNull();
    expect(payload.widgets[0].values).toEqual({ to: 'TODAY' });
    expect(widgetName('by month!', ['bymonth'])).toBe('bymonth2');
    expect(widgetName('1st', [])).toBe('w1st');
  });
});

describe('chart drill keys', () => {
  const base: ReportChartData = {
    name: 'c',
    titleAr: 'c',
    titleEn: 'c',
    type: 'BAR',
    stacked: false,
    width: 'HALF',
    category: 'X',
    x: [
      {
        name: 'x0',
        labelAr: 'الشهر',
        labelEn: 'Month',
        type: 'DATE',
        aggregate: null,
        bucket: 'MONTH',
      },
    ],
    y: [
      {
        name: 'y0',
        labelAr: 'العدد',
        labelEn: 'Count',
        type: 'NUMBER',
        aggregate: 'COUNT',
        bucket: null,
      },
    ],
    series: {
      name: 's',
      labelAr: 'الحالة',
      labelEn: 'Status',
      type: 'TEXT',
      aggregate: null,
      bucket: null,
    },
    rows: [
      { x0: '2026-01-01', y0: 3, s: 'APPROVED' },
      { x0: '2026-02-01', y0: 5, s: 'REJECTED' },
    ],
    hasMore: false,
  };

  it('carries the raw group and series values on each bar, point and slice', () => {
    const bars = layoutChart(base, 'en');
    expect(bars.rects.map((r) => r.key)).toEqual([
      { x0: '2026-01-01', s: 'APPROVED' },
      { x0: '2026-02-01', s: 'REJECTED' },
    ]);
    const line = layoutChart({ ...base, type: 'LINE', series: null }, 'en');
    expect(line.dots.map((d) => d.key)).toEqual([{ x0: '2026-01-01' }, { x0: '2026-02-01' }]);
    const pie = layoutChart({ ...base, type: 'PIE', series: null }, 'en');
    expect(pie.slices.map((s) => s.key)).toEqual([{ x0: '2026-01-01' }, { x0: '2026-02-01' }]);
  });

  it('gives no key to the rows a chart draws as they are', () => {
    const rows = layoutChart({ ...base, category: 'ROWS', series: null, type: 'SCATTER' }, 'en');
    expect(rows.dots.every((d) => d.key === undefined)).toBe(true);
  });
});

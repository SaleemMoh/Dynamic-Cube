import { ReportChartData, ReportChartDef } from '@dynamic-cube/shared';
import { layoutChart, niceDomain, fieldText } from './report-chart-layout';
import {
  ChartSourceField,
  ROW_COUNT,
  chartMode,
  checkCharts,
  dropField,
  emptyChart,
  removeField,
  setField,
  swapAxes,
} from './report-chart-model';

const DATE: ChartSourceField = {
  ref: 'r.submitted_at',
  category: 'DATE',
  labelAr: 'التقديم',
  labelEn: 'Submitted',
};
const FEE: ChartSourceField = {
  ref: 'r.fee',
  category: 'NUMBER',
  labelAr: 'الرسوم',
  labelEn: 'Fee',
};
const STATUS: ChartSourceField = {
  ref: 'r.status',
  category: 'TEXT',
  labelAr: 'الحالة',
  labelEn: 'Status',
};
const ROWS: ChartSourceField = {
  ref: ROW_COUNT,
  category: 'NUMBER',
  labelAr: 'العدد',
  labelEn: 'Count',
};

function titled(chart: ReportChartDef): ReportChartDef {
  return { ...chart, titleAr: 'ع', titleEn: 'E' };
}

describe('chart designer model', () => {
  it('drops a date on X and a number on Y: grouped by month along X, the number summed', () => {
    let c = dropField(emptyChart([]), 'x', DATE);
    expect(c.x).toEqual([
      {
        column: 'r.submitted_at',
        aggregate: null,
        bucket: 'MONTH',
        labelAr: 'التقديم',
        labelEn: 'Submitted',
      },
    ]);
    c = dropField(c, 'y', FEE);
    expect(c.y[0].aggregate).toBe('SUM');
    expect(chartMode(c)).toBe('X');
    c = dropField(c, 'y', ROWS);
    expect(c.y.map((f) => [f.column, f.aggregate])).toEqual([
      ['r.fee', 'SUM'],
      [null, 'COUNT'],
    ]);
  });

  it('puts time down the Y axis with the average across, or swaps the axes', () => {
    let c = dropField(emptyChart([]), 'y', DATE);
    c = dropField(c, 'x', FEE);
    c = setField(c, 'x', 0, { aggregate: 'AVG' }, 'NUMBER');
    expect(chartMode(c)).toBe('Y');
    expect(c.x[0].aggregate).toBe('AVG');
    const swapped = swapAxes({ ...c, sort: 'Y_DESC' });
    expect(chartMode(swapped)).toBe('X');
    expect(swapped.sort).toBe('X_DESC');
  });

  it('draws the rows themselves with a field as it is on each axis', () => {
    let c = dropField(emptyChart([]), 'x', FEE);
    expect(c.x[0].aggregate).toBeNull();
    c = dropField(c, 'y', DATE);
    expect(c.y[0].aggregate).toBe('COUNT');
    c = setField(c, 'y', 0, { aggregate: null }, 'DATE');
    expect(chartMode(c)).toBe('ROWS');
  });

  it('keeps one field on the axis grouped by, moves it aside for a count, and colours by a column', () => {
    let c = dropField(emptyChart([]), 'x', STATUS);
    c = dropField(c, 'x', DATE);
    expect(c.x.map((f) => f.column)).toEqual(['r.submitted_at']);
    c = dropField(c, 'x', ROWS);
    expect(c.y.map((f) => f.column)).toEqual(['r.submitted_at']);
    expect(c.x[0].aggregate).toBe('COUNT');
    c = dropField(c, 'series', STATUS);
    expect(c.series?.column).toBe('r.status');
    expect(removeField(c, 'series').series).toBeNull();
    expect(removeField(c, 'x', 0).x).toEqual([]);
  });

  it('a totalled field loses its period and a field with no column can only count', () => {
    let c = dropField(emptyChart([]), 'x', DATE);
    c = setField(c, 'x', 0, { aggregate: 'MAX' }, 'DATE');
    expect(c.x[0].bucket).toBeNull();
    c = dropField(emptyChart([]), 'y', ROWS);
    c = setField(c, 'y', 0, { aggregate: 'SUM' });
    expect(c.y[0].aggregate).toBe('COUNT');
  });

  it('checks titles, axes, the pie and series', () => {
    const grouped = titled(dropField(dropField(emptyChart([]), 'x', STATUS), 'y', ROWS));
    expect(checkCharts([grouped])).toEqual([]);
    const both = { ...grouped, x: [{ ...grouped.y[0] }] };
    const pie = { ...grouped, type: 'PIE' as const, y: [...grouped.y, { ...grouped.y[0] }] };
    const series = {
      ...grouped,
      series: { ...grouped.x[0] },
      y: [...grouped.y, { ...grouped.y[0] }],
    };
    const keys = checkCharts([
      { ...grouped, name: 'a', titleAr: '' },
      { ...grouped, name: 'b', x: [] },
      { ...both, name: 'c' },
      { ...pie, name: 'd' },
      { ...series, name: 'e' },
      { ...grouped, name: 'x y' },
    ]).map((p) => `${p.chart}:${p.key}`);
    expect(keys).toEqual([
      '0:rp.problem.chartTitles',
      '1:rp.problem.chartAxes',
      '2:rp.problem.chartMode',
      '3:rp.problem.chartPie',
      '4:rp.problem.chartSeries',
      '5:rp.problem.chartName',
    ]);
    expect(emptyChart([grouped]).name).toBe('chart2');
  });
});

function data(change: Partial<ReportChartData>): ReportChartData {
  return {
    name: 'c',
    titleAr: 'ع',
    titleEn: 'E',
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
    series: null,
    rows: [
      { x0: '2026-01-01', y0: 2 },
      { x0: '2026-02-01', y0: 4 },
    ],
    hasMore: false,
    ...change,
  };
}

describe('chart layout', () => {
  it('formats periods and rounds the value axis', () => {
    const month = data({}).x[0];
    expect(fieldText('2026-03-01', month, 'en')).toBe('2026-03');
    expect(fieldText('2026-05-01', { ...month, bucket: 'QUARTER' }, 'en')).toBe('2026 Q2');
    expect(fieldText('2026-05-01', { ...month, bucket: 'YEAR' }, 'en')).toBe('2026');
    expect(niceDomain(0, 87)).toEqual({ min: 0, max: 100, ticks: [0, 25, 50, 75, 100] });
    expect(niceDomain(-10, 30).min).toBe(-10);
  });

  it('draws bars up when grouped along X and across when grouped along Y', () => {
    const up = layoutChart(data({}), 'en');
    expect(up.rects.length).toBe(2);
    expect(up.xTicks.map((t) => t.label)).toEqual(['2026-01', '2026-02']);
    expect(up.rects[1].h).toBeGreaterThan(up.rects[0].h);
    expect(up.yGrid).toBe(true);

    const across = layoutChart(
      data({
        category: 'Y',
        y: [{ ...data({}).x[0], name: 'y0' }],
        x: [
          {
            name: 'x0',
            labelAr: 'المتوسط',
            labelEn: 'Average',
            type: 'NUMBER',
            aggregate: 'AVG',
            bucket: null,
          },
        ],
        rows: [
          { y0: '2026-01-01', x0: 10 },
          { y0: '2026-02-01', x0: 30 },
        ],
      }),
      'en',
    );
    expect(across.yTicks.map((t) => t.label)).toEqual(['2026-01', '2026-02']);
    expect(across.rects[1].w).toBeGreaterThan(across.rects[0].w);
    expect(across.xGrid).toBe(true);
    expect(across.xTitle).toBe('Average');
  });

  it('lines down the Y axis, series with a legend, stacked bars and a pie', () => {
    const line = layoutChart(
      data({
        type: 'LINE',
        category: 'Y',
        y: [{ ...data({}).x[0], name: 'y0' }],
        x: [{ ...data({}).y[0], name: 'x0' }],
        rows: [
          { y0: '2026-01-01', x0: 1 },
          { y0: '2026-02-01', x0: 3 },
        ],
      }),
      'en',
    );
    expect(line.lines.length).toBe(1);
    expect(line.dots[1].cy).toBeGreaterThan(line.dots[0].cy);
    expect(line.dots[1].cx).toBeGreaterThan(line.dots[0].cx);

    const stacked = layoutChart(
      data({
        stacked: true,
        series: {
          name: 's',
          labelAr: 'الحالة',
          labelEn: 'Status',
          type: 'TEXT',
          aggregate: null,
          bucket: null,
        },
        rows: [
          { x0: '2026-01-01', s: 'A', y0: 1 },
          { x0: '2026-01-01', s: 'B', y0: 2 },
        ],
      }),
      'en',
    );
    expect(stacked.legend.map((l) => l.label)).toEqual(['A', 'B']);
    expect(stacked.rects[1].y + stacked.rects[1].h).toBeCloseTo(stacked.rects[0].y);

    const pie = layoutChart(data({ type: 'PIE' }), 'en');
    expect(pie.slices.length).toBe(2);
    expect(pie.legend.map((l) => l.label)).toEqual(['2026-01', '2026-02']);
  });

  it('draws the rows themselves on numbers, dates or categories', () => {
    const points = layoutChart(
      data({
        type: 'SCATTER',
        category: 'ROWS',
        x: [
          {
            name: 'x0',
            labelAr: 'التاريخ',
            labelEn: 'Date',
            type: 'DATE',
            aggregate: null,
            bucket: null,
          },
        ],
        y: [
          {
            name: 'y0',
            labelAr: 'الرسوم',
            labelEn: 'Fee',
            type: 'NUMBER',
            aggregate: null,
            bucket: null,
          },
        ],
        rows: [
          { x0: '2026-01-01T00:00:00Z', y0: 100 },
          { x0: '2026-03-01T00:00:00Z', y0: 50 },
        ],
      }),
      'en',
    );
    expect(points.dots.length).toBe(2);
    expect(points.dots[1].cx).toBeGreaterThan(points.dots[0].cx);
    expect(points.dots[1].cy).toBeGreaterThan(points.dots[0].cy);
    expect(points.xTicks[0].label).toBe('2026-01-01');

    const empty = layoutChart(data({ rows: [] }), 'en');
    expect(empty.empty).toBe(true);
  });
});

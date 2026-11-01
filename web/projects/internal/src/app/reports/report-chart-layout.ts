import { ReportChartData, ReportChartFieldData } from '@dynamic-cube/shared';
import { formatValue, pieSlices, toNumber } from './report-model';

/** Categorical colours in fixed order (never cycled): past eight, series fold into "Other". */
export const CHART_COLORS = [
  '#2a78d6',
  '#eb6834',
  '#1baf7a',
  '#eda100',
  '#e87ba4',
  '#008300',
  '#6250d6',
  '#e34948',
];
export const OTHER_COLOR = '#9a9a94';

export interface ChartTick {
  pos: number;
  label: string;
}

export interface ChartRect {
  x: number;
  y: number;
  w: number;
  h: number;
  color: string;
  title: string;
}

export interface ChartLine {
  points: string;
  area: string | null;
  color: string;
}

export interface ChartDot {
  cx: number;
  cy: number;
  color: string;
  title: string;
}

export interface ChartSlice {
  path: string;
  color: string;
  title: string;
}

/** What a chart draws: its plot area, axes with ticks and titles, the marks and the legend. */
export interface ChartLayout {
  width: number;
  height: number;
  plot: { left: number; top: number; right: number; bottom: number };
  xTicks: ChartTick[];
  yTicks: ChartTick[];
  xGrid: boolean;
  yGrid: boolean;
  xTitle: string;
  yTitle: string;
  rects: ChartRect[];
  lines: ChartLine[];
  dots: ChartDot[];
  slices: ChartSlice[];
  legend: { label: string; color: string }[];
  empty: boolean;
}

type Lang = 'ar' | 'en';

/** A value of a field as text: a date by its period (2026-03, 2026 Q1, 2026), numbers grouped, others as they are. */
export function fieldText(value: unknown, field: ReportChartFieldData, lang: Lang): string {
  if (value === null || value === undefined || value === '') return '—';
  if (field.type === 'DATE' && typeof value === 'string') {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    if (m) {
      switch (field.bucket) {
        case 'YEAR':
          return m[1];
        case 'QUARTER':
          return `${m[1]} Q${Math.floor((Number(m[2]) - 1) / 3) + 1}`;
        case 'MONTH':
          return `${m[1]}-${m[2]}`;
      }
    }
  }
  return formatValue(value, field.type, lang);
}

/** A round domain for values (zero included) and its ticks. */
export function niceDomain(
  min: number,
  max: number,
  count = 4,
): { min: number; max: number; ticks: number[] } {
  let lo = Math.min(0, min);
  let hi = Math.max(0, max);
  if (!(hi > lo)) hi = lo + 1;
  const raw = (hi - lo) / count;
  const power = Math.pow(10, Math.floor(Math.log10(raw)));
  const step = ([1, 2, 2.5, 5, 10].find((m) => m * power >= raw) ?? 10) * power;
  lo = Math.floor(lo / step) * step;
  hi = Math.ceil(hi / step) * step;
  const ticks: number[] = [];
  for (let v = lo; v <= hi + step / 2; v += step) ticks.push(Math.round(v * 1e6) / 1e6);
  return { min: lo, max: hi, ticks };
}

/** A domain for a continuous axis that need not start at zero (dates, raw numbers). */
function spanDomain(values: number[], count = 4): { min: number; max: number; ticks: number[] } {
  let lo = Math.min(...values);
  let hi = Math.max(...values);
  if (!(hi > lo)) {
    lo -= 1;
    hi += 1;
  }
  const raw = (hi - lo) / count;
  const ticks: number[] = [];
  for (let i = 0; i <= count; i++) ticks.push(lo + raw * i);
  return { min: lo, max: hi, ticks };
}

interface Scale {
  kind: 'band' | 'linear' | 'time';
  /** Band: the category labels in order. */
  bands: string[];
  min: number;
  max: number;
  ticks: number[];
}

/**
 * The shapes of a chart (an SVG of W×H): grouped by the X axis (bars up, lines across), by the Y axis
 * (bars across, lines down — time down the Y axis with values across), the rows themselves (points, lines or bars per
 * row on numbers, dates or categories on either axis), or a pie; series in fixed colours, stacked when asked.
 */
export function layoutChart(data: ReportChartData, lang: Lang, W = 640, H = 300): ChartLayout {
  const label = (f: { labelAr: string; labelEn: string }) =>
    lang === 'ar' ? f.labelAr : f.labelEn;
  const layout: ChartLayout = {
    width: W,
    height: H,
    plot: { left: 64, top: 12, right: W - 16, bottom: H - 44 },
    xTicks: [],
    yTicks: [],
    xGrid: false,
    yGrid: false,
    xTitle: data.x.map(label).join(' · '),
    yTitle: data.y.map(label).join(' · '),
    rects: [],
    lines: [],
    dots: [],
    slices: [],
    legend: [],
    empty: data.rows.length === 0,
  };
  if (layout.empty) return layout;
  if (data.type === 'PIE') return pie(data, layout, lang, label);
  return data.category === 'ROWS'
    ? rowsLayout(data, layout, lang)
    : grouped(data, layout, lang, label);
}

function pie(
  data: ReportChartData,
  layout: ChartLayout,
  lang: Lang,
  label: (f: ReportChartFieldData) => string,
): ChartLayout {
  const byX = data.category !== 'Y';
  const cat = byX ? data.x[0] : data.y[0];
  const val = byX ? data.y[0] : data.x[0];
  let parts = data.rows.map((r, i) => ({
    label: fieldText(r[cat.name], cat, lang),
    value: toNumber(r[val.name]),
    color: CHART_COLORS[i] ?? OTHER_COLOR,
  }));
  if (parts.length > CHART_COLORS.length) {
    const rest = parts.slice(CHART_COLORS.length - 1).reduce((a, p) => a + p.value, 0);
    parts = [
      ...parts.slice(0, CHART_COLORS.length - 1),
      { label: lang === 'ar' ? 'أخرى' : 'Other', value: rest, color: OTHER_COLOR },
    ];
  }
  const slices = pieSlices(
    parts.map((p) => p.value),
    100,
  );
  layout.slices = parts.map((p, i) => ({
    path: slices[i].path,
    color: p.color,
    title: `${p.label} · ${label(val)}: ${formatValue(p.value, 'NUMBER', lang)} (${Math.round(slices[i].share * 100)}%)`,
  }));
  layout.legend = parts.map((p) => ({ label: p.label, color: p.color }));
  return layout;
}

/** Series of a grouped chart: each value field, or each value of the series column (the first value field). */
function seriesOf(
  data: ReportChartData,
  values: ReportChartFieldData[],
  categories: string[],
  catKey: string,
  catField: ReportChartFieldData,
  lang: Lang,
  label: (f: ReportChartFieldData) => string,
) {
  const index = new Map(categories.map((c, i) => [c, i]));
  if (data.series) {
    const field = data.series;
    const names: string[] = [];
    for (const r of data.rows) {
      const n = fieldText(r['s'], field, lang);
      if (!names.includes(n)) names.push(n);
    }
    return names.map((name, si) => {
      const vals: (number | null)[] = categories.map(() => null);
      for (const r of data.rows) {
        if (fieldText(r['s'], field, lang) !== name) continue;
        vals[index.get(fieldText(r[catKey], catField, lang))!] = toNumber(r[values[0].name]);
      }
      return {
        label: name,
        color: CHART_COLORS[si] ?? OTHER_COLOR,
        values: vals,
        valueLabel: label(values[0]),
      };
    });
  }
  return values.map((v, vi) => {
    const vals: (number | null)[] = categories.map(() => null);
    for (const r of data.rows)
      vals[index.get(fieldText(r[catKey], catField, lang))!] = toNumber(r[v.name]);
    return {
      label: label(v),
      color: CHART_COLORS[vi] ?? OTHER_COLOR,
      values: vals,
      valueLabel: label(v),
    };
  });
}

function grouped(
  data: ReportChartData,
  layout: ChartLayout,
  lang: Lang,
  label: (f: ReportChartFieldData) => string,
): ChartLayout {
  const horizontal = data.category === 'Y';
  const catField = horizontal ? data.y[0] : data.x[0];
  const values = horizontal ? data.x : data.y;
  const catKey = catField.name;
  const categories: string[] = [];
  for (const r of data.rows) {
    const c = fieldText(r[catKey], catField, lang);
    if (!categories.includes(c)) categories.push(c);
  }
  const series = seriesOf(data, values, categories, catKey, catField, lang, label).slice(
    0,
    CHART_COLORS.length,
  );
  const stacked = data.stacked && (data.type === 'BAR' || data.type === 'AREA');
  let lo = 0;
  let hi = 0;
  categories.forEach((_, ci) => {
    let pos = 0;
    let neg = 0;
    for (const s of series) {
      const v = s.values[ci] ?? 0;
      if (stacked) {
        if (v >= 0) pos += v;
        else neg += v;
      } else {
        hi = Math.max(hi, v);
        lo = Math.min(lo, v);
      }
    }
    if (stacked) {
      hi = Math.max(hi, pos);
      lo = Math.min(lo, neg);
    }
  });
  const domain = niceDomain(lo, hi);
  const { left, top, right, bottom } = layout.plot;
  const catLength = horizontal ? bottom - top : right - left;
  const band = catLength / Math.max(1, categories.length);
  // Value position along its axis.
  const valuePos = (v: number) =>
    horizontal
      ? left + ((v - domain.min) / (domain.max - domain.min)) * (right - left)
      : bottom - ((v - domain.min) / (domain.max - domain.min)) * (bottom - top);
  const catPos = (i: number) => (horizontal ? top + band * (i + 0.5) : left + band * (i + 0.5));
  const valueTicks = domain.ticks.map((t) => ({
    pos: valuePos(t),
    label: formatValue(t, 'NUMBER', lang),
  }));
  const every = Math.max(1, Math.ceil(categories.length / (horizontal ? 14 : 10)));
  const catTicks = categories
    .map((c, i) => ({ pos: catPos(i), label: short(c), i }))
    .filter((t) => t.i % every === 0)
    .map(({ pos, label: l }) => ({ pos, label: l }));
  if (horizontal) {
    layout.xTicks = valueTicks;
    layout.yTicks = catTicks;
    layout.xGrid = true;
  } else {
    layout.yTicks = valueTicks;
    layout.xTicks = catTicks;
    layout.yGrid = true;
  }
  const title = (ci: number, s: (typeof series)[number], v: number) =>
    `${categories[ci]} · ${data.series ? s.label + ' · ' + s.valueLabel : s.label}: ${formatValue(v, 'NUMBER', lang)}`;

  if (data.type === 'BAR') {
    const inner = Math.min(band * 0.72, (stacked ? 1 : series.length) * 56);
    const each = stacked ? inner : Math.max(2, inner / Math.max(1, series.length) - 2);
    categories.forEach((_, ci) => {
      let pos = 0;
      let neg = 0;
      series.forEach((s, si) => {
        const v = s.values[ci];
        if (v === null) return;
        const start = stacked ? (v >= 0 ? pos : neg) : 0;
        const end = start + v;
        if (stacked) {
          if (v >= 0) pos = end;
          else neg = end;
        }
        const a = valuePos(Math.min(start, end));
        const b = valuePos(Math.max(start, end));
        const offset = catPos(ci) - inner / 2 + (stacked ? 0 : si * (each + 2));
        layout.rects.push(
          horizontal
            ? {
                x: a,
                y: offset,
                w: Math.max(0, b - a),
                h: each,
                color: s.color,
                title: title(ci, s, v),
              }
            : {
                x: offset,
                y: b,
                w: each,
                h: Math.max(0, a - b),
                color: s.color,
                title: title(ci, s, v),
              },
        );
      });
    });
  } else {
    const base = categories.map(() => 0);
    for (const s of series) {
      const pts: [number, number][] = [];
      const lower: [number, number][] = [];
      s.values.forEach((v, ci) => {
        if (v === null) return;
        const from = stacked ? base[ci] : 0;
        const to = from + v;
        if (stacked) base[ci] = to;
        const point: [number, number] = horizontal
          ? [valuePos(to), catPos(ci)]
          : [catPos(ci), valuePos(to)];
        const floor: [number, number] = horizontal
          ? [valuePos(from), catPos(ci)]
          : [catPos(ci), valuePos(from)];
        pts.push(point);
        lower.push(floor);
        layout.dots.push({ cx: point[0], cy: point[1], color: s.color, title: title(ci, s, v) });
      });
      if (data.type !== 'SCATTER' && pts.length) {
        const area =
          data.type === 'AREA'
            ? [...pts, ...lower.reverse()].map((p) => p.join(',')).join(' ')
            : null;
        layout.lines.push({ points: pts.map((p) => p.join(',')).join(' '), area, color: s.color });
      }
    }
  }
  if (series.length > 1 || data.series)
    layout.legend = series.map((s) => ({ label: s.label, color: s.color }));
  return layout;
}

function scaleFor(field: ReportChartFieldData, raw: unknown[], lang: Lang): Scale {
  if (field.type === 'NUMBER') {
    const values = raw.map(toNumber);
    const d = niceDomain(Math.min(...values), Math.max(...values));
    return { kind: 'linear', bands: [], ...d };
  }
  if (field.type === 'DATE') {
    const values = raw
      .map((v) => (typeof v === 'string' ? Date.parse(v) : NaN))
      .filter((v) => !Number.isNaN(v));
    if (values.length) return { kind: 'time', bands: [], ...spanDomain(values) };
  }
  const bands: string[] = [];
  for (const v of raw) {
    const t = fieldText(v, field, lang);
    if (!bands.includes(t)) bands.push(t);
  }
  return { kind: 'band', bands, min: 0, max: 0, ticks: [] };
}

function rowsLayout(data: ReportChartData, layout: ChartLayout, lang: Lang): ChartLayout {
  const xf = data.x[0];
  const yf = data.y[0];
  const xs = scaleFor(
    xf,
    data.rows.map((r) => r[xf.name]),
    lang,
  );
  const ys = scaleFor(
    yf,
    data.rows.map((r) => r[yf.name]),
    lang,
  );
  const { left, top, right, bottom } = layout.plot;
  const pos = (s: Scale, field: ReportChartFieldData, v: unknown, from: number, to: number) => {
    if (s.kind === 'band') {
      const band = (to - from) / Math.max(1, s.bands.length);
      return from + band * (s.bands.indexOf(fieldText(v, field, lang)) + 0.5);
    }
    const n = s.kind === 'time' ? Date.parse(String(v)) : toNumber(v);
    return from + ((n - s.min) / (s.max - s.min)) * (to - from);
  };
  const ticks = (s: Scale, field: ReportChartFieldData, from: number, to: number): ChartTick[] => {
    if (s.kind === 'band') {
      const every = Math.max(1, Math.ceil(s.bands.length / 10));
      const band = (to - from) / Math.max(1, s.bands.length);
      return s.bands
        .map((b, i) => ({ pos: from + band * (i + 0.5), label: short(b), i }))
        .filter((t) => t.i % every === 0);
    }
    return s.ticks.map((t) => ({
      pos: from + ((t - s.min) / (s.max - s.min)) * (to - from),
      label:
        s.kind === 'time' ? new Date(t).toISOString().slice(0, 10) : formatValue(t, 'NUMBER', lang),
    }));
  };
  layout.xTicks = ticks(xs, xf, left, right);
  layout.yTicks = ticks(ys, yf, bottom, top);
  layout.xGrid = xs.kind !== 'band';
  layout.yGrid = ys.kind !== 'band';

  // Series by the colour-by column (one colour each), else one.
  const names: string[] = [];
  const seriesName = (r: Record<string, unknown>) =>
    data.series ? fieldText(r['s'], data.series, lang) : '';
  for (const r of data.rows) if (!names.includes(seriesName(r))) names.push(seriesName(r));
  const color = (name: string) => CHART_COLORS[names.indexOf(name)] ?? OTHER_COLOR;
  const points = data.rows.map((r) => ({
    x: pos(xs, xf, r[xf.name], left, right),
    y: pos(ys, yf, r[yf.name], bottom, top),
    s: seriesName(r),
    title: `${data.series ? seriesName(r) + ' · ' : ''}${fieldText(r[xf.name], xf, lang)}, ${fieldText(r[yf.name], yf, lang)}`,
  }));
  const barsUp = data.type === 'BAR' && xs.kind === 'band' && ys.kind !== 'band';
  const barsAcross = data.type === 'BAR' && ys.kind === 'band' && xs.kind !== 'band';
  if (barsUp || barsAcross) {
    const count = barsUp ? xs.bands.length : ys.bands.length;
    const band = ((barsUp ? right - left : bottom - top) / Math.max(1, count)) * 0.7;
    const zero = barsUp ? pos(ys, yf, 0, bottom, top) : pos(xs, xf, 0, left, right);
    for (const p of points)
      layout.rects.push(
        barsUp
          ? {
              x: p.x - band / 2,
              y: Math.min(p.y, zero),
              w: band,
              h: Math.abs(zero - p.y),
              color: color(p.s),
              title: p.title,
            }
          : {
              x: Math.min(p.x, zero),
              y: p.y - band / 2,
              w: Math.abs(p.x - zero),
              h: band,
              color: color(p.s),
              title: p.title,
            },
      );
  } else {
    for (const p of points)
      layout.dots.push({ cx: p.x, cy: p.y, color: color(p.s), title: p.title });
    if (data.type === 'LINE' || data.type === 'AREA') {
      for (const name of names) {
        const pts = points
          .filter((p) => p.s === name)
          .sort((a, b) => (xs.kind === 'band' ? 0 : a.x - b.x));
        const line = pts.map((p) => `${p.x},${p.y}`).join(' ');
        const area =
          data.type === 'AREA' && pts.length
            ? `${line} ${pts[pts.length - 1].x},${bottom} ${pts[0].x},${bottom}`
            : null;
        layout.lines.push({ points: line, area, color: color(name) });
      }
    }
  }
  if (data.series) layout.legend = names.map((n) => ({ label: n, color: color(n) }));
  return layout;
}

function short(text: string): string {
  return text.length > 16 ? text.slice(0, 15) + '…' : text;
}

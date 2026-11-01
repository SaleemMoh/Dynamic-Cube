import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService, ReportChartData, ReportRunColumn } from '@dynamic-cube/shared';
import { formatValue, niceScale, pieSlices, toNumber } from './report-model';

/** Categorical colours in fixed order (never cycled): a ninth slice folds into "Other". */
const SERIES = [
  '#2a78d6',
  '#eb6834',
  '#1baf7a',
  '#eda100',
  '#e87ba4',
  '#008300',
  '#6250d6',
  '#e34948',
];
const OTHER = '#9a9a94';
const W = 640;
const H = 260;
const PAD = { top: 12, right: 12, bottom: 44, left: 56 };

interface Point {
  x: number;
  y: number;
  label: string;
}

/**
 * A report's chart drawn as SVG (no chart library): bars (series side by side), lines or a pie, over the chart rows the
 * server returns (up to 100). Each mark has a tooltip; a legend names the series when there are several; the table
 * below the chart holds every value.
 */
@Component({
  selector: 'dc-report-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (chart(); as c) {
      @if (series().length > 1 || c.type === 'PIE') {
        <ul class="rc-legend">
          @for (s of legend(); track s.label) {
            <li><span class="rc-key" [style.background]="s.color"></span>{{ s.label }}</li>
          }
        </ul>
      }
      @if (c.type === 'PIE') {
        <svg class="rc-pie" viewBox="0 0 160 160" role="img" [attr.aria-label]="title()">
          @for (s of pie(); track $index) {
            @if (s.path) {
              <path
                [attr.d]="s.path"
                [attr.fill]="s.color"
                stroke="var(--rc-surface)"
                stroke-width="2"
              >
                <title>{{ s.title }}</title>
              </path>
            }
          }
        </svg>
      } @else {
        <svg
          class="rc-plot"
          [attr.viewBox]="'0 0 ' + w + ' ' + h"
          role="img"
          [attr.aria-label]="title()"
        >
          @for (t of scale().ticks; track t) {
            <line
              class="rc-grid"
              [attr.x1]="pad.left"
              [attr.x2]="w - pad.right"
              [attr.y1]="y(t)"
              [attr.y2]="y(t)"
            />
            <text class="rc-tick" [attr.x]="pad.left - 6" [attr.y]="y(t) + 4" text-anchor="end">
              {{ fmt(t) }}
            </text>
          }
          @for (l of xLabels(); track $index) {
            <text
              class="rc-tick"
              [attr.x]="l.x"
              [attr.y]="h - pad.bottom + 16"
              text-anchor="middle"
            >
              {{ l.label }}
            </text>
          }
          @if (c.type === 'BAR') {
            @for (b of bars(); track $index) {
              <rect
                [attr.x]="b.x"
                [attr.y]="b.y"
                [attr.width]="b.width"
                [attr.height]="b.height"
                [attr.fill]="b.color"
                rx="3"
              >
                <title>{{ b.title }}</title>
              </rect>
            }
          } @else {
            @for (l of lines(); track l.color) {
              <polyline
                [attr.points]="l.points"
                fill="none"
                [attr.stroke]="l.color"
                stroke-width="2"
              />
              @for (p of l.dots; track $index) {
                <circle
                  [attr.cx]="p.x"
                  [attr.cy]="p.y"
                  r="4"
                  [attr.fill]="l.color"
                  stroke="var(--rc-surface)"
                  stroke-width="2"
                >
                  <title>{{ p.label }}</title>
                </circle>
              }
            }
          }
          <line
            class="rc-axis"
            [attr.x1]="pad.left"
            [attr.x2]="w - pad.right"
            [attr.y1]="y(0)"
            [attr.y2]="y(0)"
          />
        </svg>
      }
    }
  `,
  styles: `
    :host {
      --rc-surface: #fff;
      display: grid;
      gap: 8px;
      color: var(--dga-text-2, #384250);
      font-size: 12px;
    }
    .rc-plot {
      width: 100%;
      height: auto;
      max-height: 300px;
    }
    .rc-pie {
      width: 220px;
      justify-self: center;
    }
    .rc-grid {
      stroke: var(--dga-gray-200, #e5e7eb);
    }
    .rc-axis {
      stroke: var(--dga-gray-400, #9da4ae);
    }
    .rc-tick {
      fill: var(--dga-text-3, #6c737f);
      font-size: 11px;
    }
    .rc-legend {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 14px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .rc-legend li {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .rc-key {
      width: 10px;
      height: 10px;
      border-radius: 2px;
    }
  `,
})
export class ReportChart {
  private readonly i18n = inject(I18nService);
  readonly chart = input<ReportChartData | null>(null);
  readonly columns = input<ReportRunColumn[]>([]);

  protected readonly w = W;
  protected readonly h = H;
  protected readonly pad = PAD;

  private readonly lang = computed(() => (this.i18n.language() === 'ar' ? 'ar' : 'en'));

  private label(name: string): string {
    const c = this.columns().find((x) => x.name === name);
    return c ? (this.lang() === 'ar' ? c.labelAr : c.labelEn) : name;
  }

  private category(row: Record<string, unknown>): string {
    const c = this.chart()!;
    const type = this.columns().find((x) => x.name === c.category)?.type ?? 'TEXT';
    return formatValue(row[c.category], type, this.lang()) || '—';
  }

  protected readonly series = computed(() =>
    (this.chart()?.values ?? [])
      .slice(0, SERIES.length)
      .map((name, i) => ({ name, label: this.label(name), color: SERIES[i] })),
  );

  protected readonly title = computed(() =>
    this.series()
      .map((s) => s.label)
      .join(', '),
  );

  protected readonly rows = computed(() => this.chart()?.rows ?? []);

  protected readonly scale = computed(() => {
    let max = 0;
    for (const r of this.rows())
      for (const s of this.series()) max = Math.max(max, toNumber(r[s.name]));
    return niceScale(max);
  });

  protected y(value: number): number {
    const plot = H - PAD.top - PAD.bottom;
    return PAD.top + plot - (Math.max(0, value) / this.scale().max) * plot;
  }

  protected fmt(value: number): string {
    return formatValue(value, 'NUMBER', this.lang());
  }

  private readonly band = computed(
    () => (W - PAD.left - PAD.right) / Math.max(1, this.rows().length),
  );

  /** Category labels, thinned so they do not run into each other. */
  protected readonly xLabels = computed(() => {
    const every = Math.max(1, Math.ceil(this.rows().length / 10));
    return this.rows()
      .map((r, i) => ({
        x: PAD.left + this.band() * (i + 0.5),
        label: this.short(this.category(r)),
        i,
      }))
      .filter((l) => l.i % every === 0);
  });

  private short(text: string): string {
    return text.length > 14 ? text.slice(0, 13) + '…' : text;
  }

  protected readonly bars = computed(() => {
    const series = this.series();
    const band = this.band();
    const inner = Math.min(band * 0.72, 72 * Math.max(1, series.length));
    const width = Math.max(2, inner / Math.max(1, series.length) - 2);
    return this.rows().flatMap((r, i) =>
      series.map((s, j) => {
        const value = toNumber(r[s.name]);
        const top = this.y(value);
        return {
          x: PAD.left + band * i + (band - inner) / 2 + j * (width + 2),
          y: top,
          width,
          height: Math.max(0, this.y(0) - top),
          color: s.color,
          title: `${this.category(r)} · ${s.label}: ${this.fmt(value)}`,
        };
      }),
    );
  });

  protected readonly lines = computed(() =>
    this.series().map((s) => {
      const dots: Point[] = this.rows().map((r, i) => {
        const value = toNumber(r[s.name]);
        return {
          x: PAD.left + this.band() * (i + 0.5),
          y: this.y(value),
          label: `${this.category(r)} · ${s.label}: ${this.fmt(value)}`,
        };
      });
      return { color: s.color, dots, points: dots.map((p) => `${p.x},${p.y}`).join(' ') };
    }),
  );

  /** Pie slices of the first number; past seven categories the rest fold into "Other". */
  private readonly pieParts = computed(() => {
    const name = this.series()[0]?.name;
    if (!name) return [];
    const parts = this.rows().map((r, i) => ({
      label: this.category(r),
      value: toNumber(r[name]),
      color: SERIES[i],
    }));
    if (parts.length <= SERIES.length) return parts;
    const kept = parts.slice(0, SERIES.length - 1);
    const rest = parts.slice(SERIES.length - 1).reduce((a, p) => a + p.value, 0);
    return [...kept, { label: this.lang() === 'ar' ? 'أخرى' : 'Other', value: rest, color: OTHER }];
  });

  protected readonly pie = computed(() => {
    const parts = this.pieParts();
    const slices = pieSlices(parts.map((p) => p.value));
    return parts.map((p, i) => ({
      path: slices[i].path,
      color: p.color,
      title: `${p.label}: ${this.fmt(p.value)} (${Math.round(slices[i].share * 100)}%)`,
    }));
  });

  protected readonly legend = computed(() =>
    this.chart()?.type === 'PIE'
      ? this.pieParts().map((p) => ({ label: p.label, color: p.color }))
      : this.series(),
  );
}

import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { I18nService, ReportChartData, TranslatePipe } from '@dynamic-cube/shared';
import { layoutChart } from './report-chart-layout';

/**
 * A report's chart drawn as SVG (no chart library) from {@link layoutChart}: bars, lines, areas, points or a pie, with
 * whatever is on X across and whatever is on Y up. Each mark has a tooltip; a legend names the series when there are
 * several; the table under the charts holds every value.
 */
@Component({
  selector: 'dc-report-chart',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (layout(); as l) {
      @if (showTitle()) {
        <h3 class="rc-title">{{ title() }}</h3>
      }
      @if (l.legend.length) {
        <ul class="rc-legend">
          @for (s of l.legend; track $index) {
            <li><span class="rc-key" [style.background]="s.color"></span>{{ s.label }}</li>
          }
        </ul>
      }
      @if (l.empty) {
        <p class="rc-empty">{{ 'rp.noRows' | dcTranslate }}</p>
      } @else if (l.slices.length) {
        <svg class="rc-pie" viewBox="0 0 200 200" role="img" [attr.aria-label]="title()">
          @for (s of l.slices; track $index) {
            @if (s.path) {
              <path [attr.d]="s.path" [attr.fill]="s.color" stroke="#fff" stroke-width="2">
                <title>{{ s.title }}</title>
              </path>
            }
          }
        </svg>
      } @else {
        <svg
          class="rc-plot"
          [attr.viewBox]="'0 0 ' + l.width + ' ' + l.height"
          role="img"
          [attr.aria-label]="title()"
        >
          @for (t of l.yTicks; track $index) {
            @if (l.yGrid) {
              <line
                class="rc-grid"
                [attr.x1]="l.plot.left"
                [attr.x2]="l.plot.right"
                [attr.y1]="t.pos"
                [attr.y2]="t.pos"
              />
            }
            <text class="rc-tick" [attr.x]="l.plot.left - 6" [attr.y]="t.pos + 4" text-anchor="end">
              {{ t.label }}
            </text>
          }
          @for (t of l.xTicks; track $index) {
            @if (l.xGrid) {
              <line
                class="rc-grid"
                [attr.x1]="t.pos"
                [attr.x2]="t.pos"
                [attr.y1]="l.plot.top"
                [attr.y2]="l.plot.bottom"
              />
            }
            <text
              class="rc-tick"
              [attr.x]="t.pos"
              [attr.y]="l.plot.bottom + 16"
              text-anchor="middle"
            >
              {{ t.label }}
            </text>
          }
          @for (r of l.rects; track $index) {
            <rect
              [attr.x]="r.x"
              [attr.y]="r.y"
              [attr.width]="r.w"
              [attr.height]="r.h"
              [attr.fill]="r.color"
              rx="3"
            >
              <title>{{ r.title }}</title>
            </rect>
          }
          @for (line of l.lines; track $index) {
            @if (line.area) {
              <polygon [attr.points]="line.area" [attr.fill]="line.color" fill-opacity="0.18" />
            }
            <polyline
              [attr.points]="line.points"
              fill="none"
              [attr.stroke]="line.color"
              stroke-width="2"
            />
          }
          @for (d of l.dots; track $index) {
            <circle
              [attr.cx]="d.cx"
              [attr.cy]="d.cy"
              r="4"
              [attr.fill]="d.color"
              stroke="#fff"
              stroke-width="2"
            >
              <title>{{ d.title }}</title>
            </circle>
          }
          <line
            class="rc-axis"
            [attr.x1]="l.plot.left"
            [attr.x2]="l.plot.right"
            [attr.y1]="l.plot.bottom"
            [attr.y2]="l.plot.bottom"
          />
          <line
            class="rc-axis"
            [attr.x1]="l.plot.left"
            [attr.x2]="l.plot.left"
            [attr.y1]="l.plot.top"
            [attr.y2]="l.plot.bottom"
          />
          <text
            class="rc-axis-title"
            [attr.x]="(l.plot.left + l.plot.right) / 2"
            [attr.y]="l.height - 6"
            text-anchor="middle"
          >
            {{ l.xTitle }}
          </text>
          <text
            class="rc-axis-title"
            [attr.transform]="'translate(14 ' + (l.plot.top + l.plot.bottom) / 2 + ') rotate(-90)'"
            text-anchor="middle"
          >
            {{ l.yTitle }}
          </text>
        </svg>
      }
      @if (chart()?.hasMore) {
        <p class="rc-more">{{ 'rp.chart.more' | dcTranslate }}</p>
      }
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 8px;
      min-width: 0;
      color: var(--dga-text-2, #384250);
      font-size: 12px;
    }
    .rc-title {
      margin: 0;
      font-size: 14px;
    }
    .rc-plot {
      width: 100%;
      height: auto;
      direction: ltr;
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
    .rc-axis-title {
      fill: var(--dga-text-2, #384250);
      font-size: 11px;
      font-weight: 600;
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
    .rc-empty,
    .rc-more {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
    }
  `,
})
export class ReportChart {
  private readonly i18n = inject(I18nService);
  readonly chart = input<ReportChartData | null>(null);
  readonly showTitle = input(true);
  /** The drawing's width in SVG units (text keeps its size when the chart is wide); default by the chart's width. */
  readonly size = input<number | null>(null);

  private readonly lang = computed(() => (this.i18n.language() === 'ar' ? 'ar' : 'en'));
  protected readonly layout = computed(() => {
    const c = this.chart();
    if (!c) return null;
    const width = this.size() ?? (c.width === 'FULL' ? 1200 : 640);
    return layoutChart(c, this.lang(), width, width > 900 ? 340 : 300);
  });
  protected readonly title = computed(() => {
    const c = this.chart();
    return c ? (this.lang() === 'ar' ? c.titleAr : c.titleEn) : '';
  });
}

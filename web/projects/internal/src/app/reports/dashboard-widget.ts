import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import {
  DashboardWidgetView,
  I18nService,
  ReportRunColumn,
  ReportsApi,
  TranslatePipe,
  WidgetData,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, map, of } from 'rxjs';
import { ReportChart } from './report-chart';
import { formatValue } from './report-model';
import { DrillTarget } from './report-result';

/** A drill-down asked from a widget: the widget, the report filter values it ran with, and the clicked group. */
export interface WidgetDrill {
  widget: DashboardWidgetView;
  filters: Record<string, string | null>;
  target: DrillTarget;
}

/**
 * One widget of a dashboard (ADR-0047): a report's tile, chart or first rows, run with the dashboard's filter values
 * through the report (the user's access to it applies). It loads on its own, so a slow or broken report does not hold
 * the others; a click on a tile, a chart's mark or a grouped row opens the rows behind it.
 */
@Component({
  selector: 'dc-dashboard-widget',
  imports: [MatIconModule, MatProgressBarModule, ReportChart, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let w = widget();
    <header class="dw-head">
      <h3>{{ lang() === 'ar' ? w.titleAr : w.titleEn }}</h3>
      <a
        class="dw-open"
        [routerLink]="['/reports/view', w.report]"
        [attr.aria-label]="'db.openReport' | dcTranslate"
        [attr.title]="'db.openReport' | dcTranslate"
      >
        <mat-icon fontSet="material-symbols-outlined">open_in_new</mat-icon>
      </a>
    </header>
    @if (data.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @let d = data.value();
    @if (d?.error) {
      <p class="dw-error" role="alert">{{ d!.error }}</p>
    } @else if (d?.data; as r) {
      @switch (w.kind) {
        @case ('TILE') {
          <button
            type="button"
            class="dw-tile"
            [class.dw-drill]="w.canDrill"
            [disabled]="!w.canDrill"
            [attr.title]="w.canDrill ? ('rp.drill.hint' | dcTranslate) : null"
            (click)="open(r, { source: 'TILE', chart: null, values: null })"
          >
            {{ number(r.tile?.value) }}
          </button>
        }
        @case ('CHART') {
          @if (r.chart) {
            <dc-report-chart
              [chart]="r.chart"
              [showTitle]="false"
              [size]="w.span >= 8 ? 1200 : 640"
              [drillable]="w.canDrill && r.chart.category !== 'ROWS'"
              (drill)="open(r, { source: 'CHART', chart: r.chart.name, values: $event })"
            />
          }
        }
        @default {
          @if (r.table; as t) {
            <div class="dw-scroll">
              <table class="dc-table dw-table">
                <thead>
                  <tr>
                    @for (c of t.columns; track c.name) {
                      <th [class.dw-num]="c.type === 'NUMBER'">{{ label(c) }}</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (row of t.rows; track $index) {
                    <tr
                      [class.dw-drill]="w.canDrill && t.grouped"
                      [attr.title]="
                        w.canDrill && t.grouped ? ('rp.drill.hint' | dcTranslate) : null
                      "
                      (click)="openRow(r, row)"
                    >
                      @for (c of t.columns; track c.name) {
                        <td [class.dw-num]="c.type === 'NUMBER'">{{ cell(row[c.name], c) }}</td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <p class="dw-count">{{ rowsOf(t.rows.length, t.total) }}</p>
          }
        }
      }
    }
  `,
  styles: `
    :host {
      display: grid;
      align-content: start;
      gap: 8px;
      min-width: 0;
      padding: 12px 14px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
    }
    .dw-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .dw-head h3 {
      margin: 0;
      font-size: 14px;
      font-weight: 600;
    }
    .dw-open {
      display: inline-flex;
      color: var(--dga-text-3, #6c737f);
    }
    .dw-open mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .dw-tile {
      justify-self: start;
      padding: 0;
      border: 0;
      background: none;
      color: inherit;
      font: inherit;
      font-size: 30px;
      font-weight: 700;
    }
    .dw-tile:disabled {
      color: inherit;
    }
    .dw-drill {
      cursor: pointer;
    }
    tr.dw-drill:hover td {
      background: var(--dga-gray-50, #f9fafb);
    }
    .dw-scroll {
      overflow-x: auto;
    }
    .dw-table {
      width: 100%;
      font-size: 13px;
    }
    .dw-num {
      text-align: end;
      white-space: nowrap;
    }
    .dw-count {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dw-error {
      margin: 0;
      color: var(--dga-error-600, #d92d20);
      font-size: 13px;
    }
  `,
})
export class DashboardWidget {
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(I18nService);
  readonly widget = input.required<DashboardWidgetView>();
  readonly dashboard = input.required<{ code: string | null; id: string | null }>();
  /** The dashboard's filter values applied; a new object runs the widget again. */
  readonly filters = input<Record<string, string | null>>({});
  readonly drill = output<WidgetDrill>();

  protected readonly lang = computed(() => (this.i18n.language() === 'ar' ? 'ar' : 'en'));

  protected readonly data = rxResource({
    params: () => ({
      dashboard: this.dashboard(),
      widget: this.widget().name,
      filters: this.filters(),
    }),
    stream: ({ params }) =>
      this.api.runWidget(params.dashboard, params.widget, params.filters).pipe(
        map((data) => ({ data, error: null as string | null })),
        catchError((error) => of({ data: null, error: problemMessages(error).join(' ') })),
      ),
  });

  protected label(c: ReportRunColumn): string {
    return this.lang() === 'ar' ? c.labelAr : c.labelEn;
  }

  protected cell(value: unknown, c: ReportRunColumn): string {
    return formatValue(value, c.type, this.lang());
  }

  protected number(value: unknown): string {
    if (value === null || value === undefined) return '—';
    return formatValue(value, Number.isNaN(Number(value)) ? 'TEXT' : 'NUMBER', this.lang());
  }

  protected rowsOf(shown: number, total: number): string {
    return this.i18n
      .translate('db.rowsOf')
      .replace('{shown}', formatValue(shown, 'NUMBER', this.lang()))
      .replace('{total}', formatValue(total, 'NUMBER', this.lang()));
  }

  protected open(data: WidgetData, target: DrillTarget): void {
    const w = this.widget();
    if (!w.canDrill) return;
    this.drill.emit({
      widget: w,
      filters: data.filters,
      target: { ...target, titleAr: w.titleAr, titleEn: w.titleEn },
    });
  }

  protected openRow(data: WidgetData, row: Record<string, unknown>): void {
    if (!this.widget().canDrill || !data.table?.grouped) return;
    const values = Object.fromEntries(
      data.table.columns.filter((c) => !c.aggregate).map((c) => [c.name, row[c.name] ?? null]),
    );
    this.open(data, { source: 'TABLE', chart: null, values });
  }
}

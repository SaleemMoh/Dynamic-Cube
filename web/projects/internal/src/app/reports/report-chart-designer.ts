import { CdkDrag, CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  model,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  I18nService,
  REPORT_BUCKETS,
  REPORT_CHART_SORTS,
  REPORT_CHART_TYPES,
  ReportChartData,
  ReportChartDef,
  ReportChartField,
  ReportDefinition,
  ReportsApi,
  TranslatePipe,
  problemMessages,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { catchError, debounceTime, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { QuerySourceInfo } from '../apps-control/lovs/lov-builder-model';
import { EndpointQuery, endpointColumns } from '../integration/api-endpoint-model';
import {
  ChartSourceField,
  ChartZone,
  MAX_CHARTS,
  ROW_COUNT,
  chartMode,
  checkCharts,
  copyChart,
  dropField,
  emptyChart,
  fieldAggregates,
  removeField,
  setField,
  swapAxes,
} from './report-chart-model';
import { ReportChart } from './report-chart';

const TYPE_ICONS: Record<string, string> = {
  BAR: 'bar_chart',
  LINE: 'show_chart',
  AREA: 'area_chart',
  SCATTER: 'scatter_plot',
  PIE: 'pie_chart',
};

const CATEGORY_ICONS: Record<string, string> = {
  NUMBER: 'tag',
  DATE: 'calendar_month',
  BOOL: 'toggle_on',
  JSON: 'data_object',
  TEXT: 'text_fields',
};

/**
 * The visual chart designer (ADR-0045): the data's columns as fields to drag onto the X axis (across), the Y axis (up)
 * or "colour by"; either axis may hold the column grouped by and the other the totalled values — or each axis one column
 * as it is, drawing the rows themselves. Several charts per report, each with its type, width, order and labels, and a
 * live preview of the unsaved chart.
 */
@Component({
  selector: 'dc-report-chart-designer',
  imports: [
    SearchableSelectModule,
    CdkDrag,
    CdkDropList,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    NgTemplateOutlet,
    ReportChart,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-chart-designer.html',
  styleUrl: './report-chart-designer.scss',
})
export class ReportChartDesigner {
  protected readonly i18n = inject(I18nService);
  private readonly api = inject(ReportsApi);
  readonly def = model.required<ReportDefinition>();
  readonly payload = input.required<ReportDefinition>();
  readonly query = input.required<EndpointQuery>();
  readonly sources = input<QuerySourceInfo[]>([]);

  protected readonly types = REPORT_CHART_TYPES;
  protected readonly typeIcons = TYPE_ICONS;
  protected readonly buckets = REPORT_BUCKETS;
  protected readonly sorts = REPORT_CHART_SORTS;
  protected readonly maxCharts = MAX_CHARTS;
  protected readonly rowCount = ROW_COUNT;
  protected readonly selected = signal(0);

  /** The data's columns as fields, with their kind and labels. */
  protected readonly fields = computed<ChartSourceField[]>(() =>
    endpointColumns(this.query(), this.sources()).map((c) => {
      const info = c.source.columns.find((x) => x.name === c.name);
      return {
        ref: c.ref,
        category: c.category,
        labelAr: info?.labelAr || c.name,
        labelEn: info?.labelEn || c.name,
      };
    }),
  );
  protected readonly chart = computed<ReportChartDef | undefined>(
    () => this.def().charts[this.selected()],
  );
  protected readonly mode = computed(() => {
    const c = this.chart();
    return c ? chartMode(c) : null;
  });
  protected readonly problems = computed(() =>
    checkCharts(this.def().charts)
      .filter((p) => p.chart === this.selected())
      .map((p) => this.i18n.translate(p.key)),
  );

  protected readonly preview = signal<ReportChartData | null>(null);
  protected readonly previewErrors = signal<string[]>([]);
  protected readonly previewing = signal(false);

  constructor() {
    // The live preview: the unsaved chart run a moment after it stops changing, when it has no problems.
    const request = computed(() => {
      const chart = this.chart();
      const payload = this.payload();
      if (!chart || this.problems().length || !this.query().from.code) return null;
      return JSON.stringify({
        chart: chart.name,
        payload: { ...payload, charts: payload.charts.filter((c) => c.name === chart.name) },
      });
    });
    toObservable(request)
      .pipe(
        distinctUntilChanged(),
        debounceTime(600),
        tap(() => this.previewErrors.set([])),
        switchMap((text) => {
          if (!text) return of(null);
          const { chart, payload } = JSON.parse(text) as {
            chart: string;
            payload: ReportDefinition;
          };
          this.previewing.set(true);
          return this.api.previewChart(payload, chart, {}).pipe(
            catchError((error) => {
              this.previewErrors.set(problemMessages(error));
              return of(null);
            }),
            map((data) => {
              this.previewing.set(false);
              return data;
            }),
          );
        }),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((data) => this.preview.set(data));
  }

  // ------------------------------------------------------------------ labels and icons

  protected fieldLabel(f: ChartSourceField): string {
    return this.i18n.language() === 'ar' ? f.labelAr : f.labelEn;
  }

  protected categoryIcon(category: string | undefined): string {
    return CATEGORY_ICONS[category ?? 'TEXT'] ?? 'text_fields';
  }

  protected categoryOf(column: string | null): string | undefined {
    return this.fields().find((f) => f.ref === column)?.category;
  }

  protected chipLabel(f: ReportChartField): string {
    const text = this.i18n.language() === 'ar' ? f.labelAr : f.labelEn;
    if (text) return text;
    if (!f.column) return this.i18n.translate('rp.cd.rows');
    return f.column.split('.').pop() ?? f.column;
  }

  protected aggregatesOf(f: ReportChartField) {
    return fieldAggregates(this.categoryOf(f.column), !!f.column);
  }

  protected chartTitle(c: ReportChartDef, i: number): string {
    return (
      (this.i18n.language() === 'ar' ? c.titleAr : c.titleEn) ||
      `${this.i18n.translate('rp.cd.chart')} ${i + 1}`
    );
  }

  protected modeText(): string {
    const mode = this.mode();
    return mode
      ? this.i18n.translate('rp.cd.mode.' + mode)
      : this.i18n.translate('rp.cd.mode.none');
  }

  // ------------------------------------------------------------------ charts

  private update(next: ReportChartDef): void {
    const index = this.selected();
    this.def.update((d) => ({ ...d, charts: d.charts.map((c, i) => (i === index ? next : c)) }));
  }

  protected set(change: Partial<ReportChartDef>): void {
    const c = this.chart();
    if (c) this.update({ ...copyChart(c), ...change });
  }

  protected addChart(): void {
    const chart = emptyChart(this.def().charts);
    this.def.update((d) => ({ ...d, charts: [...d.charts, chart] }));
    this.selected.set(this.def().charts.length - 1);
    this.preview.set(null);
  }

  protected select(i: number): void {
    this.selected.set(i);
    this.preview.set(null);
  }

  protected duplicate(): void {
    const c = this.chart();
    if (!c) return;
    const copy = { ...copyChart(c), name: emptyChart(this.def().charts).name };
    this.def.update((d) => ({ ...d, charts: [...d.charts, copy] }));
    this.selected.set(this.def().charts.length - 1);
  }

  protected removeChart(): void {
    const index = this.selected();
    this.def.update((d) => ({ ...d, charts: d.charts.filter((_, i) => i !== index) }));
    this.selected.set(Math.max(0, index - 1));
    this.preview.set(null);
  }

  protected move(by: number): void {
    const index = this.selected();
    const to = index + by;
    const charts = [...this.def().charts];
    if (to < 0 || to >= charts.length) return;
    [charts[index], charts[to]] = [charts[to], charts[index]];
    this.def.update((d) => ({ ...d, charts }));
    this.selected.set(to);
  }

  // ------------------------------------------------------------------ fields on the axes

  private source(ref: string): ChartSourceField | null {
    if (ref === ROW_COUNT) return { ref, category: 'NUMBER', labelAr: 'العدد', labelEn: 'Count' };
    return this.fields().find((f) => f.ref === ref) ?? null;
  }

  /** A field dragged from the list onto an axis or "colour by". */
  protected drop(zone: ChartZone, event: CdkDragDrop<unknown>): void {
    this.add(zone, String(event.item.data));
  }

  /** A field chosen in an axis's list (the same as dragging it there). */
  protected add(zone: ChartZone, ref: string): void {
    const c = this.chart();
    const source = this.source(ref);
    if (c && source) this.update(dropField(c, zone, source));
  }

  protected remove(zone: ChartZone, index: number): void {
    const c = this.chart();
    if (c) this.update(removeField(c, zone, index));
  }

  protected setOn(
    zone: ChartZone,
    index: number,
    change: Partial<ReportChartField>,
    field: ReportChartField,
  ): void {
    const c = this.chart();
    if (c) this.update(setField(c, zone, index, change, this.categoryOf(field.column)));
  }

  protected swap(): void {
    const c = this.chart();
    if (c) this.update(swapAxes(c));
  }
}

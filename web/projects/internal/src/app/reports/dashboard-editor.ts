import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  AppsControlApi,
  DashboardDefinition,
  DashboardView,
  DashboardWidgetDef,
  DashboardWidgetKind,
  I18nService,
  REPORT_FILTER_TYPES,
  ReportFilterDef,
  ReportFilterType,
  ReportSummary,
  ReportView,
  ReportsApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, map, of } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { payloadOf } from '../apps-control/config/form-values';
import { DraftsService } from '../apps-control/drafts.service';
import { SubmitButton } from '../apps-control/submit-button';
import {
  DashboardProblem,
  FilterSource,
  WIDGET_SPANS,
  checkDashboard,
  dashboardPayload,
  defaultSpan,
  emptyWidget,
  filterSource,
  fits,
  normalizeDashboard,
  widgetName,
  withFilterSource,
} from './dashboard-model';
import { DASHBOARD_TRANSLATIONS } from './dashboard-translations';
import { REPORT_TRANSLATIONS } from './report-translations';

/**
 * Opens the editor for a shared dashboard (from its configuration screen: a row or a new one) or for one of the user's
 * own (<c>personal</c>: the dashboard, or null for a new one).
 */
export interface DashboardEditorData extends Partial<ConfigEditorData> {
  personal?: DashboardView | null;
}

const KINDS: DashboardWidgetKind[] = ['CHART', 'TILE', 'TABLE'];

/**
 * Designs a dashboard (ADR-0047): its filters, and its widgets — a report's tile, chart or first rows, a title, a width
 * and how each report filter is filled (the dashboard filter of the same name by default, another dashboard filter, a
 * fixed value or the report's default). A shared dashboard is saved to a change set; once published it is a screen roles
 * grant. The user's own dashboard is saved at once.
 */
@Component({
  selector: 'dc-dashboard-editor',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    SubmitButton,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-editor.html',
  styleUrl: './dashboard-editor.scss',
})
export class DashboardEditor {
  protected readonly data = inject<DashboardEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<DashboardEditor, boolean | DashboardView>);
  private readonly api = inject(ReportsApi);
  private readonly config = inject(AppsControlApi);
  private readonly drafts = inject(DraftsService);
  protected readonly i18n = inject(I18nService);

  protected readonly entityType = 'rpt.dashboard';
  protected readonly personal = 'personal' in this.data;
  protected readonly isNew = this.personal ? !this.data.personal : !this.data.row;
  protected readonly kinds = KINDS;
  protected readonly spans = WIDGET_SPANS;
  protected readonly filterTypes = REPORT_FILTER_TYPES;

  protected readonly code = signal(String(this.data.row?.code ?? ''));
  protected readonly def = signal<DashboardDefinition>(this.initial());
  protected readonly isDefault = signal(this.data.personal?.isDefault ?? false);
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);
  /** Each widget's report as the viewer sees it (its tiles, charts and filters); null when it cannot be opened. */
  protected readonly views = signal<Record<string, ReportView | null>>({});

  protected readonly reports = toSignal(
    this.api.mine().pipe(catchError(() => of([] as ReportSummary[]))),
    { initialValue: [] as ReportSummary[] },
  );
  /** Lists of values for list filters: all of them for a shared dashboard's maker; the lists the user's reports use otherwise. */
  private readonly allLovs = this.personal
    ? signal<{ code: string; nameAr: string; nameEn: string }[]>([])
    : toSignal(
        this.config.list('apps.lov').pipe(
          map((rows) => rows.map((r) => ({ code: r.code, nameAr: r.nameAr, nameEn: r.nameEn }))),
          catchError(() => of([] as { code: string; nameAr: string; nameEn: string }[])),
        ),
        { initialValue: [] as { code: string; nameAr: string; nameEn: string }[] },
      );
  protected readonly lovs = computed(() => {
    if (!this.personal) return this.allLovs();
    const seen = new Map<string, { code: string; nameAr: string; nameEn: string }>();
    for (const view of Object.values(this.views()))
      for (const f of view?.filters ?? [])
        if (f.type === 'LIST' && f.lov && !seen.has(f.lov))
          seen.set(f.lov, { code: f.lov, nameAr: f.labelAr, nameEn: f.labelEn });
    return [...seen.values()];
  });
  protected readonly problems = computed(() =>
    checkDashboard(this.def(), this.code().trim(), this.personal, this.views()),
  );

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
    this.i18n.register(DASHBOARD_TRANSLATIONS);
    for (const code of new Set(
      this.def()
        .widgets.map((w) => w.report)
        .filter(Boolean),
    ))
      void this.loadView(code);
  }

  private initial(): DashboardDefinition {
    if (this.personal) {
      const p = this.data.personal;
      return normalizeDashboard(
        p
          ? { nameAr: p.nameAr, nameEn: p.nameEn, filters: p.filters, widgets: p.definition ?? [] }
          : {},
      );
    }
    return normalizeDashboard({ ...payloadOf(this.data.row), ...(this.data.defaults ?? {}) });
  }

  protected ar(): boolean {
    return this.i18n.language() === 'ar';
  }

  protected name(item: { nameAr: string; nameEn: string }): string {
    return this.ar() ? item.nameAr : item.nameEn;
  }

  protected label(item: { labelAr: string; labelEn: string }): string {
    return this.ar() ? item.labelAr : item.labelEn;
  }

  protected title(item: { titleAr: string; titleEn: string }): string {
    return this.ar() ? item.titleAr : item.titleEn;
  }

  protected problemText(p: DashboardProblem): string {
    let text = this.i18n.translate(p.key);
    for (const [k, v] of Object.entries(p.params ?? {})) text = text.replace(`{${k}}`, String(v));
    return text;
  }

  protected set(change: Partial<DashboardDefinition>): void {
    this.def.update((d) => ({ ...d, ...change }));
  }

  private async loadView(code: string): Promise<void> {
    if (!code || code in this.views()) return;
    try {
      const view = await firstValueFrom(this.api.view(code));
      this.views.update((v) => ({ ...v, [code]: view }));
    } catch {
      this.views.update((v) => ({ ...v, [code]: null }));
    }
  }

  // ------------------------------------------------------------------ filters

  protected addFilter(): void {
    const taken = this.def().filters.map((f) => f.name);
    let name = 'filter';
    for (let n = 2; taken.includes(name); n++) name = `filter${n}`;
    this.set({
      filters: [
        ...this.def().filters,
        {
          name,
          labelAr: '',
          labelEn: '',
          type: 'TEXT',
          lov: null,
          multiple: false,
          required: false,
          default: null,
        },
      ],
    });
  }

  protected setFilter(index: number, change: Partial<ReportFilterDef>): void {
    this.set({
      filters: this.def().filters.map((f, i) => (i === index ? { ...f, ...change } : f)),
    });
  }

  protected setFilterType(index: number, type: ReportFilterType): void {
    this.setFilter(index, {
      type,
      lov: type === 'LIST' ? this.def().filters[index].lov : null,
      multiple: false,
    });
  }

  protected removeFilter(index: number): void {
    const name = this.def().filters[index].name;
    this.set({
      filters: this.def().filters.filter((_, i) => i !== index),
      widgets: this.def().widgets.map((w) => ({
        ...w,
        bind: Object.fromEntries(Object.entries(w.bind).filter(([, v]) => v !== name)),
      })),
    });
  }

  // ------------------------------------------------------------------ widgets

  protected addWidget(): void {
    const widgets = this.def().widgets;
    this.set({
      widgets: [
        ...widgets,
        emptyWidget(
          widgetName(
            'widget',
            widgets.map((w) => w.name),
          ),
        ),
      ],
    });
  }

  protected setWidget(index: number, change: Partial<DashboardWidgetDef>): void {
    this.set({
      widgets: this.def().widgets.map((w, i) => (i === index ? { ...w, ...change } : w)),
    });
  }

  /** A new report for a widget: its first chart (or tile), and a name after it; fixed values and bindings reset. */
  protected setReport(index: number, report: string): void {
    void this.loadView(report).then(() => {
      const view = this.views()[report];
      const w = this.def().widgets[index];
      const kind: DashboardWidgetKind = view?.charts.length
        ? w.kind
        : view?.tiles.length
          ? 'TILE'
          : 'TABLE';
      this.setWidget(index, {
        report,
        kind,
        item: this.firstItem(view, kind),
        bind: {},
        values: {},
        span: defaultSpan(kind),
      });
    });
  }

  protected setKind(index: number, kind: DashboardWidgetKind): void {
    const w = this.def().widgets[index];
    this.setWidget(index, {
      kind,
      item: this.firstItem(this.views()[w.report], kind),
      span: defaultSpan(kind),
    });
  }

  private firstItem(view: ReportView | null | undefined, kind: DashboardWidgetKind): string | null {
    if (kind === 'TILE') return view?.tiles[0]?.name ?? null;
    if (kind === 'CHART') return view?.charts[0]?.name ?? null;
    return null;
  }

  protected moveWidget(index: number, by: number): void {
    const widgets = [...this.def().widgets];
    const to = index + by;
    if (to < 0 || to >= widgets.length) return;
    [widgets[index], widgets[to]] = [widgets[to], widgets[index]];
    this.set({ widgets });
  }

  protected removeWidget(index: number): void {
    this.set({ widgets: this.def().widgets.filter((_, i) => i !== index) });
  }

  /** The select's value for how a report filter is filled: DEFAULT, FIXED or the bound dashboard filter. */
  protected sourceKey(widget: DashboardWidgetDef, filter: ReportFilterDef): string {
    const s = filterSource(widget, filter, this.def().filters);
    return s.kind === 'BOUND' ? 'BOUND:' + s.filter : s.kind;
  }

  protected fixedValue(widget: DashboardWidgetDef, filter: ReportFilterDef): string {
    return widget.values[filter.name] ?? '';
  }

  /** The dashboard filters that can fill a report filter (same kind; a list one of the same list). */
  protected candidates(filter: ReportFilterDef): ReportFilterDef[] {
    return this.def().filters.filter((d) => fits(d, filter));
  }

  protected setSource(index: number, filter: ReportFilterDef, key: string, value = ''): void {
    const source: FilterSource = key.startsWith('BOUND:')
      ? { kind: 'BOUND', filter: key.slice(6) }
      : key === 'FIXED'
        ? { kind: 'FIXED', value: value || filter.default || '-' }
        : { kind: 'DEFAULT' };
    const w = this.def().widgets[index];
    this.setWidget(index, withFilterSource(w, filter, source, this.def().filters));
  }

  protected setFixed(index: number, filter: ReportFilterDef, value: string): void {
    const w = this.def().widgets[index];
    this.setWidget(
      index,
      withFilterSource(w, filter, { kind: 'FIXED', value }, this.def().filters),
    );
  }

  // ------------------------------------------------------------------ save

  protected async save(submit = false): Promise<void> {
    this.errors.set([]);
    if (this.problems().length) {
      this.errors.set(this.problems().map((p) => this.problemText(p)));
      return;
    }
    const payload = dashboardPayload(this.def());
    this.saving.set(true);
    try {
      if (this.personal) {
        const saved = await firstValueFrom(
          this.api.saveMyDashboard(this.data.personal?.id ?? null, {
            name: payload.nameEn,
            filters: payload.filters,
            widgets: payload.widgets,
            isDefault: this.isDefault(),
          }),
        );
        this.ref.close(saved);
      } else {
        const body: Record<string, unknown> = { ...payloadOf(this.data.row), ...payload };
        await this.drafts.addChange(
          this.entityType,
          this.code().trim(),
          this.isNew ? 'CREATE' : 'UPDATE',
          body,
          {
            submit,
          },
        );
        this.ref.close(true);
      }
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected close(): void {
    this.ref.close(false);
  }
}

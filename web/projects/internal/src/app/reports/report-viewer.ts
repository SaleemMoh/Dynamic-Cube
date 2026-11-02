import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import {
  AppsControlApi,
  DashboardView,
  I18nService,
  LovItem,
  ReportFilterDef,
  ReportFormat,
  ReportRun,
  ReportView,
  ReportsApi,
  SavedReportFilter,
  TranslatePipe,
  problemMessages,
  saveBlob,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, forkJoin, map, of, tap } from 'rxjs';
import { cleanFilters, exportFileName, joinValues, splitValues } from './report-model';
import { ReportDrill, ReportDrillData } from './report-drill';
import { ReportPin, ReportPinData } from './report-pin';
import { DrillTarget, ReportResult } from './report-result';
import { REPORT_TRANSLATIONS } from './report-translations';

const NONE: string[] = [];

/**
 * One report (ADR-0045): the filters (list filters show their list's values), the user's saved filters (the default
 * one applies on opening), tiles, the chart and the paged table with its totals row, and export to Excel or CSV.
 * Rows follow the user's data permissions, company and branches.
 */
@Component({
  selector: 'dc-report-viewer',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSelectModule,
    ReportResult,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-viewer.html',
  styleUrl: './report-viewer.scss',
})
export class ReportViewer {
  private readonly api = inject(ReportsApi);
  private readonly config = inject(AppsControlApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);
  readonly code = input.required<string>();

  protected readonly values = signal<Record<string, string | null>>({});
  /** The values the user chose (not the defaults filled in): what a pinned widget keeps, so relative dates stay relative. */
  private readonly chosen = signal<Record<string, string | null>>({});
  protected readonly run = signal<ReportRun | null>(null);
  protected readonly running = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly fieldErrors = signal<Record<string, string>>({});
  protected readonly saved = signal<SavedReportFilter[]>([]);
  protected readonly saving = signal(false);
  protected readonly saveName = signal('');
  protected readonly saveDefault = signal(false);
  protected readonly showSave = signal(false);
  protected readonly notice = signal('');
  /** The personal dashboard a widget was just pinned to (a link to open it). */
  protected readonly pinned = signal<DashboardView | null>(null);

  protected readonly view = rxResource({
    params: () => this.code(),
    stream: ({ params }) =>
      this.api.view(params).pipe(
        tap((view) => this.opened(view)),
        catchError((error) => {
          this.errors.set(problemMessages(error));
          return of(null);
        }),
      ),
  });

  /** The values of each list filter's list (up to 200). */
  protected readonly options = rxResource({
    params: () => this.view.value()?.filters.filter((f) => f.type === 'LIST' && f.lov) ?? [],
    stream: ({ params }) =>
      params.length
        ? forkJoin(
            params.map((f) =>
              this.config.lovValues(f.lov!, { size: 200 }).pipe(
                map((page) => [f.name, page.items] as const),
                catchError(() => of([f.name, [] as LovItem[]] as const)),
              ),
            ),
          ).pipe(map((pairs) => Object.fromEntries(pairs) as Record<string, LovItem[]>))
        : of({} as Record<string, LovItem[]>),
  });

  protected readonly name = computed(() => {
    const v = this.view.value();
    return v ? (this.ar() ? v.nameAr : v.nameEn) : '';
  });
  protected readonly description = computed(() => {
    const v = this.view.value();
    return v ? (this.ar() ? v.descriptionAr : v.descriptionEn) : null;
  });

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  protected ar(): boolean {
    return this.i18n.language() === 'ar';
  }

  protected label(f: { labelAr: string; labelEn: string }): string {
    return this.ar() ? f.labelAr : f.labelEn;
  }

  protected itemLabel(item: LovItem): string {
    return this.ar() ? item.labelAr : item.labelEn;
  }

  /** On opening: the default saved filters (else the report's defaults) and the first page. */
  private opened(view: ReportView): void {
    this.saved.set(view.savedFilters);
    const preferred = view.savedFilters.find((s) => s.isDefault);
    this.values.set(preferred ? { ...preferred.values } : {});
    this.chosen.set(preferred ? { ...preferred.values } : {});
    // The resource's value is set after this tap: the first run takes the view itself.
    void this.load(1, view);
  }

  protected value(f: ReportFilterDef): string {
    return this.values()[f.name] ?? '';
  }

  /** The chosen values of each list filter taking several, kept as one array per change (a new array each check would loop). */
  private readonly multiValues = computed(() => {
    const values = this.values();
    return Object.fromEntries(
      Object.keys(values).map((k) => [k, splitValues(values[k])]),
    ) as Record<string, string[]>;
  });

  protected multi(f: ReportFilterDef): string[] {
    return this.multiValues()[f.name] ?? NONE;
  }

  protected setValue(name: string, value: string | null): void {
    this.values.update((v) => ({ ...v, [name]: value || null }));
    this.chosen.update((v) => ({ ...v, [name]: value || null }));
  }

  protected setMulti(name: string, values: string[]): void {
    this.setValue(name, joinValues(values));
  }

  protected clear(): void {
    this.values.set({});
    this.chosen.set({});
    void this.load(1);
  }

  protected apply(saved: SavedReportFilter): void {
    this.values.set({ ...saved.values });
    this.chosen.set({ ...saved.values });
    void this.load(1);
  }

  /** Runs the report for a page; the filters applied (defaults filled in) come back and show in the form. */
  protected async load(page: number, view = this.view.value()): Promise<void> {
    if (!view) return;
    this.running.set(true);
    this.errors.set([]);
    this.fieldErrors.set({});
    try {
      const run = await firstValueFrom(this.api.run(view.code, cleanFilters(this.values()), page));
      this.run.set(run);
      this.values.set({ ...run.filters, ...cleanFilters(this.values()) });
    } catch (error) {
      this.run.set(null);
      const fields = (error as { error?: { errors?: Record<string, string[]> } })?.error?.errors;
      if (fields) {
        this.fieldErrors.set(
          Object.fromEntries(Object.entries(fields).map(([k, v]) => [k, v.join(' ')])),
        );
      }
      this.errors.set(problemMessages(error));
    } finally {
      this.running.set(false);
    }
  }

  protected async export(format: ReportFormat): Promise<void> {
    const view = this.view.value();
    if (!view) return;
    this.errors.set([]);
    try {
      const blob = await firstValueFrom(
        this.api.export(view.code, cleanFilters(this.values()), format, this.ar() ? 'ar' : 'en'),
      );
      saveBlob(blob, exportFileName(view.code, format));
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  /** Opens the rows behind the clicked group, with the filters the page ran with. */
  protected drill(target: DrillTarget): void {
    const view = this.view.value();
    if (!view) return;
    this.dialog.open<ReportDrill, ReportDrillData>(ReportDrill, {
      data: {
        report: view.code,
        reportNameAr: view.nameAr,
        reportNameEn: view.nameEn,
        titleAr: target.titleAr,
        titleEn: target.titleEn,
        canExport: view.canExport,
        request: {
          filters: cleanFilters(this.run()?.filters ?? this.values()),
          source: target.source,
          chart: target.chart,
          values: target.values,
        },
      },
      maxWidth: '96vw',
    });
  }

  /** Pins a chart, tile or the table to one of the user's dashboards. */
  protected pin(): void {
    const view = this.view.value();
    if (!view) return;
    this.dialog
      .open<ReportPin, ReportPinData, DashboardView>(ReportPin, {
        data: { view, filters: this.chosen() },
      })
      .afterClosed()
      .subscribe((board) => {
        if (!board) return;
        this.pinned.set(board);
        this.notice.set(this.i18n.translate('rp.pin.done').replace('{name}', board.nameEn));
      });
  }

  protected openPinned(): void {
    const board = this.pinned();
    if (board?.id) void this.router.navigate(['/reports/my-dashboards', board.id]);
  }

  protected async save(): Promise<void> {
    const view = this.view.value();
    const name = this.saveName().trim();
    if (!view || !name) return;
    this.saving.set(true);
    try {
      const saved = await firstValueFrom(
        this.api.saveFilter(view.code, name, cleanFilters(this.values()), this.saveDefault()),
      );
      this.saved.update((list) => [
        ...list
          .filter((s) => s.name !== saved.name)
          .map((s) => (saved.isDefault ? { ...s, isDefault: false } : s)),
        saved,
      ]);
      this.showSave.set(false);
      this.saveName.set('');
      this.saveDefault.set(false);
      this.notice.set(this.i18n.translate('rp.saved.done'));
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(saved: SavedReportFilter): Promise<void> {
    const view = this.view.value();
    if (!view) return;
    try {
      await firstValueFrom(this.api.deleteFilter(view.code, saved.id));
      this.saved.update((list) => list.filter((s) => s.id !== saved.id));
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

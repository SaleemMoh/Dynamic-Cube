import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import {
  AppsControlApi,
  BusinessAppContext,
  I18nService,
  ReportDefinition,
  ReportRun,
  ReportsApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, map, of } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { payloadOf } from '../apps-control/config/form-values';
import { DraftsService } from '../apps-control/drafts.service';
import { QuerySourceInfo } from '../apps-control/lovs/lov-builder-model';
import { LOV_BUILDER_TRANSLATIONS } from '../apps-control/lovs/lov-builder-translations';
import { SubmitButton } from '../apps-control/submit-button';
import { EndpointQuery } from '../integration/api-endpoint-model';
import { PUBLIC_API_TRANSLATIONS } from '../integration/public-api-translations';
import { LovOption, ReportDesignerParts } from './report-designer-parts';
import {
  REPORT_PARTS,
  ReportProblem,
  checkReport,
  cleanFilters,
  normalizeReport,
  reportPayload,
  reportQuery,
} from './report-model';
import { ReportResult } from './report-result';
import { REPORT_TRANSLATIONS } from './report-translations';

/**
 * Designs a report (ADR-0045): its data built in the visual query builder over registered tables and classes, the
 * columns with their totals (grouping by the others), the filters users fill, tiles and a chart, and its details. The
 * preview runs the unsaved report with the designer's own access. Saving adds it to a change set like every
 * configuration; once published it is a screen roles grant and menus open.
 */
@Component({
  selector: 'dc-report-designer',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTabsModule,
    ReportDesignerParts,
    ReportResult,
    SubmitButton,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-designer.html',
  styleUrls: ['../integration/api-endpoint-designer.scss', './report-designer.scss'],
})
export class ReportDesigner {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReportDesigner, boolean>);
  private readonly config = inject(AppsControlApi);
  private readonly reports = inject(ReportsApi);
  protected readonly drafts = inject(DraftsService);
  protected readonly i18n = inject(I18nService);

  protected readonly entityType = 'rpt.report';
  protected readonly parts = REPORT_PARTS;
  protected readonly isNew = !this.data.row;

  protected readonly code = signal(this.data.row?.code ?? '');
  protected readonly def = signal<ReportDefinition>(
    normalizeReport({ ...payloadOf(this.data.row), ...(this.data.defaults ?? {}) }),
  );
  protected readonly query = signal<EndpointQuery>(reportQuery(this.def()));
  private readonly entityCode =
    (this.data.row?.['entityCode'] as string | null | undefined) ?? null;
  private readonly businessApps = this.initialApps();
  protected readonly tab = signal(0);

  protected readonly sources = toSignal(
    this.config.lovBuilderSources<QuerySourceInfo>().pipe(
      map((all) => all.filter((s) => s.kind === 'CLASS' || s.kind === 'DATA_OBJECT')),
      catchError(() => of([] as QuerySourceInfo[])),
    ),
    { initialValue: [] as QuerySourceInfo[] },
  );
  protected readonly lovs = toSignal(
    this.config.list('apps.lov').pipe(
      map((rows) =>
        rows.map((r): LovOption => ({ code: r.code, nameAr: r.nameAr, nameEn: r.nameEn })),
      ),
      catchError(() => of([] as LovOption[])),
    ),
    { initialValue: [] as LovOption[] },
  );

  protected readonly problems = computed(() =>
    checkReport(this.code().trim(), this.def(), this.query(), this.isNew, this.sources()),
  );
  protected readonly partProblems = computed(() =>
    REPORT_PARTS.map((p) => this.problems().filter((x) => x.part === p).length),
  );
  protected readonly payload = computed(() => reportPayload(this.def(), this.query()));

  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);

  // Preview: filter values typed here, the run and its errors.
  protected readonly previewFilters = signal<Record<string, string | null>>({});
  protected readonly preview = signal<ReportRun | null>(null);
  protected readonly previewing = signal(false);
  protected readonly previewErrors = signal<string[]>([]);

  constructor() {
    this.i18n.register(PUBLIC_API_TRANSLATIONS);
    this.i18n.register(LOV_BUILDER_TRANSLATIONS);
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  protected set(change: Partial<ReportDefinition>): void {
    this.def.update((d) => ({ ...d, ...change }));
  }

  protected setPreviewFilter(name: string, value: string): void {
    this.previewFilters.update((f) => ({ ...f, [name]: value || null }));
  }

  protected problemText(problem: ReportProblem): string {
    let text = this.i18n.translate(problem.key);
    for (const [k, v] of Object.entries(problem.params ?? {}))
      text = text.replace(`{${k}}`, String(v));
    return text;
  }

  protected async runPreview(): Promise<void> {
    this.previewErrors.set([]);
    if (this.problems().length) {
      this.previewErrors.set([
        this.i18n.translate('rp.preview.fix'),
        ...this.problems().map((p) => this.problemText(p)),
      ]);
      return;
    }
    this.previewing.set(true);
    try {
      this.preview.set(
        await firstValueFrom(
          this.reports.preview(this.payload(), cleanFilters(this.previewFilters())),
        ),
      );
    } catch (error) {
      this.preview.set(null);
      this.previewErrors.set(problemMessages(error));
    } finally {
      this.previewing.set(false);
    }
  }

  protected async save(submit = false): Promise<void> {
    this.errors.set([]);
    if (this.problems().length) {
      this.tab.set(REPORT_PARTS.indexOf(this.problems()[0].part));
      this.errors.set(this.problems().map((p) => this.problemText(p)));
      return;
    }
    const payload: Record<string, unknown> = {
      ...payloadOf(this.data.row),
      ...this.payload(),
      entityCode: this.entityCode ?? '',
      businessApps: this.businessApps,
    };
    delete payload['code'];
    this.saving.set(true);
    try {
      await this.drafts.addChange(
        this.entityType,
        this.code().trim(),
        this.isNew ? 'CREATE' : 'UPDATE',
        payload,
        { submit },
      );
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected close(): void {
    this.ref.close(false);
  }

  private initialApps(): string[] {
    const row = this.data.row;
    if (row) return (row['businessApps'] as string[] | undefined) ?? [];
    const current = inject(BusinessAppContext).current();
    return current ? [current] : [];
  }
}

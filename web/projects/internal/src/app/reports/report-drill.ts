import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import {
  DrillRequest,
  DrillRun,
  DrillValue,
  I18nService,
  ReportFormat,
  ReportRunColumn,
  ReportsApi,
  TranslatePipe,
  problemMessages,
  saveBlob,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { exportFileName, formatValue } from './report-model';
import { REPORT_TRANSLATIONS } from './report-translations';

/** What the drill-down opens: the report, the clicked group, a title and whether the rows export. */
export interface ReportDrillData {
  report: string;
  reportNameAr: string;
  reportNameEn: string;
  titleAr?: string;
  titleEn?: string;
  request: DrillRequest;
  canExport: boolean;
}

/**
 * The rows behind a group (ADR-0047): the report's query with its filters and the user's access, narrowed to the clicked
 * group (a table row, a chart's bar, point or slice, a tile or the totals row). Paged, and exported to Excel or CSV when
 * the user may export the report.
 */
@Component({
  selector: 'dc-report-drill',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTableModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ title() }}</h2>
    <mat-dialog-content class="rd-body">
      @let r = run();
      <div class="rd-narrowed" data-testid="drill-narrowed">
        @for (n of r?.narrowed ?? []; track $index) {
          <span class="rd-chip">
            <strong>{{ lang() === 'ar' ? n.labelAr : n.labelEn }}</strong>
            {{ valueText(n) }}
          </span>
        } @empty {
          @if (r) {
            <span class="rd-chip">{{ 'rp.drill.all' | dcTranslate }}</span>
          }
        }
      </div>
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
      @if (r) {
        <p class="rd-count">{{ count() }}</p>
        <div class="rd-scroll">
          <table mat-table [dataSource]="r.rows" class="dc-table" data-testid="drill-table">
            @for (c of r.columns; track c.name) {
              <ng-container [matColumnDef]="c.name">
                <th mat-header-cell *matHeaderCellDef [class.rd-num]="c.type === 'NUMBER'">
                  {{ label(c) }}
                </th>
                <td mat-cell *matCellDef="let row" [class.rd-num]="c.type === 'NUMBER'">
                  {{ cell(row[c.name], c) }}
                </td>
              </ng-container>
            }
            <tr mat-header-row *matHeaderRowDef="names()"></tr>
            <tr mat-row *matRowDef="let row; columns: names()"></tr>
          </table>
        </div>
        @if (!r.rows.length) {
          <p class="dc-empty">{{ 'rp.noRows' | dcTranslate }}</p>
        }
        @if (pages() > 1) {
          <div class="rd-pager">
            <button matButton type="button" [disabled]="r.page <= 1" (click)="load(r.page - 1)">
              {{ 'rp.prev' | dcTranslate }}
            </button>
            <span>{{ r.page }} / {{ pages() }}</span>
            <button
              matButton
              type="button"
              [disabled]="!r.hasMore"
              (click)="load(r.page + 1)"
              data-testid="drill-next"
            >
              {{ 'rp.next' | dcTranslate }}
            </button>
          </div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      @if (data.canExport) {
        <button matButton type="button" [matMenuTriggerFor]="exportMenu" data-testid="drill-export">
          <mat-icon fontSet="material-symbols-outlined">download</mat-icon
          >{{ 'rp.export' | dcTranslate }}
        </button>
        <mat-menu #exportMenu="matMenu">
          <button mat-menu-item type="button" (click)="export('XLSX')">
            {{ 'rp.export.XLSX' | dcTranslate }}
          </button>
          <button mat-menu-item type="button" (click)="export('CSV')">
            {{ 'rp.export.CSV' | dcTranslate }}
          </button>
        </mat-menu>
      }
      <button matButton="filled" type="button" mat-dialog-close>
        {{ 'rp.close' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .rd-body {
      display: grid;
      gap: 10px;
      min-width: min(60rem, 90vw);
    }
    .rd-narrowed {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .rd-chip {
      display: inline-flex;
      gap: 6px;
      padding: 3px 10px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 13px;
    }
    .rd-count {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
    .rd-scroll {
      overflow-x: auto;
      max-height: 60vh;
    }
    .rd-num {
      text-align: end;
      white-space: nowrap;
    }
    .rd-pager {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      font-size: 13px;
    }
  `,
})
export class ReportDrill {
  protected readonly data = inject<ReportDrillData>(MAT_DIALOG_DATA);
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(I18nService);

  protected readonly run = signal<DrillRun | null>(null);
  protected readonly loading = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly lang = computed(() => (this.i18n.language() === 'ar' ? 'ar' : 'en'));
  protected readonly names = computed(() => this.run()?.columns.map((c) => c.name) ?? []);
  protected readonly pages = computed(() => {
    const r = this.run();
    return r ? Math.max(1, Math.ceil(r.total / r.pageSize)) : 1;
  });
  protected readonly count = computed(() =>
    this.i18n
      .translate('rp.rows')
      .replace('{n}', formatValue(this.run()?.total ?? 0, 'NUMBER', this.lang())),
  );
  protected readonly title = computed(() => {
    const report = this.lang() === 'ar' ? this.data.reportNameAr : this.data.reportNameEn;
    const part = this.lang() === 'ar' ? this.data.titleAr : this.data.titleEn;
    return `${this.i18n.translate('rp.drill.title')} — ${part ? part + ' · ' : ''}${report}`;
  });

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
    void this.load(1);
  }

  protected label(c: ReportRunColumn): string {
    return this.lang() === 'ar' ? c.labelAr : c.labelEn;
  }

  protected cell(value: unknown, c: ReportRunColumn): string {
    return formatValue(value, c.type, this.lang());
  }

  /** A narrowing value as text; a date grouped by a period shows its period. */
  protected valueText(n: DrillValue): string {
    if (n.value === null || n.value === undefined || n.value === '')
      return this.i18n.translate('rp.drill.empty');
    const text = String(n.value);
    if (n.bucket && /^\d{4}-\d{2}-\d{2}/.test(text)) {
      if (n.bucket === 'YEAR') return text.slice(0, 4);
      if (n.bucket === 'MONTH') return text.slice(0, 7);
      if (n.bucket === 'QUARTER')
        return `${text.slice(0, 4)} Q${Math.floor((Number(text.slice(5, 7)) - 1) / 3) + 1}`;
    }
    return formatValue(n.value, typeof n.value === 'number' ? 'NUMBER' : 'TEXT', this.lang());
  }

  protected async load(page: number): Promise<void> {
    this.loading.set(true);
    this.errors.set([]);
    try {
      this.run.set(
        await firstValueFrom(this.api.drill(this.data.report, { ...this.data.request, page })),
      );
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected async export(format: ReportFormat): Promise<void> {
    this.errors.set([]);
    try {
      const blob = await firstValueFrom(
        this.api.exportDrill(this.data.report, this.data.request, format, this.lang()),
      );
      saveBlob(blob, exportFileName(this.data.report + '-details', format));
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

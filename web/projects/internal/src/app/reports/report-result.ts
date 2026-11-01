import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { I18nService, ReportRun, ReportRunColumn, TranslatePipe } from '@dynamic-cube/shared';
import { ReportChart } from './report-chart';
import { formatValue } from './report-model';

/** A page of a report: its tiles, its chart, the table with a totals row and the page buttons. */
@Component({
  selector: 'dc-report-result',
  imports: [MatButtonModule, MatIconModule, MatTableModule, ReportChart, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let r = run();
    @if (r) {
      @if (r.tiles.length) {
        <div class="rr-tiles" data-testid="report-tiles">
          @for (t of r.tiles; track t.name) {
            <div class="rr-tile" [attr.data-tile]="t.name">
              <span class="rr-tile__label">{{ lang() === 'ar' ? t.labelAr : t.labelEn }}</span>
              <span class="rr-tile__value">{{ number(t.value) }}</span>
            </div>
          }
        </div>
      }
      @if (r.chart && r.chart.rows.length) {
        <dc-report-chart
          class="rr-chart"
          [chart]="r.chart"
          [columns]="r.columns"
          data-testid="report-chart"
        />
      }
      <p class="rr-count">{{ count() }}</p>
      <div class="rr-scroll">
        <table mat-table [dataSource]="r.rows" class="dc-table" data-testid="report-table">
          @for (c of r.columns; track c.name) {
            <ng-container [matColumnDef]="c.name">
              <th mat-header-cell *matHeaderCellDef [class.rr-num]="c.type === 'NUMBER'">
                {{ label(c) }}
              </th>
              <td mat-cell *matCellDef="let row" [class.rr-num]="c.type === 'NUMBER'">
                {{ cell(row[c.name], c) }}
              </td>
              <td mat-footer-cell *matFooterCellDef [class.rr-num]="c.type === 'NUMBER'">
                @if ($first && !c.aggregate) {
                  {{ 'rp.total' | dcTranslate }}
                } @else {
                  {{ cell(r.totals?.[c.name], c) }}
                }
              </td>
            </ng-container>
          }
          <tr mat-header-row *matHeaderRowDef="names()"></tr>
          <tr mat-row *matRowDef="let row; columns: names()"></tr>
          @if (r.totals && r.rows.length) {
            <tr
              mat-footer-row
              *matFooterRowDef="names()"
              class="rr-totals"
              data-testid="report-totals"
            ></tr>
          }
        </table>
      </div>
      @if (!r.rows.length) {
        <p class="dc-empty">{{ 'rp.noRows' | dcTranslate }}</p>
      }
      @if (pages() > 1) {
        <div class="rr-pager">
          <button matButton type="button" [disabled]="r.page <= 1" (click)="page.emit(r.page - 1)">
            {{ 'rp.prev' | dcTranslate }}
          </button>
          <span>{{ pageText() }}</span>
          <button
            matButton
            type="button"
            [disabled]="!r.hasMore"
            (click)="page.emit(r.page + 1)"
            data-testid="report-next"
          >
            {{ 'rp.next' | dcTranslate }}
          </button>
        </div>
      }
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 12px;
      min-width: 0;
    }
    .rr-tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr));
      gap: 10px;
    }
    .rr-tile {
      display: grid;
      gap: 4px;
      padding: 12px 14px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
    }
    .rr-tile__label {
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .rr-tile__value {
      font-size: 22px;
      font-weight: 600;
    }
    .rr-chart {
      padding: 12px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
    }
    .rr-count {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 13px;
    }
    .rr-scroll {
      overflow-x: auto;
    }
    .rr-num {
      text-align: end;
      white-space: nowrap;
    }
    .rr-totals td {
      font-weight: 600;
      border-top: 2px solid var(--dga-gray-300, #d2d6db);
    }
    .rr-pager {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      font-size: 13px;
    }
  `,
})
export class ReportResult {
  private readonly i18n = inject(I18nService);
  readonly run = input<ReportRun | null>(null);
  readonly page = output<number>();

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
  protected readonly pageText = computed(() =>
    this.i18n
      .translate('rp.page')
      .replace('{page}', String(this.run()?.page ?? 1))
      .replace('{pages}', String(this.pages())),
  );

  protected label(c: ReportRunColumn): string {
    return this.lang() === 'ar' ? c.labelAr : c.labelEn;
  }

  protected cell(value: unknown, c: ReportRunColumn): string {
    return formatValue(value, c.type, this.lang());
  }

  /** A tile's value: a number, a date (a minimum or maximum of dates) or text; none shows a dash. */
  protected number(value: unknown): string {
    if (value === null || value === undefined) return '—';
    const type =
      typeof value === 'number' || !Number.isNaN(Number(value))
        ? 'NUMBER'
        : typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)
          ? 'DATE'
          : 'TEXT';
    return formatValue(value, type, this.lang());
  }
}

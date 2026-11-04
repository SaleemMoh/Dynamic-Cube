import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  DashboardSummary,
  DashboardView,
  DashboardWidgetKind,
  I18nService,
  ReportView,
  ReportsApi,
  TranslatePipe,
  problemMessages,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { catchError, firstValueFrom, map, of } from 'rxjs';
import { cleanFilters } from './report-model';
import { REPORT_TRANSLATIONS } from './report-translations';

export interface ReportPinData {
  view: ReportView;
  filters: Record<string, string | null>;
}

interface PinChoice {
  key: string;
  kind: DashboardWidgetKind;
  item: string | null;
  label: string;
}

/**
 * Pins a report's table, one of its tiles or one of its charts to one of the user's own dashboards, or to a new one
 * (ADR-0047). The current filter values can be kept on the widget; otherwise it takes the dashboard's filters or the
 * report's defaults.
 */
@Component({
  selector: 'dc-report-pin',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    SearchableSelectModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'rp.pin.title' | dcTranslate }}</h2>
    <mat-dialog-content class="rpin">
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'rp.pin.what' | dcTranslate }}</mat-label>
        <mat-select
          [ngModel]="choice()"
          (ngModelChange)="choice.set($event)"
          data-testid="pin-what"
        >
          @for (c of choices(); track c.key) {
            <mat-option [value]="c.key">{{ c.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'rp.pin.board' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="board()" (ngModelChange)="board.set($event)" data-testid="pin-board">
          <mat-option value="">{{ 'rp.pin.new' | dcTranslate }}</mat-option>
          @for (d of mine(); track d.id) {
            <mat-option [value]="d.id">{{ d.nameEn }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (!board()) {
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'rp.pin.newName' | dcTranslate }}</mat-label>
          <input
            matInput
            maxlength="200"
            [ngModel]="newName()"
            (ngModelChange)="newName.set($event)"
            data-testid="pin-new-name"
          />
        </mat-form-field>
      }
      <mat-checkbox [ngModel]="keep()" (ngModelChange)="keep.set($event)">{{
        'rp.pin.keep' | dcTranslate
      }}</mat-checkbox>
      <p class="rpin-hint">{{ 'rp.pin.keepHint' | dcTranslate }}</p>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton type="button" mat-dialog-close>{{ 'rp.close' | dcTranslate }}</button>
      <button
        matButton="filled"
        type="button"
        [disabled]="saving() || (!board() && !newName().trim())"
        (click)="save()"
        data-testid="pin-save"
      >
        {{ 'rp.pin.save' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .rpin {
      display: grid;
      gap: 12px;
      min-width: min(28rem, 90vw);
    }
    .rpin-hint {
      margin: -8px 0 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
  `,
})
export class ReportPin {
  private readonly data = inject<ReportPinData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReportPin, DashboardView>);
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(I18nService);

  protected readonly mine = toSignal(
    this.api.dashboards().pipe(
      map((all) => all.filter((d) => d.personal)),
      catchError(() => of([] as DashboardSummary[])),
    ),
    { initialValue: [] as DashboardSummary[] },
  );
  protected readonly choices = computed<PinChoice[]>(() => {
    const ar = this.i18n.language() === 'ar';
    const v = this.data.view;
    const name = (template: string, label: string) =>
      this.i18n.translate(template).replace('{name}', label);
    return [
      ...v.charts.map((c) => ({
        key: 'CHART:' + c.name,
        kind: 'CHART' as const,
        item: c.name,
        label: name('rp.pin.chart', ar ? c.titleAr : c.titleEn),
      })),
      ...v.tiles.map((t) => ({
        key: 'TILE:' + t.name,
        kind: 'TILE' as const,
        item: t.name,
        label: name('rp.pin.tile', ar ? t.labelAr : t.labelEn),
      })),
      {
        key: 'TABLE',
        kind: 'TABLE' as const,
        item: null,
        label: this.i18n.translate('rp.pin.table'),
      },
    ];
  });
  protected readonly choice = signal(this.choices()[0]?.key ?? 'TABLE');
  protected readonly board = signal('');
  protected readonly newName = signal('');
  protected readonly keep = signal(true);
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  protected async save(): Promise<void> {
    const choice = this.choices().find((c) => c.key === this.choice());
    if (!choice) return;
    this.saving.set(true);
    this.errors.set([]);
    try {
      const view = await firstValueFrom(
        this.api.pin(this.board() || null, this.board() ? null : this.newName().trim(), {
          name: '',
          kind: choice.kind,
          report: this.data.view.code,
          item: choice.item,
          titleAr: null,
          titleEn: null,
          span: 0,
          rows: 10,
          bind: {},
          values: this.keep() ? cleanFilters(this.data.filters) : {},
        }),
      );
      this.ref.close(view);
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.saving.set(false);
    }
  }
}

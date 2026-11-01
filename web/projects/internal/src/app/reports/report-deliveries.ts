import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  I18nService,
  ReportDelivery,
  ReportsApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfigEditorData } from '../apps-control/config/config-editor';
import { REPORT_TRANSLATIONS } from './report-translations';

/** Badge modifier of a recipient's outcome. */
function outcomeBadge(outcome: string): string {
  return outcome === 'SENT' ? 'dc-badge--success' : outcome === 'FAILED' ? 'dc-badge--danger' : '';
}

/** Every run of the report schedules: when, which report, how many got it, and each recipient's outcome. */
@Component({
  selector: 'dc-report-deliveries',
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'rp.deliveries.title' | dcTranslate }}</h1>
      <p class="dc-muted">{{ 'rp.deliveries.subtitle' | dcTranslate }}</p>
    </header>
    <div class="dc-toolbar">
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>{{ 'rp.deliveries.schedule' | dcTranslate }}</mat-label>
        <input
          matInput
          dir="ltr"
          [ngModel]="schedule()"
          (ngModelChange)="schedule.set($event.toUpperCase()); skip.set(0)"
          data-testid="delivery-schedule"
        />
      </mat-form-field>
      <button matButton type="button" (click)="page.reload()">
        <mat-icon fontSet="material-symbols-outlined">refresh</mat-icon
        >{{ 'rp.deliveries.refresh' | dcTranslate }}
      </button>
    </div>
    <div class="rd-list" data-testid="deliveries">
      @for (d of page.value()?.items ?? []; track d.id) {
        <details class="rd-item" [attr.data-delivery]="d.scheduleCode">
          <summary>
            <span dir="ltr" class="rd-when">{{ d.dueAt | date: 'yyyy-MM-dd HH:mm' }}</span>
            <span
              ><b dir="ltr">{{ d.scheduleCode }}</b> ·
              <span dir="ltr">{{ d.reportCode }}</span></span
            >
            <span class="rd-counts">
              <span class="dc-badge dc-badge--success"
                >{{ 'rp.deliveries.sent' | dcTranslate }} {{ d.sent }}</span
              >
              <span class="dc-badge"
                >{{ 'rp.deliveries.skipped' | dcTranslate }} {{ d.skipped }}</span
              >
              @if (d.failed) {
                <span class="dc-badge dc-badge--danger"
                  >{{ 'rp.deliveries.failed' | dcTranslate }} {{ d.failed }}</span
                >
              }
            </span>
          </summary>
          @if (d.error) {
            <p class="dc-errors">{{ d.error }}</p>
          }
          <ul class="rd-recipients">
            @for (r of d.recipients; track r.userName) {
              <li>
                <span class="dc-badge" [class]="badge(r.outcome)">{{
                  'rp.outcome.' + r.outcome | dcTranslate
                }}</span>
                <span dir="ltr">{{ r.userName }}</span>
                @if (r.message) {
                  <span class="dc-muted">{{ r.message }}</span>
                }
              </li>
            }
          </ul>
        </details>
      }
    </div>
    @if ((page.value()?.items ?? []).length === 0 && !page.isLoading()) {
      <p class="dc-empty">{{ 'rp.deliveries.empty' | dcTranslate }}</p>
    }
    <mat-paginator
      [length]="page.value()?.total ?? 0"
      [pageSize]="take()"
      [pageSizeOptions]="[25, 50, 100]"
      (page)="paginate($event)"
    />
  `,
  styles: `
    .rd-list {
      display: grid;
      gap: 8px;
    }
    .rd-item {
      padding: 10px 14px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      background: #fff;
    }
    .rd-item summary {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      align-items: center;
      cursor: pointer;
      font-size: 13px;
    }
    .rd-when {
      color: var(--dga-text-3, #6c737f);
    }
    .rd-counts {
      display: inline-flex;
      gap: 6px;
      margin-inline-start: auto;
    }
    .rd-recipients {
      display: grid;
      gap: 6px;
      margin: 10px 0 0;
      padding: 0;
      list-style: none;
      font-size: 13px;
    }
    .rd-recipients li {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }
  `,
})
export class ReportDeliveries {
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(I18nService);
  protected readonly schedule = signal('');
  protected readonly skip = signal(0);
  protected readonly take = signal(50);
  protected readonly page = rxResource({
    params: () => ({
      schedule: this.schedule().trim() || undefined,
      skip: this.skip(),
      take: this.take(),
    }),
    stream: ({ params }) => this.api.deliveries(params.schedule, params.skip, params.take),
  });

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  protected badge(outcome: string): string {
    return outcomeBadge(outcome);
  }

  protected paginate(event: PageEvent): void {
    this.take.set(event.pageSize);
    this.skip.set(event.pageIndex * event.pageSize);
  }
}

/** Sends a schedule's report now (a row action of the schedules screen) and shows who got it. */
@Component({
  selector: 'dc-report-send-now',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ 'rp.sendNow' | dcTranslate }} — <span dir="ltr">{{ data.row?.code }}</span>
    </h2>
    <mat-dialog-content class="rs-body">
      @if (sending()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
      @if (result(); as d) {
        <p data-testid="send-now-result">{{ summary(d) }}</p>
        <ul class="rs-recipients">
          @for (r of d.recipients; track r.userName) {
            <li>
              <span class="dc-badge" [class]="badge(r.outcome)">{{
                'rp.outcome.' + r.outcome | dcTranslate
              }}</span>
              <span dir="ltr">{{ r.userName }}</span>
              @if (r.message) {
                <span class="dc-muted">{{ r.message }}</span>
              }
            </li>
          }
        </ul>
      } @else if (!sending()) {
        <p class="dc-muted">{{ 'rp.deliveries.subtitle' | dcTranslate }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      <button matButton mat-dialog-close>{{ 'rp.close' | dcTranslate }}</button>
      @if (!result()) {
        <button
          matButton="filled"
          type="button"
          (click)="send()"
          [disabled]="sending()"
          data-testid="send-now-confirm"
        >
          <mat-icon fontSet="material-symbols-outlined">send</mat-icon
          >{{ 'rp.sendNow' | dcTranslate }}
        </button>
      }
    </mat-dialog-actions>
  `,
  styles: `
    .rs-body {
      display: grid;
      gap: 10px;
    }
    .rs-recipients {
      display: grid;
      gap: 6px;
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: 13px;
    }
    .rs-recipients li {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      align-items: center;
    }
  `,
})
export class ReportSendNow {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(I18nService);
  protected readonly sending = signal(false);
  protected readonly result = signal<ReportDelivery | null>(null);
  protected readonly errors = signal<string[]>([]);

  constructor() {
    this.i18n.register(REPORT_TRANSLATIONS);
  }

  protected badge(outcome: string): string {
    return outcomeBadge(outcome);
  }

  protected summary(d: ReportDelivery): string {
    return this.i18n
      .translate('rp.sendNow.done')
      .replace('{sent}', String(d.sent))
      .replace('{skipped}', String(d.skipped))
      .replace('{failed}', String(d.failed));
  }

  protected async send(): Promise<void> {
    const code = this.data.row?.code;
    if (!code) return;
    this.sending.set(true);
    this.errors.set([]);
    try {
      this.result.set(await firstValueFrom(this.api.sendNow(code)));
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.sending.set(false);
    }
  }
}

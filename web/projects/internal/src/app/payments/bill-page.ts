import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import {
  BillCard,
  I18nService,
  LocalizePipe,
  PaymentsApi,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../apps-control/confirm-dialog';
import { PrivilegesService } from '../apps-control/privileges.service';

/** A bill: what it is for, its lines and history; with EDIT, record a payment made elsewhere or cancel it. */
@Component({
  selector: 'dc-bill-page',
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
    BillCard,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <a matButton routerLink="..">
        <mat-icon fontSet="material-symbols-outlined">{{
          i18n.direction() === 'rtl' ? 'arrow_forward' : 'arrow_back'
        }}</mat-icon>
        {{ 'bills.title' | dcTranslate }}
      </a>
    </header>
    @if (errors().length) {
      <ul class="dc-errors" role="alert">
        @for (e of errors(); track $index) {
          <li>{{ e }}</li>
        }
      </ul>
    }
    @if (detail.value(); as d) {
      <h1 dir="ltr" class="dc-billpage__title">{{ d.bill.number }}</h1>
      <p class="dc-muted">
        {{ { ar: d.descriptionAr, en: d.descriptionEn } | dcLocalize }} · {{ d.bill.payerName }}
        @if (d.bill.subjectType === 'SERVICE_REQUEST') {
          ·
          <a class="dc-link" [routerLink]="['/services/requests', d.bill.subjectId]" dir="ltr">{{
            d.bill.subjectNumber
          }}</a>
        }
      </p>
      <dc-bill-card [bill]="d.info" />
      @if (canEdit() && d.bill.status === 'PENDING') {
        <div class="dc-billpage__actions">
          <button
            matButton="outlined"
            type="button"
            (click)="markPaid()"
            data-testid="bill-mark-paid"
          >
            <mat-icon fontSet="material-symbols-outlined">price_check</mat-icon>
            {{ 'bills.markPaid' | dcTranslate }}
          </button>
          <button
            matButton="outlined"
            type="button"
            class="dc-danger"
            (click)="cancel()"
            data-testid="bill-cancel"
          >
            <mat-icon fontSet="material-symbols-outlined">block</mat-icon>
            {{ 'bills.cancel' | dcTranslate }}
          </button>
        </div>
      }
      <section class="dc-card">
        <h2>{{ 'bills.history' | dcTranslate }}</h2>
        <ol class="dc-timeline">
          @for (e of d.events; track $index) {
            <li>
              <strong>{{ 'billEvent.' + e.type | dcTranslate }}</strong>
              <span class="dc-muted dc-timeline__meta"
                ><bdi dir="ltr">{{ e.at | date: 'yyyy-MM-dd HH:mm' }}</bdi> ·
                <bdi>{{ e.actor }}</bdi></span
              >
              @if (e.comment) {
                <p>{{ e.comment }}</p>
              }
            </li>
          }
        </ol>
        <p class="dc-muted" dir="ltr">
          {{ 'bills.net' | dcTranslate }} {{ d.net | number: '1.2-2' }} ·
          {{ 'bills.vat' | dcTranslate }} {{ d.vat | number: '1.2-2' }}
        </p>
      </section>
    } @else if (detail.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: 1rem;
    }
    .dc-billpage__title {
      margin: 0;
    }
    .dc-billpage__actions {
      display: flex;
      gap: 0.5rem;
    }
    .dc-danger {
      color: #b42318;
    }
    .dc-timeline {
      display: grid;
      gap: 0.75rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-timeline li {
      padding-inline-start: 0.75rem;
      border-inline-start: 3px solid var(--dga-border, #d2d6db);
    }
    .dc-timeline__meta {
      display: block;
      font-size: 13px;
    }
    .dc-timeline p {
      margin: 0.25rem 0 0;
    }
  `,
})
export class BillPage {
  readonly id = input.required<string>();
  private readonly api = inject(PaymentsApi);
  private readonly dialog = inject(MatDialog);
  protected readonly i18n = inject(I18nService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly detail = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.get(params),
  });
  protected readonly canEdit = computed(() => this.privileges.can('payments.bills', 'EDIT'));
  protected readonly errors = signal<string[]>([]);

  protected async markPaid(): Promise<void> {
    const d = this.detail.value();
    if (!d) return;
    const reference = await this.ask('bills.markPaid', 'bills.markPaidHint', false);
    if (!reference) return;
    await this.run(() => firstValueFrom(this.api.markPaid(d.bill.id, reference, null)));
  }

  protected async cancel(): Promise<void> {
    const d = this.detail.value();
    if (!d) return;
    const reason = await this.ask('bills.cancel', 'bills.cancelHint', true);
    if (!reason) return;
    await this.run(() => firstValueFrom(this.api.cancel(d.bill.id, reason)));
  }

  private ask(label: string, hint: string, danger: boolean): Promise<string | undefined> {
    return firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, string>(ConfirmDialog, {
          data: {
            title: this.i18n.translate(label),
            message: this.i18n.translate(hint),
            confirmLabel: this.i18n.translate(label),
            comment: true,
            commentRequired: true,
            danger,
          },
        })
        .afterClosed(),
    );
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.errors.set([]);
    try {
      await action();
      this.detail.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

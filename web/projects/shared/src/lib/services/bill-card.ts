import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { BillInfo } from './service.models';

/**
 * A bill of a request: while unpaid, the amount, the number to pay with, the biller code, the due date, how to pay and
 * the provider's payment page; once paid, the receipt with its date, reference and lines.
 */
@Component({
  selector: 'dc-bill-card',
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let b = bill();
    <section class="dc-bill" [class.dc-bill--due]="b.status === 'PENDING'" [attr.data-testid]="'bill-' + b.number">
      <header class="dc-bill__head">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ b.status === 'PAID' ? 'receipt_long' : 'payments' }}</mat-icon>
        <h2>{{ (b.status === 'PAID' ? 'bill.paidTitle' : b.status === 'PENDING' ? 'bill.due' : 'bill.title') | dcTranslate }}</h2>
        <span class="dc-bill__status" data-testid="bill-status">{{ 'bill.status.' + b.status | dcTranslate }}</span>
      </header>
      <dl class="dc-bill__facts">
        <div>
          <dt>{{ 'bill.amount' | dcTranslate }}</dt>
          <dd class="dc-bill__amount" dir="ltr" data-testid="bill-amount">{{ b.total | number: '1.2-2' }} {{ 'fees.currency' | dcTranslate }}</dd>
        </div>
        @if (b.providerBillNumber) {
          <div>
            <dt>{{ 'bill.number' | dcTranslate }}</dt>
            <dd dir="ltr" data-testid="bill-provider-number">{{ b.providerBillNumber }}</dd>
          </div>
        }
        @if (b.billerCode) {
          <div>
            <dt>{{ 'bill.biller' | dcTranslate }}</dt>
            <dd dir="ltr">{{ b.billerCode }}</dd>
          </div>
        }
        @if (b.status === 'PAID') {
          <div>
            <dt>{{ 'bill.paidOn' | dcTranslate }}</dt>
            <dd dir="ltr">{{ b.paidAt | date: 'yyyy-MM-dd HH:mm' }}</dd>
          </div>
          <div>
            <dt>{{ 'bill.reference' | dcTranslate }}</dt>
            <dd dir="ltr" data-testid="bill-reference">{{ b.paymentReference }}</dd>
          </div>
        } @else {
          <div>
            <dt>{{ 'bill.dueOn' | dcTranslate }}</dt>
            <dd dir="ltr">{{ b.dueAt | date: 'yyyy-MM-dd' }}</dd>
          </div>
        }
        <div>
          <dt>{{ 'bill.platformNumber' | dcTranslate }}</dt>
          <dd dir="ltr">{{ b.number }}</dd>
        </div>
      </dl>
      @if (b.status === 'PENDING') {
        @if (b.instructionsAr || b.instructionsEn) {
          <p class="dc-bill__instructions">{{ { ar: b.instructionsAr ?? '', en: b.instructionsEn ?? '' } | dcLocalize }}</p>
        }
        @if (b.payUrl; as url) {
          <a matButton="filled" [href]="url" target="_blank" rel="noopener" data-testid="bill-pay">
            <mat-icon fontSet="material-symbols-outlined">open_in_new</mat-icon>
            {{ 'bill.payNow' | dcTranslate }}
          </a>
        }
      }
      @if (b.lines.length > 1 || b.status === 'PAID') {
        <table class="dc-bill__lines">
          <tbody>
            @for (line of b.lines; track line.code) {
              <tr>
                <td>{{ { ar: line.nameAr, en: line.nameEn } | dcLocalize }}</td>
                <td dir="ltr">{{ line.total | number: '1.2-2' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
  styles: `
    .dc-bill {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 20px 24px;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 12px;
      background: #ffffff;
    }
    .dc-bill--due {
      border-color: #f79009;
      background: #fffcf5;
    }
    .dc-bill__head {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .dc-bill__head h2 {
      flex: 1;
      margin: 0;
      font-size: 18px;
    }
    .dc-bill__status {
      padding: 2px 10px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 13px;
      font-weight: 600;
    }
    .dc-bill--due .dc-bill__status {
      background: #fef0c7;
      color: #93370d;
    }
    .dc-bill__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 12px 24px;
      margin: 0;
    }
    .dc-bill__facts dt {
      color: var(--dga-text-3, #384250);
      font-size: 13px;
    }
    .dc-bill__facts dd {
      margin: 2px 0 0;
      font-weight: 600;
    }
    .dc-bill__amount {
      font-size: 20px;
    }
    .dc-bill__instructions {
      margin: 0;
      color: var(--dga-text-2, #1f2a37);
    }
    .dc-bill__lines {
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
    }
    .dc-bill__lines td {
      padding: 6px 0;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
    }
    .dc-bill__lines td:last-child {
      text-align: end;
    }
  `,
})
export class BillCard {
  readonly bill = input.required<BillInfo>();
}

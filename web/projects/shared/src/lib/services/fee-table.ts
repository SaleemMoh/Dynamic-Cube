import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { FeeLine } from './service.models';

/** Fee lines with VAT and the total, in SAR. */
@Component({
  selector: 'dc-fee-table',
  imports: [DecimalPipe, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (lines().length) {
      <table class="dc-fees" data-testid="fee-table">
        <thead>
          <tr>
            <th>{{ 'fees.item' | dcTranslate }}</th>
            <th>{{ 'fees.net' | dcTranslate }}</th>
            <th>{{ 'fees.vat' | dcTranslate }}</th>
            <th>{{ 'fees.total' | dcTranslate }}</th>
          </tr>
        </thead>
        <tbody>
          @for (line of lines(); track line.code) {
            <tr>
              <td>{{ { ar: line.nameAr, en: line.nameEn } | dcLocalize }}@if (line.isEstimate) { <span class="dc-muted"> ({{ 'fees.estimate' | dcTranslate }})</span> }</td>
              <td dir="ltr">{{ line.net | number: '1.2-2' }}</td>
              <td dir="ltr">{{ line.vat | number: '1.2-2' }}</td>
              <td dir="ltr">{{ line.total | number: '1.2-2' }}</td>
            </tr>
          }
        </tbody>
        <tfoot>
          <tr>
            <th colspan="3">{{ 'fees.grandTotal' | dcTranslate }}</th>
            <th dir="ltr" data-testid="fee-total">{{ total() | number: '1.2-2' }} {{ 'fees.currency' | dcTranslate }}</th>
          </tr>
        </tfoot>
      </table>
    } @else {
      <p class="dc-muted" data-testid="no-fees">{{ 'fees.none' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-fees {
      width: 100%;
      border-collapse: collapse;
    }
    .dc-fees th,
    .dc-fees td {
      padding: 10px 12px;
      border-bottom: 1px solid var(--dga-border, #d2d6db);
      text-align: start;
    }
    .dc-fees thead th {
      background: var(--dga-gray-50, #f9fafb);
      font-weight: 500;
    }
    .dc-fees tfoot th {
      font-weight: 700;
    }
  `,
})
export class FeeTable {
  readonly lines = input.required<FeeLine[]>();
  protected readonly total = computed(() => this.lines().reduce((sum, l) => sum + l.total, 0));
}

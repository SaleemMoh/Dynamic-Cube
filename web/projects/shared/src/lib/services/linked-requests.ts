import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { I18nService } from '../i18n/i18n.service';
import { TranslationDictionary } from '../i18n/translations';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { LinkedRequest } from './service.models';

const LINKED_REQUEST_TRANSLATIONS: TranslationDictionary = {
  ar: {
    'links.title': 'الطلبات المرتبطة',
    'links.relation.PARENT': 'نشأ من الطلب',
    'links.relation.BUNDLED': 'طلب مرافق',
    'links.relation.SPAWNED': 'طلب فرعي',
    'links.draft': 'مسودة لاستكمالها',
    'links.status.DRAFT': 'مسودة',
    'links.status.SUBMITTED': 'مقدم',
    'links.status.IN_PROGRESS': 'قيد المعالجة',
    'links.status.PENDING_CUSTOMER': 'بانتظارك',
    'links.status.PENDING_EXTERNAL': 'بانتظار طلب آخر',
    'links.status.PENDING_PAYMENT': 'بانتظار السداد',
    'links.status.APPROVED': 'معتمد',
    'links.status.REJECTED': 'مرفوض',
    'links.status.CANCELLED': 'ملغى',
    'links.status.CLOSED': 'مغلق',
  },
  en: {
    'links.title': 'Linked requests',
    'links.relation.PARENT': 'Made from request',
    'links.relation.BUNDLED': 'Bundled request',
    'links.relation.SPAWNED': 'Sub-request',
    'links.draft': 'Draft to complete',
    'links.status.DRAFT': 'Draft',
    'links.status.SUBMITTED': 'Submitted',
    'links.status.IN_PROGRESS': 'In progress',
    'links.status.PENDING_CUSTOMER': 'Waiting for you',
    'links.status.PENDING_EXTERNAL': 'Waiting for another request',
    'links.status.PENDING_PAYMENT': 'Awaiting payment',
    'links.status.APPROVED': 'Approved',
    'links.status.REJECTED': 'Rejected',
    'links.status.CANCELLED': 'Cancelled',
    'links.status.CLOSED': 'Closed',
  },
};

/**
 * The requests linked to a request (3f): the one it was made from, and those bundled with it or made by its workflow's
 * steps, each with its status and a link. Shown to the applicant and on the internal request page.
 */
@Component({
  selector: 'dc-linked-requests',
  imports: [MatIconModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (links().length) {
      <section class="dc-links" data-testid="linked-requests">
        <h2>
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">account_tree</mat-icon
          >{{ 'links.title' | dcTranslate }}
        </h2>
        <ul>
          @for (l of links(); track l.id) {
            <li [attr.data-link]="l.relation">
              <span class="dc-links__relation">{{
                'links.relation.' + l.relation | dcTranslate
              }}</span>
              <a [routerLink]="[base(), l.id]">
                {{ { ar: l.serviceNameAr, en: l.serviceNameEn } | dcLocalize }}
                @if (l.number) {
                  <bdi dir="ltr" class="dc-links__number">{{ l.number }}</bdi>
                }
              </a>
              <span
                class="dc-links__status"
                [class.dc-links__status--todo]="
                  l.status === 'DRAFT' || l.status === 'PENDING_CUSTOMER'
                "
              >
                {{
                  (l.status === 'DRAFT' ? 'links.draft' : 'links.status.' + l.status) | dcTranslate
                }}
              </span>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    .dc-links {
      padding: 12px 16px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
      background: var(--dga-white, #fff);
    }
    .dc-links h2 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 8px;
      font-size: 15px;
    }
    .dc-links ul {
      display: grid;
      gap: 6px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-links li {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 8px;
      font-size: 14px;
    }
    .dc-links__relation {
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-links__number {
      margin-inline-start: 4px;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
    .dc-links__status {
      margin-inline-start: auto;
      padding: 1px 8px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 12px;
    }
    .dc-links__status--todo {
      background: #fef0c7;
      color: #93370d;
    }
  `,
})
export class LinkedRequests {
  readonly links = input<LinkedRequest[]>([]);
  /** Where a request opens, e.g. <c>/requests</c> on the portal. */
  readonly base = input('/requests');

  constructor() {
    inject(I18nService).register(LINKED_REQUEST_TRANSLATIONS);
  }
}

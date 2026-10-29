import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { VisitInfo } from './service.models';

/**
 * A visit of a request: its type, where, when it is scheduled (or due) and, once done, its outcome. Shown to the
 * applicant and on the internal request page.
 */
@Component({
  selector: 'dc-visit-card',
  imports: [DatePipe, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let v = visit();
    <section
      class="dc-visit"
      [class.dc-visit--open]="v.status !== 'COMPLETED' && v.status !== 'CANCELLED'"
      [class.dc-visit--pass]="v.outcomeKind === 'PASS'"
      [class.dc-visit--fail]="v.outcomeKind === 'FAIL'"
      [attr.data-testid]="'visit-' + v.number"
    >
      <header class="dc-visit__head">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ v.status === 'COMPLETED' ? 'fact_check' : 'location_on' }}</mat-icon>
        <h2>{{ { ar: v.typeNameAr, en: v.typeNameEn } | dcLocalize }}</h2>
        <span class="dc-visit__status" data-testid="visit-status">{{ 'visit.status.' + v.status | dcTranslate }}</span>
      </header>
      <dl class="dc-visit__facts">
        @if (v.status === 'COMPLETED') {
          <div>
            <dt>{{ 'visit.outcome' | dcTranslate }}</dt>
            <dd data-testid="visit-outcome">{{ { ar: v.outcomeNameAr ?? v.outcome ?? '', en: v.outcomeNameEn ?? v.outcome ?? '' } | dcLocalize }}</dd>
          </div>
          <div>
            <dt>{{ 'visit.completedOn' | dcTranslate }}</dt>
            <dd dir="ltr">{{ v.completedAt | date: 'yyyy-MM-dd' }}</dd>
          </div>
        } @else if (v.scheduledAt) {
          <div>
            <dt>{{ 'visit.scheduledOn' | dcTranslate }}</dt>
            <dd dir="ltr" data-testid="visit-scheduled">{{ v.scheduledAt | date: 'yyyy-MM-dd HH:mm' }}</dd>
          </div>
        } @else if (v.dueAt && v.status !== 'CANCELLED') {
          <div>
            <dt>{{ 'visit.dueBy' | dcTranslate }}</dt>
            <dd dir="ltr">{{ v.dueAt | date: 'yyyy-MM-dd' }}</dd>
          </div>
        }
        @if (v.siteAddress) {
          <div>
            <dt>{{ 'visit.site' | dcTranslate }}</dt>
            <dd>{{ v.siteAddress }}</dd>
          </div>
        }
        <div>
          <dt>{{ 'visit.number' | dcTranslate }}</dt>
          <dd dir="ltr">{{ v.number }}</dd>
        </div>
      </dl>
      @if (v.status === 'PENDING' || v.status === 'SENT') {
        <p class="dc-visit__hint">{{ (v.mode === 'EXTERNAL' ? 'visit.hintExternal' : 'visit.hintPending') | dcTranslate }}</p>
      }
    </section>
  `,
  styles: `
    .dc-visit {
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 20px 24px;
      border: 1px solid var(--dga-border, #d2d6db);
      border-radius: 12px;
      background: #ffffff;
    }
    .dc-visit--open {
      border-color: #84caff;
      background: #f5faff;
    }
    .dc-visit--pass {
      border-color: #75e0a7;
    }
    .dc-visit--fail {
      border-color: #fda29b;
    }
    .dc-visit__head {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .dc-visit__head h2 {
      flex: 1;
      margin: 0;
      font-size: 18px;
    }
    .dc-visit__status {
      padding: 2px 10px;
      border-radius: 999px;
      background: var(--dga-gray-100, #f3f4f6);
      font-size: 13px;
      font-weight: 600;
    }
    .dc-visit--open .dc-visit__status {
      background: #d1e9ff;
      color: #1849a9;
    }
    .dc-visit__facts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 12px 24px;
      margin: 0;
    }
    .dc-visit__facts dt {
      color: var(--dga-text-3, #384250);
      font-size: 13px;
    }
    .dc-visit__facts dd {
      margin: 2px 0 0;
      font-weight: 600;
    }
    .dc-visit__hint {
      margin: 0;
      color: var(--dga-text-2, #1f2a37);
    }
  `,
})
export class VisitCard {
  readonly visit = input.required<VisitInfo>();
}

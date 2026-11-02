import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '../i18n/translate.pipe';
import { ItemResult } from './service.models';

/** The items of a request decided one by one (e.g. each sector), with the reason for each rejection. */
@Component({
  selector: 'dc-item-results',
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length) {
      <section class="dc-item-results" data-testid="item-results">
        <h2>{{ 'itemResults.title' | dcTranslate }}</h2>
        <p class="dc-item-results__count">
          {{ 'itemResults.approved' | dcTranslate }}: <strong>{{ approved() }}</strong> ·
          {{ 'itemResults.rejected' | dcTranslate }}:
          <strong>{{ items().length - approved() }}</strong>
        </p>
        <ul>
          @for (i of items(); track i.step + i.key) {
            <li
              [class.dc-item-results--no]="i.decision === 'REJECTED'"
              [attr.data-testid]="'item-result-' + i.key"
            >
              <mat-icon aria-hidden="true">{{
                i.decision === 'APPROVED' ? 'check_circle' : 'cancel'
              }}</mat-icon>
              <div>
                <strong dir="auto">{{ i.label }}</strong>
                <span class="dc-item-results__verdict">{{
                  'itemResults.' + i.decision | dcTranslate
                }}</span>
                @if (i.reason) {
                  <p dir="auto">{{ i.reason }}</p>
                }
              </div>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    .dc-item-results h2 {
      margin: 0 0 4px;
      font-size: 18px;
    }
    .dc-item-results__count {
      margin: 0 0 12px;
      color: var(--dga-gray-600, #4d5761);
    }
    .dc-item-results ul {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .dc-item-results li {
      display: flex;
      gap: 10px;
      align-items: flex-start;
      padding: 10px 12px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 8px;
    }
    .dc-item-results li mat-icon {
      flex: none;
      color: var(--dga-success-600, #079455);
    }
    .dc-item-results li.dc-item-results--no mat-icon {
      color: var(--dga-error-600, #d92d20);
    }
    .dc-item-results li p {
      margin: 4px 0 0;
      color: var(--dga-gray-700, #384250);
    }
    .dc-item-results__verdict {
      margin-inline-start: 8px;
      font-size: 13px;
      color: var(--dga-gray-600, #4d5761);
    }
  `,
})
export class ItemResults {
  readonly items = input<ItemResult[]>([]);
  protected readonly approved = computed(
    () => this.items().filter((i) => i.decision === 'APPROVED').length,
  );
}

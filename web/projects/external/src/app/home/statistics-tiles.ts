import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from '../portal/portal-store';

/** The portal statistics as tiles: counted metrics or fixed values, as the portal settings list them. */
@Component({
  selector: 'dc-statistics-tiles',
  imports: [MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-stats" aria-labelledby="dc-stats-title">
      <h2 id="dc-stats-title">{{ 'portal.statistics' | dcTranslate }}</h2>
      <ul class="dc-stats__tiles">
        @for (tile of store.tiles(); track $index) {
          <li class="dc-stats__tile" [attr.data-testid]="'stat-' + (tile.metric ?? $index)">
            <span class="dc-stats__icon" aria-hidden="true"><mat-icon fontSet="material-symbols-outlined">{{ tile.icon || 'insights' }}</mat-icon></span>
            <span class="dc-stats__value" dir="ltr">{{ format(store.tileValue(tile)) }}</span>
            <span class="dc-stats__label">{{ { ar: tile.labelAr, en: tile.labelEn } | dcLocalize }}</span>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-stats h2 {
      margin: 0;
      font-size: 26px;
      font-weight: 700;
      line-height: 36px;
    }
    .dc-stats__tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(13rem, 15rem));
      justify-content: center;
      gap: 24px;
      margin: 24px 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-stats__tile {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 16px;
      border-radius: 16px;
      background: #ffffff;
      box-shadow: 0 1px 2px rgb(16 24 40 / 4%);
      text-align: center;
    }
    .dc-stats__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 48px;
      margin-bottom: 8px;
      border-radius: 50%;
      background: #ecfdf3;
      color: var(--dga-green-700);
    }
    .dc-stats__icon mat-icon {
      font-size: 26px;
      width: 32px;
      height: 24px;
    }
    .dc-stats__value {
      color: var(--dga-green-800);
      font-size: 40px;
      font-weight: 500;
      line-height: 50px;
    }
    .dc-stats__label {
      color: var(--dga-text);
      font-size: 18px;
    }
  `,
})
export class StatisticsTiles {
  protected readonly store = inject(PortalStore);
  private readonly numbers = new Intl.NumberFormat('en-US');

  protected format(value: number | null): string {
    return value === null ? '—' : this.numbers.format(value);
  }
}

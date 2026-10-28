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
    .dc-stats {
      padding: 32px;
      border-radius: 24px;
      background: var(--dga-green-50);
    }
    .dc-stats__tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
      gap: 16px;
      margin: 20px 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-stats__tile {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 20px 24px;
      border-radius: 16px;
      background: #ffffff;
      box-shadow: 0 1px 3px rgb(16 24 40 / 6%);
    }
    .dc-stats__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      margin-bottom: 8px;
      border-radius: 10px;
      background: var(--dga-green-700);
      color: #ffffff;
    }
    .dc-stats__value {
      align-self: flex-start;
      color: var(--dga-green-950);
      font-size: 32px;
      font-weight: 700;
      line-height: 40px;
    }
    .dc-stats__label {
      color: var(--dga-text-3);
    }

    .dc-stats h2 {
      margin: 0;
      font-size: 28px;
      font-weight: 700;
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

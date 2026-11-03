import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { PageHero } from '../portal/page-hero';
import { CalculatorsApi } from './calculators-api';

/** The portal's public calculators (estimates anyone can work out before applying). */
@Component({
  selector: 'dc-calculator-list',
  imports: [MatIconModule, RouterLink, LocalizePipe, TranslatePipe, PageHero],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero
      [title]="'calculators.title' | dcTranslate"
      [text]="'calculators.lead' | dcTranslate"
    />
    <div class="dc-portal-block dc-page-body">
      <div class="dc-calcs">
        @for (c of calculators.value() ?? []; track c.code) {
          <a
            class="dc-calcs__card"
            [routerLink]="['/calculators', c.code]"
            [attr.data-testid]="'calculator-' + c.code"
          >
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
              c.icon || 'calculate'
            }}</mat-icon>
            <strong>{{ { ar: c.titleAr, en: c.titleEn } | dcLocalize }}</strong>
            @if (c.introAr || c.introEn) {
              <span>{{ { ar: c.introAr ?? '', en: c.introEn ?? '' } | dcLocalize }}</span>
            }
          </a>
        } @empty {
          @if (!calculators.isLoading()) {
            <p class="dc-empty">{{ 'calculators.none' | dcTranslate }}</p>
          }
        }
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-calcs {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
      gap: 16px;
    }
    .dc-calcs__card {
      display: grid;
      gap: 8px;
      align-content: start;
      padding: 20px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: #fff;
      color: inherit;
      text-decoration: none;
    }
    .dc-calcs__card:hover {
      border-color: var(--dga-primary-600, #1b8354);
    }
    .dc-calcs__card mat-icon {
      color: var(--dga-primary-600, #1b8354);
    }
    .dc-calcs__card strong {
      font-size: 18px;
    }
    .dc-calcs__card span {
      color: var(--dga-gray-600, #4d5761);
    }
  `,
})
export class CalculatorList {
  private readonly api = inject(CalculatorsApi);
  protected readonly calculators = rxResource({ stream: () => this.api.list() });
}

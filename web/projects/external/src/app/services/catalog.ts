import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ActingPartyService, LocalizePipe, ServicesApi, TranslatePipe } from '@dynamic-cube/shared';

/** Services the acting party can request, with the reasons when one is not available to them. */
@Component({
  selector: 'dc-catalog',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ 'catalog.title' | dcTranslate }}</h1>
    @if (acting.current(); as current) {
      <p class="dc-muted">{{ 'catalog.actingAs' | dcTranslate }} <strong>{{ { ar: current.nameAr, en: current.nameEn } | dcLocalize }}</strong></p>
    }
    @if (catalog.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <ul class="dc-catalog">
      @for (s of catalog.value() ?? []; track s.code) {
        <li class="dc-card dc-catalog__item" [attr.data-testid]="'service-' + s.code">
          <div class="dc-catalog__head">
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ s.icon || 'description' }}</mat-icon>
            <h2>{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }}</h2>
          </div>
          @if (s.descriptionAr || s.descriptionEn) {
            <p>{{ { ar: s.descriptionAr ?? '', en: s.descriptionEn ?? '' } | dcLocalize }}</p>
          }
          @if (s.isEligible) {
            <span class="dc-badge dc-badge--success">{{ 'catalog.available' | dcTranslate }}</span>
          } @else {
            <span class="dc-badge dc-badge--warning">{{ 'catalog.unavailable' | dcTranslate }}</span>
            <ul class="dc-catalog__reasons">
              @for (r of s.reasons; track r.code) {
                <li>{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</li>
              }
            </ul>
          }
          <a matButton="filled" [routerLink]="['/services', s.code]" [attr.data-testid]="'open-' + s.code">{{ 'catalog.details' | dcTranslate }}</a>
        </li>
      } @empty {
        @if (!catalog.isLoading()) {
          <li class="dc-muted">{{ 'catalog.empty' | dcTranslate }}</li>
        }
      }
    </ul>
  `,
  styles: `
    .dc-catalog {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
      gap: 1rem;
      margin: 1rem 0 0;
      padding: 0;
      list-style: none;
    }
    .dc-catalog__item {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.75rem;
    }
    .dc-catalog__head {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .dc-catalog__head h2 {
      margin: 0;
      font-size: 18px;
    }
    .dc-catalog__reasons {
      margin: 0;
      padding-inline-start: 1.25rem;
      color: #b54708;
    }
  `,
})
export class Catalog {
  protected readonly acting = inject(ActingPartyService);
  private readonly api = inject(ServicesApi);
  protected readonly catalog = rxResource({ params: () => this.acting.current()?.partyId ?? 'none', stream: () => this.api.catalog() });
}

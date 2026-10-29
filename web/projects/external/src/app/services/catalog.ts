import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { ActingPartyService, AuthService, I18nService, LocalizePipe, ServicesApi, TranslatePipe } from '@dynamic-cube/shared';
import { of } from 'rxjs';
import { PortalStore } from '../portal/portal-store';
import { PageHero } from '../portal/page-hero';
import { ServiceTile } from '../portal/service-tile';

/**
 * All services, searchable and filtered by category (the home page's search and "View all" land here). Anyone may
 * browse; for a signed-in applicant each service also says whether the acting party can use it now.
 */
@Component({
  selector: 'dc-catalog',
  imports: [FormsModule, MatIconModule, MatProgressBarModule, LocalizePipe, TranslatePipe, PageHero, ServiceTile],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero [title]="'portal.services' | dcTranslate" [text]="'catalog.lead' | dcTranslate" />
    <div class="dc-portal-block dc-page-body">
      <form class="dc-dga-search" role="search" (ngSubmit)="sync()">
        <label class="dc-dga-search__field">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">search</mat-icon>
          <input
            type="search"
            name="q"
            [ngModel]="text()"
            (ngModelChange)="text.set($event); sync()"
            [placeholder]="'portal.searchPlaceholder' | dcTranslate"
            [attr.aria-label]="'portal.searchLabel' | dcTranslate"
            data-testid="catalog-search"
          />
        </label>
        <button type="submit" class="dc-dark-button">{{ 'portal.search' | dcTranslate }}</button>
      </form>

      <div class="dc-utabs dc-catalog__tabs" role="tablist" [attr.aria-label]="'portal.categories' | dcTranslate">
        <button role="tab" class="dc-utabs__tab" [class.dc-utabs__tab--active]="!chosen()" [attr.aria-selected]="!chosen()" (click)="choose('')">
          {{ 'portal.all' | dcTranslate }}
        </button>
        @for (c of store.categories(); track c.code) {
          <button
            role="tab"
            class="dc-utabs__tab"
            [class.dc-utabs__tab--active]="chosen() === c.code"
            [attr.aria-selected]="chosen() === c.code"
            (click)="choose(c.code)"
            [attr.data-testid]="'filter-' + c.code"
          >
            {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
          </button>
        }
      </div>

      <div class="dc-section-head">
        <div>
          <h2>{{ sectionTitle() }}</h2>
          <p data-testid="catalog-count">
            {{ shown().length }} {{ 'portal.servicesFound' | dcTranslate }}
            @if (acting.current(); as current) {
              · {{ 'catalog.actingAs' | dcTranslate }} <strong>{{ { ar: current.nameAr, en: current.nameEn } | dcLocalize }}</strong>
            }
          </p>
        </div>
      </div>

      @if (store.catalog.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <ul class="dc-grid">
        @for (s of shown(); track s.code) {
          <li [attr.data-testid]="'service-' + s.code"><dc-service-tile [service]="s" [note]="reasons()[s.code] ?? null" /></li>
        } @empty {
          @if (!store.catalog.isLoading()) {
            <li class="dc-muted">{{ 'portal.noMatch' | dcTranslate }}</li>
          }
        }
      </ul>
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-catalog__tabs {
      align-self: flex-start;
      max-width: 100%;
    }
    .dc-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
      gap: 24px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
  `,
})
export class Catalog {
  /** Query parameters: the search text and the category. */
  readonly q = input<string>();
  readonly category = input<string>();

  protected readonly store = inject(PortalStore);
  protected readonly acting = inject(ActingPartyService);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  private readonly api = inject(ServicesApi);

  protected readonly text = linkedSignal(() => this.q() ?? '');
  protected readonly chosen = linkedSignal(() => this.category() ?? '');

  /** For a signed-in applicant: the services the acting party cannot use now, with the reason. */
  private readonly eligibility = rxResource({
    params: () => (this.auth.isAuthenticated() ? (this.acting.current()?.partyId ?? 'self') : null),
    stream: ({ params }) => (params ? this.api.catalog() : of([])),
  });
  protected readonly reasons = computed(() => {
    const reasons: Record<string, string> = {};
    for (const entry of this.eligibility.value() ?? []) {
      if (!entry.isEligible) reasons[entry.code] = entry.reasons.map((r) => this.i18n.localize({ ar: r.messageAr, en: r.messageEn })).join(' · ');
    }
    return reasons;
  });

  protected readonly shown = computed(() => {
    const words = this.text().trim().toLowerCase();
    const category = this.chosen();
    return this.store.services().filter(
      (s) =>
        (!category || s.categoryCode === category) &&
        (!words || [s.nameAr, s.nameEn, s.descriptionAr ?? '', s.descriptionEn ?? '', s.code].some((v) => v.toLowerCase().includes(words))),
    );
  });

  protected readonly sectionTitle = computed(() => {
    const category = this.store.category(this.chosen());
    return category
      ? this.i18n.translate('portal.servicesOf').replace('{name}', this.i18n.localize({ ar: category.nameAr, en: category.nameEn }))
      : this.i18n.translate('portal.allServices');
  });

  protected choose(code: string): void {
    this.chosen.set(code);
    this.sync();
  }

  /** Keeps the address in step with the filters, so a search can be shared or bookmarked. */
  protected sync(): void {
    const queryParams = { q: this.text().trim() || null, category: this.chosen() || null };
    void this.router.navigate([], { queryParams, replaceUrl: true });
  }
}

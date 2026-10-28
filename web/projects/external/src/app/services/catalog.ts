import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { ActingPartyService, AuthService, I18nService, LocalizePipe, ServicesApi, TranslatePipe } from '@dynamic-cube/shared';
import { of } from 'rxjs';
import { PortalStore } from '../portal/portal-store';
import { ServiceTile } from '../portal/service-tile';

/**
 * All services, searchable and filtered by category (the home page's search and "View all" land here). Anyone may
 * browse; for a signed-in applicant each service also says whether the acting party can use it now.
 */
@Component({
  selector: 'dc-catalog',
  imports: [FormsModule, MatIconModule, MatProgressBarModule, RouterLink, LocalizePipe, TranslatePipe, ServiceTile],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
      <a routerLink="/">{{ 'portal.home' | dcTranslate }}</a>
      <span aria-hidden="true">/</span>
      <span>{{ 'portal.services' | dcTranslate }}</span>
    </nav>
    <h1>{{ 'portal.services' | dcTranslate }}</h1>
    @if (acting.current(); as current) {
      <p class="dc-muted">{{ 'catalog.actingAs' | dcTranslate }} <strong>{{ { ar: current.nameAr, en: current.nameEn } | dcLocalize }}</strong></p>
    }

    <div class="dc-filters">
      <label class="dc-filters__search">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">search</mat-icon>
        <input
          type="search"
          [ngModel]="text()"
          (ngModelChange)="text.set($event); sync()"
          [placeholder]="'portal.searchPlaceholder' | dcTranslate"
          [attr.aria-label]="'portal.searchLabel' | dcTranslate"
          data-testid="catalog-search"
        />
      </label>
      <div class="dc-filters__tabs" role="tablist" [attr.aria-label]="'portal.categories' | dcTranslate">
        <button role="tab" class="dc-chip" [class.dc-chip--active]="!chosen()" [attr.aria-selected]="!chosen()" (click)="choose('')">{{ 'portal.all' | dcTranslate }}</button>
        @for (c of store.categories(); track c.code) {
          <button
            role="tab"
            class="dc-chip"
            [class.dc-chip--active]="chosen() === c.code"
            [attr.aria-selected]="chosen() === c.code"
            (click)="choose(c.code)"
            [attr.data-testid]="'filter-' + c.code"
          >
            {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
          </button>
        }
      </div>
    </div>

    @if (store.catalog.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    <p class="dc-muted" data-testid="catalog-count">{{ shown().length }} {{ 'portal.servicesFound' | dcTranslate }}</p>
    <ul class="dc-grid">
      @for (s of shown(); track s.code) {
        <li [attr.data-testid]="'service-' + s.code"><dc-service-tile [service]="s" [note]="reasons()[s.code] ?? null" /></li>
      } @empty {
        @if (!store.catalog.isLoading()) {
          <li class="dc-muted">{{ 'portal.noMatch' | dcTranslate }}</li>
        }
      }
    </ul>
  `,
  styles: `
    .dc-crumbs {
      display: flex;
      gap: 8px;
      color: var(--dga-text-3);
      font-size: 14px;
    }
    .dc-crumbs a {
      color: var(--dga-green-700);
      text-decoration: none;
    }
    .dc-filters {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-block: 16px;
    }
    .dc-filters__search {
      display: flex;
      align-items: center;
      gap: 8px;
      max-width: 36rem;
      padding-inline: 12px;
      border: 1px solid var(--dga-input-border);
      border-radius: 8px;
      background: #ffffff;
    }
    .dc-filters__search input {
      flex: 1;
      height: 44px;
      border: none;
      outline: none;
      background: transparent;
      font: inherit;
    }
    .dc-filters__tabs {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .dc-chip {
      height: 36px;
      padding-inline: 14px;
      border: 1px solid var(--dga-border);
      border-radius: 999px;
      background: #ffffff;
      font: inherit;
      cursor: pointer;
    }
    .dc-chip--active {
      border-color: var(--dga-green-700);
      background: var(--dga-green-700);
      color: #ffffff;
    }
    .dc-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
      gap: 16px;
      margin: 8px 0 0;
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

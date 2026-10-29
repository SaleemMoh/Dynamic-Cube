import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink } from '@angular/router';
import { AuthService, I18nService, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { PageHero } from '../portal/page-hero';
import { PortalStore } from '../portal/portal-store';
import { ServiceTile } from '../portal/service-tile';
import { VoiceSearch } from '../portal/voice-search';
import { ActingPanel } from './acting-panel';
import { StatisticsTiles } from './statistics-tiles';

/**
 * The portal home, as the DGA guide lays it out: the title band, a search with a dark button (its menu searches one
 * category), the services by category in underline tabs and a paged carousel, and the statistics. Signed-in
 * applicants also see who they are applying for.
 */
@Component({
  selector: 'dc-home',
  imports: [
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    RouterLink,
    LocalizePipe,
    TranslatePipe,
    ActingPanel,
    PageHero,
    ServiceTile,
    StatisticsTiles,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero
      data-testid="hero"
      [title]="store.heroTitle({ ar: 'بوابة الخدمات الإلكترونية', en: 'E-services portal' }) | dcLocalize"
      [text]="store.heroText(defaultText) | dcLocalize"
    />

    <div class="dc-home">
      <form class="dc-dga-search dc-portal-block" role="search" (ngSubmit)="search('')">
        <label class="dc-dga-search__field">
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">search</mat-icon>
          <input
            name="q"
            type="search"
            [ngModel]="query()"
            (ngModelChange)="query.set($event)"
            [attr.aria-label]="'portal.searchLabel' | dcTranslate"
            [placeholder]="'portal.searchPlaceholder' | dcTranslate"
            data-testid="hero-search"
          />
          @if (voice.supported) {
            <button
              type="button"
              class="dc-dga-search__voice"
              [class.dc-dga-search__voice--on]="voice.listening()"
              (click)="listen()"
              [attr.aria-label]="'portal.voiceSearch' | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">mic</mat-icon>
            </button>
          }
        </label>
        <button type="button" class="dc-dark-button" [matMenuTriggerFor]="scope" data-testid="hero-search-submit">
          {{ 'portal.search' | dcTranslate }}
          <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">expand_more</mat-icon>
        </button>
      </form>
      <mat-menu #scope="matMenu">
        <button mat-menu-item (click)="search('')" data-testid="search-all">
          <mat-icon fontSet="material-symbols-outlined">apps</mat-icon>{{ 'portal.searchAll' | dcTranslate }}
        </button>
        @for (c of store.categories(); track c.code) {
          <button mat-menu-item (click)="search(c.code)" [attr.data-testid]="'search-in-' + c.code">
            <mat-icon fontSet="material-symbols-outlined">{{ c.icon || 'folder' }}</mat-icon
            >{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
          </button>
        }
      </mat-menu>

      @if (auth.isAuthenticated()) {
        <div class="dc-portal-block"><dc-acting-panel /></div>
      }

      <section class="dc-services dc-portal-block" aria-labelledby="dc-services-title">
        <div class="dc-utabs dc-services__tabs" role="tablist" [attr.aria-label]="'portal.categories' | dcTranslate">
          <button
            role="tab"
            class="dc-utabs__tab"
            [class.dc-utabs__tab--active]="tab() === ''"
            [attr.aria-selected]="tab() === ''"
            (click)="choose('')"
            data-testid="tab-all"
          >
            {{ 'portal.all' | dcTranslate }}
          </button>
          @for (c of store.categories(); track c.code) {
            <button
              role="tab"
              class="dc-utabs__tab"
              [class.dc-utabs__tab--active]="tab() === c.code"
              [attr.aria-selected]="tab() === c.code"
              (click)="choose(c.code)"
              [attr.data-testid]="'tab-' + c.code"
            >
              {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
            </button>
          }
        </div>

        <div class="dc-section-head">
          <div>
            <h2 id="dc-services-title">{{ sectionTitle() }}</h2>
            <p>{{ 'portal.servicesLead' | dcTranslate }}</p>
          </div>
          <a
            matButton="outlined"
            class="dc-outline-button"
            [routerLink]="['/services']"
            [queryParams]="tab() ? { category: tab() } : {}"
            data-testid="view-all"
          >
            {{ 'portal.viewAll' | dcTranslate }}
          </a>
        </div>

        <ul class="dc-carousel" #track (scroll)="measure()" data-testid="carousel">
          @for (s of shown(); track s.code) {
            <li class="dc-carousel__item"><dc-service-tile [service]="s" /></li>
          } @empty {
            <li class="dc-muted dc-carousel__empty">{{ (store.catalog.isLoading() ? 'portal.loading' : 'catalog.empty') | dcTranslate }}</li>
          }
        </ul>
        @if (pages() > 1) {
          <div class="dc-carousel__controls">
            <div class="dc-carousel__arrows">
              <button
                type="button"
                class="dc-carousel__prev"
                (click)="goTo(page() - 1)"
                [disabled]="page() === 0"
                [attr.aria-label]="'portal.previous' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined" class="dc-flip-ltr">chevron_right</mat-icon>
              </button>
              <button
                type="button"
                class="dc-carousel__next"
                (click)="goTo(page() + 1)"
                [disabled]="page() >= pages() - 1"
                [attr.aria-label]="'portal.next' | dcTranslate"
                data-testid="carousel-next"
              >
                <mat-icon fontSet="material-symbols-outlined" class="dc-flip-ltr">chevron_left</mat-icon>
              </button>
            </div>
            <div class="dc-carousel__dots" role="group" [attr.aria-label]="'portal.pages' | dcTranslate">
              @for (p of pageList(); track p) {
                <button
                  type="button"
                  class="dc-carousel__dot"
                  [class.dc-carousel__dot--active]="p === page()"
                  [attr.aria-current]="p === page() ? 'true' : null"
                  [attr.aria-label]="('portal.page' | dcTranslate) + ' ' + (p + 1)"
                  (click)="goTo(p)"
                ></button>
              }
            </div>
          </div>
        }
      </section>

      <dc-statistics-tiles class="dc-portal-block" />
    </div>
  `,
  styleUrl: './home.scss',
})
export class Home {
  protected readonly auth = inject(AuthService);
  protected readonly store = inject(PortalStore);
  protected readonly voice = inject(VoiceSearch);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  private readonly track = viewChild<ElementRef<HTMLElement>>('track');

  protected readonly defaultText = {
    ar: 'قدّم طلباتك وتابعها واستلم شهاداتك إلكترونيًا، في أي وقت ومن أي مكان.',
    en: 'Apply, follow your requests and receive your certificates online, any time and anywhere.',
  };
  protected readonly query = signal('');
  protected readonly tab = signal('');
  protected readonly page = signal(0);
  protected readonly pages = signal(1);
  protected readonly pageList = computed(() => Array.from({ length: this.pages() }, (_, i) => i));
  protected readonly shown = computed(() => {
    const tab = this.tab();
    return this.store.services().filter((s) => !tab || s.categoryCode === tab);
  });
  protected readonly sectionTitle = computed(() => {
    const category = this.store.category(this.tab());
    return category
      ? this.i18n.translate('portal.servicesOf').replace('{name}', this.i18n.localize({ ar: category.nameAr, en: category.nameEn }))
      : this.i18n.translate('portal.services');
  });

  constructor() {
    // Pages follow the cards shown and the width they get.
    afterRenderEffect(() => {
      this.shown();
      this.i18n.direction();
      this.measure();
    });
  }

  protected choose(code: string): void {
    this.tab.set(code);
    this.goTo(0);
  }

  protected search(category: string): void {
    const queryParams: Record<string, string> = {};
    if (this.query().trim()) queryParams['q'] = this.query().trim();
    if (category) queryParams['category'] = category;
    void this.router.navigate(['/services'], { queryParams });
  }

  protected async listen(): Promise<void> {
    const heard = await this.voice.listen(this.i18n.language());
    if (heard) this.query.set(heard);
  }

  /** How many views the cards take and which one shows. */
  protected measure(): void {
    const el = this.track()?.nativeElement;
    if (!el || !el.clientWidth) return;
    const pages = Math.max(1, Math.ceil((el.scrollWidth - 2) / el.clientWidth));
    this.pages.set(pages);
    this.page.set(Math.min(pages - 1, Math.round(Math.abs(el.scrollLeft) / el.clientWidth)));
  }

  /** Shows one view of cards; in Arabic the cards run to the left. */
  protected goTo(page: number): void {
    const el = this.track()?.nativeElement;
    if (!el) return;
    const target = Math.max(0, Math.min(page, this.pages() - 1));
    const sign = this.i18n.direction() === 'rtl' ? -1 : 1;
    el.scrollTo({ left: sign * target * el.clientWidth, behavior: 'smooth' });
    this.page.set(target);
  }
}

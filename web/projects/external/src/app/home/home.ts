import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { AuthService, I18nService, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from '../portal/portal-store';
import { ServiceTile } from '../portal/service-tile';
import { ActingPanel } from './acting-panel';
import { StatisticsTiles } from './statistics-tiles';
import { VoiceSearch } from '../portal/voice-search';

/**
 * The portal home: a hero banner with the portal's purpose and a search by category, the services by category in a
 * carousel, the statistics tiles and the last update. Signed-in applicants also see who they are applying for.
 */
@Component({
  selector: 'dc-home',
  imports: [DatePipe, FormsModule, MatButtonModule, MatIconModule, RouterLink, LocalizePipe, TranslatePipe, ActingPanel, ServiceTile, StatisticsTiles],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-hero" [style.--dc-hero-image]="bannerImage()" data-testid="hero">
      <div class="dc-hero__inner">
        <h1 class="dc-hero__title">{{ store.heroTitle({ ar: 'بوابة الخدمات الإلكترونية', en: 'E-services portal' }) | dcLocalize }}</h1>
        <p class="dc-hero__text">{{ store.heroText(defaultText) | dcLocalize }}</p>
        <form class="dc-search" role="search" (ngSubmit)="search()">
          <label class="dc-search__field">
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
                class="dc-search__voice"
                [class.dc-search__voice--on]="voice.listening()"
                (click)="listen()"
                [attr.aria-label]="'portal.voiceSearch' | dcTranslate"
              >
                <mat-icon fontSet="material-symbols-outlined">mic</mat-icon>
              </button>
            }
          </label>
          <select
            class="dc-search__category"
            name="category"
            [ngModel]="searchCategory()"
            (ngModelChange)="searchCategory.set($event)"
            [attr.aria-label]="'portal.category' | dcTranslate"
            data-testid="hero-category"
          >
            <option value="">{{ 'portal.allCategories' | dcTranslate }}</option>
            @for (c of store.categories(); track c.code) {
              <option [value]="c.code">{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}</option>
            }
          </select>
          <button matButton="filled" type="submit" class="dc-search__submit" data-testid="hero-search-submit">{{ 'portal.search' | dcTranslate }}</button>
        </form>
      </div>
    </section>

    <div class="dc-home">
      @if (auth.isAuthenticated()) {
        <dc-acting-panel />
      }

      <section class="dc-services" aria-labelledby="dc-services-title">
        <div class="dc-section-head">
          <h2 id="dc-services-title">{{ 'portal.services' | dcTranslate }}</h2>
          <a class="dc-section-head__all" [routerLink]="['/services']" [queryParams]="tab() ? { category: tab() } : {}" data-testid="view-all">
            {{ 'portal.viewAll' | dcTranslate }}
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">arrow_back</mat-icon>
          </a>
        </div>
        <div class="dc-tabs" role="tablist" [attr.aria-label]="'portal.categories' | dcTranslate">
          <button role="tab" class="dc-tabs__tab" [class.dc-tabs__tab--active]="tab() === ''" [attr.aria-selected]="tab() === ''" (click)="tab.set('')" data-testid="tab-all">
            {{ 'portal.all' | dcTranslate }}
          </button>
          @for (c of store.categories(); track c.code) {
            <button
              role="tab"
              class="dc-tabs__tab"
              [class.dc-tabs__tab--active]="tab() === c.code"
              [attr.aria-selected]="tab() === c.code"
              (click)="tab.set(c.code)"
              [attr.data-testid]="'tab-' + c.code"
            >
              @if (c.icon) {
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ c.icon }}</mat-icon>
              }
              {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
            </button>
          }
        </div>
        <div class="dc-carousel">
          <button type="button" class="dc-carousel__nav dc-carousel__nav--prev" (click)="scroll(-1)" [attr.aria-label]="'portal.previous' | dcTranslate">
            <mat-icon fontSet="material-symbols-outlined">chevron_right</mat-icon>
          </button>
          <ul class="dc-carousel__track" #track data-testid="carousel">
            @for (s of shown(); track s.code) {
              <li class="dc-carousel__item"><dc-service-tile [service]="s" /></li>
            } @empty {
              <li class="dc-muted dc-carousel__empty">{{ (store.catalog.isLoading() ? 'portal.loading' : 'catalog.empty') | dcTranslate }}</li>
            }
          </ul>
          <button type="button" class="dc-carousel__nav dc-carousel__nav--next" (click)="scroll(1)" [attr.aria-label]="'portal.next' | dcTranslate">
            <mat-icon fontSet="material-symbols-outlined">chevron_left</mat-icon>
          </button>
        </div>
      </section>

      <dc-statistics-tiles />

      @if (store.catalog.value()?.lastUpdated; as updated) {
        <p class="dc-updated" data-testid="last-updated">
          {{ 'portal.lastUpdated' | dcTranslate }} <span dir="ltr">{{ updated | date: 'yyyy-MM-dd' }}</span>
        </p>
      }
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
  protected readonly searchCategory = signal('');
  protected readonly tab = signal('');
  protected readonly shown = computed(() => {
    const tab = this.tab();
    return this.store.services().filter((s) => !tab || s.categoryCode === tab);
  });
  protected readonly bannerImage = computed(() => {
    const url = this.store.setting.value()?.bannerUrl;
    return url ? `url("${encodeURI(url)}")` : null;
  });

  protected search(): void {
    const queryParams: Record<string, string> = {};
    if (this.query().trim()) queryParams['q'] = this.query().trim();
    if (this.searchCategory()) queryParams['category'] = this.searchCategory();
    void this.router.navigate(['/services'], { queryParams });
  }

  protected async listen(): Promise<void> {
    const heard = await this.voice.listen(this.i18n.language());
    if (heard) this.query.set(heard);
  }

  /** Moves the carousel by one view; "next" goes towards the reading direction's end. */
  protected scroll(step: 1 | -1): void {
    const el = this.track()?.nativeElement;
    if (!el) return;
    const sign = this.i18n.direction() === 'rtl' ? -1 : 1;
    el.scrollBy({ left: sign * step * el.clientWidth * 0.9, behavior: 'smooth' });
  }
}

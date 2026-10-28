import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { ActingPartyService, AuthService, FeeTable, FieldError, I18nService, LocalizePipe, ServicesApi, TranslatePipe, requestErrors } from '@dynamic-cube/shared';
import { firstValueFrom, of } from 'rxjs';
import { CHANNEL_ICONS, CostPipe, DurationPipe, PARTY_KIND_KEYS } from '../portal/portal-text';

/**
 * The service card: what the service is, its steps, terms and required documents, and a side card with the audience,
 * duration, channels, cost and contacts. Anyone may read it; starting the service asks for sign-in first, and a
 * signed-in applicant sees whether the acting party may apply and the fee of each level for them.
 */
@Component({
  selector: 'dc-service-page',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTabsModule, RouterLink, FeeTable, LocalizePipe, TranslatePipe, CostPipe, DurationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (card.value(); as c) {
      <nav class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
        <a routerLink="/">{{ 'portal.home' | dcTranslate }}</a>
        <span aria-hidden="true">/</span>
        @if (c.category; as category) {
          <a [routerLink]="['/services']" [queryParams]="{ category: category.code }" data-testid="crumb-category">{{ { ar: category.nameAr, en: category.nameEn } | dcLocalize }}</a>
        } @else {
          <a routerLink="/services">{{ 'portal.services' | dcTranslate }}</a>
        }
        <span aria-hidden="true">/</span>
        <span>{{ { ar: c.service.nameAr, en: c.service.nameEn } | dcLocalize }}</span>
      </nav>

      <header class="dc-head">
        <span class="dc-head__icon" aria-hidden="true"><mat-icon fontSet="material-symbols-outlined">{{ c.service.icon || 'description' }}</mat-icon></span>
        <div class="dc-head__text">
          <h1 data-testid="service-title">{{ { ar: c.service.nameAr, en: c.service.nameEn } | dcLocalize }}</h1>
          @if (c.service.descriptionAr || c.service.descriptionEn) {
            <p>{{ { ar: c.service.descriptionAr ?? '', en: c.service.descriptionEn ?? '' } | dcLocalize }}</p>
          }
        </div>
        <div class="dc-head__start">
          @if (!auth.isAuthenticated()) {
            <button matButton="filled" (click)="signIn()" data-testid="start-request">
              <mat-icon fontSet="material-symbols-outlined">login</mat-icon>
              {{ 'portal.startService' | dcTranslate }}
            </button>
            <span class="dc-muted">{{ 'portal.signInFirst' | dcTranslate }}</span>
          } @else if (view.value(); as v) {
            @if (v.service.isEligible) {
              <button matButton="filled" (click)="start(c.service.code)" [disabled]="starting()" data-testid="start-request">
                <mat-icon fontSet="material-symbols-outlined">edit_document</mat-icon>
                {{ 'portal.startService' | dcTranslate }}
              </button>
            } @else {
              <ul class="dc-errors" role="alert" data-testid="not-eligible">
                @for (r of v.service.reasons; track r.code) {
                  <li>{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</li>
                }
              </ul>
            }
          }
        </div>
      </header>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ { ar: e.messageAr, en: e.messageEn } | dcLocalize }}</li>
          }
        </ul>
      }

      <div class="dc-body">
        <mat-tab-group class="dc-body__tabs" mat-stretch-tabs="false" animationDuration="0ms">
          <mat-tab [label]="'portal.steps' | dcTranslate">
            @if (c.card.steps.length) {
              <ol class="dc-steps" data-testid="card-steps">
                @for (step of c.card.steps; track $index) {
                  <li>
                    <span class="dc-steps__number">{{ $index + 1 }}</span>
                    <span>{{ { ar: step.textAr, en: step.textEn } | dcLocalize }}</span>
                  </li>
                }
              </ol>
            } @else {
              <p class="dc-muted dc-tab-empty">{{ 'portal.noSteps' | dcTranslate }}</p>
            }
          </mat-tab>
          <mat-tab [label]="'portal.terms' | dcTranslate">
            @if (terms(); as paragraphs) {
              <ul class="dc-terms" data-testid="card-terms">
                @for (p of paragraphs; track $index) {
                  <li>{{ p }}</li>
                }
              </ul>
            } @else {
              <p class="dc-muted dc-tab-empty">{{ 'portal.noTerms' | dcTranslate }}</p>
            }
          </mat-tab>
          <mat-tab [label]="'portal.requiredDocuments' | dcTranslate">
            @if (c.card.requiredDocuments.length) {
              <ul class="dc-documents" data-testid="card-documents">
                @for (d of c.card.requiredDocuments; track $index) {
                  <li>
                    <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">description</mat-icon>
                    <span>
                      <strong>{{ { ar: d.nameAr, en: d.nameEn } | dcLocalize }}</strong>
                      @if (d.noteAr || d.noteEn) {
                        <span class="dc-muted">{{ { ar: d.noteAr ?? '', en: d.noteEn ?? '' } | dcLocalize }}</span>
                      }
                    </span>
                  </li>
                }
              </ul>
            } @else {
              <p class="dc-muted dc-tab-empty">{{ 'portal.noDocuments' | dcTranslate }}</p>
            }
          </mat-tab>
          @if (c.levels.length) {
            <mat-tab [label]="'portal.levels' | dcTranslate">
              <div class="dc-levels" data-testid="card-levels">
                @for (level of c.levels; track level.code) {
                  <section class="dc-levels__level">
                    <h3>{{ { ar: level.nameAr, en: level.nameEn } | dcLocalize }}</h3>
                    @if (level.descriptionAr || level.descriptionEn) {
                      <p class="dc-muted">{{ { ar: level.descriptionAr ?? '', en: level.descriptionEn ?? '' } | dcLocalize }}</p>
                    }
                    <p>{{ 'portal.duration' | dcTranslate }}: {{ level.slaWorkingDays | dcDuration }}</p>
                    @if (view.value()?.feesByLevel?.[level.code]; as lines) {
                      <dc-fee-table [lines]="lines" />
                    } @else {
                      <p>{{ 'portal.cost' | dcTranslate }}: {{ level.cost | dcCost }}</p>
                    }
                  </section>
                }
              </div>
            </mat-tab>
          }
        </mat-tab-group>

        <aside class="dc-side" data-testid="service-side">
          <dl>
            <div class="dc-side__row">
              <dt><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">groups</mat-icon>{{ 'portal.audience' | dcTranslate }}</dt>
              <dd data-testid="side-audience">
                @if (c.service.audienceAr || c.service.audienceEn) {
                  {{ { ar: c.service.audienceAr ?? '', en: c.service.audienceEn ?? '' } | dcLocalize }}
                } @else {
                  @for (kind of c.service.partyKinds; track kind; let last = $last) {
                    {{ kindKeys[kind] | dcTranslate }}{{ last ? '' : ' · ' }}
                  }
                }
              </dd>
            </div>
            <div class="dc-side__row">
              <dt><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">schedule</mat-icon>{{ 'portal.duration' | dcTranslate }}</dt>
              <dd data-testid="side-duration">{{ c.service.duration | dcDuration }}</dd>
            </div>
            <div class="dc-side__row">
              <dt><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">devices</mat-icon>{{ 'portal.channels' | dcTranslate }}</dt>
              <dd class="dc-side__channels">
                @for (channel of channels(); track channel) {
                  <span><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ channelIcons[channel] }}</mat-icon>{{ 'portal.channel.' + channel | dcTranslate }}</span>
                }
              </dd>
            </div>
            <div class="dc-side__row">
              <dt><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">payments</mat-icon>{{ 'portal.cost' | dcTranslate }}</dt>
              <dd data-testid="side-cost">
                {{ c.service.cost | dcCost }}
                @if (c.service.cost.max !== 0 || c.service.cost.dependsOnRequest) {
                  <span class="dc-muted dc-side__vat">{{ 'portal.vatIncluded' | dcTranslate }}</span>
                }
              </dd>
            </div>
          </dl>
          @if (c.card.faqUrl || c.card.phone || c.card.email) {
            <div class="dc-side__contacts">
              <h2>{{ 'portal.contact' | dcTranslate }}</h2>
              @if (c.card.faqUrl; as faq) {
                <a [href]="faq" [attr.target]="faq.startsWith('/') ? null : '_blank'" rel="noopener"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">quiz</mat-icon>{{ 'portal.faq' | dcTranslate }}</a>
              }
              @if (c.card.phone; as phone) {
                <a [href]="'tel:' + phone.replaceAll(' ', '')"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">call</mat-icon><span dir="ltr">{{ phone }}</span></a>
              }
              @if (c.card.email; as email) {
                <a [href]="'mailto:' + email"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">mail</mat-icon><span dir="ltr">{{ email }}</span></a>
              }
            </div>
          }
        </aside>
      </div>
    } @else if (card.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (card.error()) {
      <p class="dc-errors" role="alert">{{ 'catalog.notFound' | dcTranslate }}</p>
    }
  `,
  styleUrl: './service-page.scss',
})
export class ServicePage {
  readonly code = input.required<string>();
  protected readonly auth = inject(AuthService);
  private readonly api = inject(ServicesApi);
  private readonly router = inject(Router);
  private readonly acting = inject(ActingPartyService);
  private readonly i18n = inject(I18nService);
  protected readonly kindKeys = PARTY_KIND_KEYS;
  protected readonly channelIcons = CHANNEL_ICONS;
  protected readonly starting = signal(false);
  protected readonly errors = signal<FieldError[]>([]);

  protected readonly card = rxResource({ params: () => this.code(), stream: ({ params }) => this.api.serviceCard(params) });
  /** For a signed-in applicant: eligibility of the acting party and the fees of each level for them. */
  protected readonly view = rxResource({
    params: () => (this.auth.isAuthenticated() ? { code: this.code(), party: this.acting.current()?.partyId } : null),
    stream: ({ params }) => (params ? this.api.service(params.code) : of(undefined)),
  });

  /** Terms of use in the current language, one paragraph per line. */
  protected readonly terms = computed(() => {
    const card = this.card.value()?.card;
    const text = card ? this.i18n.localize({ ar: card.termsAr ?? '', en: card.termsEn ?? '' }) : '';
    const paragraphs = text.split('\n').map((p) => p.trim()).filter((p) => p);
    return paragraphs.length ? paragraphs : null;
  });
  protected readonly channels = computed(() => {
    const channels = this.card.value()?.card.channels ?? [];
    return channels.length ? channels : (['PORTAL'] as const);
  });

  protected signIn(): Promise<void> {
    return this.auth.login(`/services/${encodeURIComponent(this.code())}`);
  }

  protected async start(code: string): Promise<void> {
    this.starting.set(true);
    this.errors.set([]);
    try {
      const draft = await firstValueFrom(this.api.create(code));
      await this.router.navigate(['/requests', draft.request.id]);
    } catch (error) {
      this.errors.set(requestErrors(error));
    } finally {
      this.starting.set(false);
    }
  }
}

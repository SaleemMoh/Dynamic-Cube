import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { ActingPartyService, AuthService, ContentApi, FeeTable, FieldError, I18nService, LocalizePipe, ServicesApi, TranslatePipe, requestErrors } from '@dynamic-cube/shared';
import { catchError, firstValueFrom, of } from 'rxjs';
import { PageFeedback } from '../portal/page-feedback';
import { PortalStore } from '../portal/portal-store';
import { CHANNEL_ICONS, CostPipe, DurationPipe, PARTY_KIND_KEYS } from '../portal/portal-text';
import { ServiceRating } from '../portal/service-rating';
import { ServiceTile } from '../portal/service-tile';
import { SuggestionsCard } from '../portal/suggestions-card';

type ServiceTab = 'steps' | 'terms' | 'documents' | 'levels';

/**
 * The service card as the DGA guide lays it out: title and start button, description, underline tabs (steps, terms,
 * required documents, levels), a side card with the audience, duration, channels, cost and contacts, then related
 * services, comments and suggestions, the rating and "was this page helpful?". Anyone may read it; starting the service asks for sign-in first, and a
 * signed-in applicant sees whether the acting party may apply and the fee of each level for them.
 */
@Component({
  selector: 'dc-service-page',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
    FeeTable,
    LocalizePipe,
    TranslatePipe,
    CostPipe,
    DurationPipe,
    PageFeedback,
    ServiceRating,
    ServiceTile,
    SuggestionsCard,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (card.value(); as c) {
      <div class="dc-page">
      <div class="dc-main">
      <ol class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
        <li><a routerLink="/">{{ 'portal.home' | dcTranslate }}</a></li>
        @if (c.category; as category) {
          <li><a [routerLink]="['/services']" [queryParams]="{ category: category.code }" data-testid="crumb-category">{{ { ar: category.nameAr, en: category.nameEn } | dcLocalize }}</a></li>
        } @else {
          <li><a routerLink="/services">{{ 'portal.services' | dcTranslate }}</a></li>
        }
        <li aria-current="page">{{ { ar: c.service.nameAr, en: c.service.nameEn } | dcLocalize }}</li>
      </ol>

      <header class="dc-head">
        <h1 data-testid="service-title">{{ { ar: c.service.nameAr, en: c.service.nameEn } | dcLocalize }}</h1>
        <div class="dc-head__start">
          @if (!auth.isAuthenticated()) {
            <button matButton="filled" (click)="signIn()" data-testid="start-request">
              <mat-icon fontSet="material-symbols-outlined">login</mat-icon>
              {{ 'portal.startService' | dcTranslate }}
            </button>
            <span class="dc-muted">{{ 'portal.signInFirst' | dcTranslate }}</span>
          } @else if (view.value(); as v) {
            @if (v.service.documentType) {
              <span class="dc-muted">{{ 'followUps.choose' | dcTranslate }}</span>
            } @else if (v.service.isEligible) {
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
      @if (c.service.descriptionAr || c.service.descriptionEn) {
        <p class="dc-lead">{{ { ar: c.service.descriptionAr ?? '', en: c.service.descriptionEn ?? '' } | dcLocalize }}</p>
      }
      @if (view.value(); as v) {
        @if (v.service.documentType) {
          <section class="dc-docs" data-testid="document-choice">
            <h2>{{ 'followUps.yourDocuments' | dcTranslate }}</h2>
            @for (d of v.documents; track d.id) {
              <div class="dc-docs__item" [attr.data-testid]="'document-' + d.number">
                <div class="dc-docs__text">
                  <strong dir="ltr">{{ d.number }}</strong>
                  <span class="dc-muted">
                    {{ 'followUps.status.' + d.status | dcTranslate }}
                    @if (d.expiresOn) {
                      · {{ 'followUps.expires' | dcTranslate }} <span dir="ltr">{{ d.expiresOn }}</span>
                    }
                  </span>
                  @if (!d.isAvailable && !d.openRequestId) {
                    @for (r of d.reasons; track r.code) {
                      <span class="dc-docs__reason">{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</span>
                    }
                  }
                </div>
                @if (d.openRequestId; as requestId) {
                  <a matButton="outlined" [routerLink]="['/requests', requestId]">{{ 'followUps.continue' | dcTranslate }} {{ d.openRequestNumber ?? '' }}</a>
                } @else if (d.isAvailable && v.service.isEligible) {
                  <button matButton="filled" (click)="start(c.service.code, d.id)" [disabled]="starting()" [attr.data-testid]="'start-on-' + d.number">
                    {{ 'portal.startService' | dcTranslate }}
                  </button>
                }
              </div>
            } @empty {
              <ul class="dc-errors" role="alert" data-testid="not-eligible">
                @for (r of v.service.reasons; track r.code) {
                  <li>{{ { ar: r.messageAr, en: r.messageEn } | dcLocalize }}</li>
                }
              </ul>
            }
          </section>
        }
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ { ar: e.messageAr, en: e.messageEn } | dcLocalize }}</li>
          }
        </ul>
      }

      <div class="dc-utabs dc-body__tabs" role="tablist">
        @for (t of tabs(); track t) {
          <button
            role="tab"
            class="dc-utabs__tab"
            [class.dc-utabs__tab--active]="tab() === t"
            [attr.aria-selected]="tab() === t"
            (click)="tab.set(t)"
            [attr.data-testid]="'service-tab-' + t"
          >
            {{ 'portal.tab.' + t | dcTranslate }}
          </button>
        }
      </div>
      <div class="dc-panel" role="tabpanel">
        @switch (tab()) {
          @case ('steps') {
            @if (c.card.steps.length) {
              <ol class="dc-steps" data-testid="card-steps">
                @for (step of c.card.steps; track $index) {
                  <li>{{ { ar: step.textAr, en: step.textEn } | dcLocalize }}</li>
                }
              </ol>
            } @else {
              <p class="dc-muted dc-tab-empty">{{ 'portal.noSteps' | dcTranslate }}</p>
            }
          }
          @case ('terms') {
            @if (terms(); as paragraphs) {
              <ul class="dc-terms" data-testid="card-terms">
                @for (p of paragraphs; track $index) {
                  <li>{{ p }}</li>
                }
              </ul>
            } @else {
              <p class="dc-muted dc-tab-empty">{{ 'portal.noTerms' | dcTranslate }}</p>
            }
          }
          @case ('documents') {
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
          }
          @case ('levels') {
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
          }
        }
      </div>
      </div>

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
            @if (c.service.requiredDocument; as required) {
              <div class="dc-side__row">
                <dt><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">badge</mat-icon>{{ 'followUps.requires' | dcTranslate }}</dt>
                <dd data-testid="side-requires">{{ { ar: required.nameAr, en: required.nameEn } | dcLocalize }}</dd>
              </div>
            }
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
          @if (c.card.faqUrl || hasFaqs() || phone() || email()) {
            <div class="dc-side__contacts">
              @if (hasFaqs() || c.card.faqUrl) {
                <div class="dc-side__contact">
                  <span class="dc-side__label"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">quiz</mat-icon>{{ 'portal.faq' | dcTranslate }}</span>
                  @if (hasFaqs()) {
                    <a routerLink="/faq" [queryParams]="{ service: c.service.code }" data-testid="service-faqs"
                      ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">open_in_new</mat-icon>{{ 'portal.serviceFaqs' | dcTranslate }}</a
                    >
                  } @else if (c.card.faqUrl; as faq) {
                    <a [href]="faq" [attr.target]="faq.startsWith('/') ? null : '_blank'" rel="noopener"
                      ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">open_in_new</mat-icon>{{ 'portal.serviceFaqs' | dcTranslate }}</a
                    >
                  }
                </div>
              }
              @if (phone(); as phone) {
                <div class="dc-side__contact">
                  <span class="dc-side__label"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">call</mat-icon>{{ 'contact.phone' | dcTranslate }}</span>
                  <a [href]="'tel:' + phone.replaceAll(' ', '')"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">open_in_new</mat-icon><span dir="ltr">{{ phone }}</span></a>
                </div>
              }
              @if (email(); as email) {
                <div class="dc-side__contact">
                  <span class="dc-side__label"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">mail</mat-icon>{{ 'contact.email' | dcTranslate }}</span>
                  <a [href]="'mailto:' + email"><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">open_in_new</mat-icon><span dir="ltr">{{ email }}</span></a>
                </div>
              }
            </div>
          }
        </aside>
      </div>

      @if (related().length) {
        <section class="dc-related" aria-labelledby="dc-related-title" data-testid="related-services">
          <div class="dc-related__inner">
            <div class="dc-related__head">
              <h2 id="dc-related-title">{{ 'portal.related' | dcTranslate }}</h2>
              <a matButton [routerLink]="['/services']" [queryParams]="c.category ? { category: c.category.code } : {}">{{ 'portal.viewAll' | dcTranslate }}</a>
            </div>
            <ul class="dc-related__list">
              @for (s of related(); track s.code) {
                <li><dc-service-tile [service]="s" /></li>
              }
            </ul>
          </div>
        </section>
      }

      <div class="dc-after">
        <dc-suggestions-card />
        <dc-service-rating [service]="c.service.code" />
        <dc-page-feedback [page]="'service:' + c.service.code.toLowerCase()" />
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
  private readonly content = inject(ContentApi);
  protected readonly kindKeys = PARTY_KIND_KEYS;
  protected readonly channelIcons = CHANNEL_ICONS;
  private readonly store = inject(PortalStore);
  protected readonly starting = signal(false);
  protected readonly tab = signal<ServiceTab>('steps');
  protected readonly errors = signal<FieldError[]>([]);

  protected readonly card = rxResource({ params: () => this.code(), stream: ({ params }) => this.api.serviceCard(params) });
  /** For a signed-in applicant: eligibility of the acting party and the fees of each level for them. */
  protected readonly view = rxResource({
    params: () => (this.auth.isAuthenticated() ? { code: this.code(), party: this.acting.current()?.partyId } : null),
    stream: ({ params }) => (params ? this.api.service(params.code) : of(undefined)),
  });

  /** Questions about this service written in the Content Manager (its page links to them). */
  private readonly faqs = rxResource({ params: () => this.code(), stream: ({ params }) => this.content.faqs(params).pipe(catchError(() => of([]))) });
  protected readonly hasFaqs = computed(() => (this.faqs.value() ?? []).length > 0);

  /** Terms of use in the current language, one paragraph per line. */
  protected readonly terms = computed(() => {
    const card = this.card.value()?.card;
    const text = card ? this.i18n.localize({ ar: card.termsAr ?? '', en: card.termsEn ?? '' }) : '';
    const paragraphs = text.split('\n').map((p) => p.trim()).filter((p) => p);
    return paragraphs.length ? paragraphs : null;
  });
  protected readonly tabs = computed<ServiceTab[]>(() => (this.card.value()?.levels.length ? ['steps', 'terms', 'documents', 'levels'] : ['steps', 'terms', 'documents']));
  /** Other services of the same category. */
  protected readonly related = computed(() => {
    const card = this.card.value();
    if (!card) return [];
    const category = card.category?.code ?? card.service.categoryCode;
    return this.store
      .services()
      .filter((s) => s.code !== card.service.code && !!category && s.categoryCode === category)
      .slice(0, 3);
  });
  /** The service's own contacts, else the entity's. */
  protected readonly phone = computed(() => this.card.value()?.card.phone || this.store.help().phone || null);
  protected readonly email = computed(() => this.card.value()?.card.email || this.store.help().email || null);
  protected readonly channels = computed(() => {
    const channels = this.card.value()?.card.channels ?? [];
    return channels.length ? channels : (['PORTAL'] as const);
  });

  protected signIn(): Promise<void> {
    return this.auth.login(`/services/${encodeURIComponent(this.code())}`);
  }

  protected async start(code: string, documentId?: string): Promise<void> {
    this.starting.set(true);
    this.errors.set([]);
    try {
      const draft = await firstValueFrom(this.api.create(code, documentId));
      await this.router.navigate(['/requests', draft.request.id]);
    } catch (error) {
      this.errors.set(requestErrors(error));
    } finally {
      this.starting.set(false);
    }
  }
}

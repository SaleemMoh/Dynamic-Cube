import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ContentApi, FaqView, I18nService, LocalizePipe, LocalizedText, MarkdownView, TranslatePipe, markdownText } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
import { PageFeedback } from '../portal/page-feedback';
import { PageHero } from '../portal/page-hero';
import { PortalStore } from '../portal/portal-store';

interface Topic {
  key: string;
  title: LocalizedText | null;
  faqs: FaqView[];
}

/** Frequently asked questions, grouped by topic and searchable; `?service=CODE` shows one service's questions. */
@Component({
  selector: 'dc-faq-page',
  imports: [
    FormsModule,
    MatExpansionModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
    LocalizePipe,
    MarkdownView,
    PageFeedback,
    PageHero,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-page-hero [title]="'faq.title' | dcTranslate" [text]="serviceName() ? null : ('faq.lead' | dcTranslate)" />
    <div class="dc-portal-block dc-page-body">
    <header class="dc-faq__head">
      @if (serviceName(); as name) {
        <p class="dc-muted" data-testid="faq-service">
          {{ 'faq.ofService' | dcTranslate }} <a [routerLink]="['/services', service()]">{{ name | dcLocalize }}</a> ·
          <a routerLink="/faq">{{ 'faq.all' | dcTranslate }}</a>
        </p>
      }
      <label class="dc-dga-search__field dc-faq__search">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">search</mat-icon>
        <input type="search" [placeholder]="'faq.search' | dcTranslate" [attr.aria-label]="'faq.search' | dcTranslate" [ngModel]="query()" (ngModelChange)="query.set($event)" data-testid="faq-search" />
      </label>
    </header>

    @if (faqs.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    }
    @for (topic of topics(); track topic.key) {
      <section class="dc-faq__topic" [attr.data-testid]="'faq-topic-' + $index">
        <h2>{{ topic.title ? (topic.title | dcLocalize) : ('faq.general' | dcTranslate) }}</h2>
        <mat-accordion>
          @for (f of topic.faqs; track f.code) {
            <mat-expansion-panel [attr.data-testid]="'faq-' + f.code">
              <mat-expansion-panel-header>
                <mat-panel-title>{{ { ar: f.questionAr, en: f.questionEn } | dcLocalize }}</mat-panel-title>
              </mat-expansion-panel-header>
              <dc-markdown [source]="i18n.localize({ ar: f.answerAr, en: f.answerEn })" />
            </mat-expansion-panel>
          }
        </mat-accordion>
      </section>
    } @empty {
      @if (!faqs.isLoading()) {
        <p class="dc-muted" data-testid="faq-empty">{{ (query().trim() ? 'faq.noMatch' : 'faq.empty') | dcTranslate }}</p>
      }
    }
    <dc-page-feedback [page]="service() ? 'faq:' + service()!.toLowerCase() : 'faq'" />
    </div>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-faq__head {
      display: grid;
      gap: 0.75rem;
    }
    .dc-faq__head p {
      margin: 0;
    }
    .dc-faq__search {
      max-width: 36rem;
    }
    .dc-faq__topic h2 {
      font-size: 20px !important;
    }
    .dc-faq__topic {
      margin-bottom: 1.75rem;
    }
    .dc-faq__topic h2 {
      margin: 0 0 0.75rem;
      font-size: 18px;
    }
  `,
})
export class FaqPage {
  /** From `?service=`. */
  readonly service = input<string | undefined>(undefined);
  protected readonly i18n = inject(I18nService);
  private readonly api = inject(ContentApi);
  private readonly store = inject(PortalStore);
  protected readonly query = signal('');

  protected readonly faqs = rxResource({
    params: () => this.service() ?? '',
    stream: ({ params }) => this.api.faqs(params || null).pipe(catchError(() => of([] as FaqView[]))),
  });

  protected readonly serviceName = computed<LocalizedText | null>(() => {
    const code = this.service();
    if (!code) return null;
    const s = this.store.services().find((x) => x.code === code);
    return s ? { ar: s.nameAr, en: s.nameEn } : { ar: code, en: code };
  });

  protected readonly topics = computed<Topic[]>(() => {
    const words = this.query()
      .trim()
      .toLowerCase()
      .split(/\s+/)
      .filter((w) => w);
    const matches = (f: FaqView) => {
      const text = [f.questionAr, f.questionEn, markdownText(f.answerAr), markdownText(f.answerEn), f.topicAr ?? '', f.topicEn ?? ''].join(' ').toLowerCase();
      return words.every((w) => text.includes(w));
    };
    const topics = new Map<string, Topic>();
    for (const f of (this.faqs.value() ?? []).filter(matches)) {
      const key = f.topicEn || f.topicAr || '';
      const topic = topics.get(key) ?? { key, title: key ? { ar: f.topicAr ?? key, en: f.topicEn ?? key } : null, faqs: [] };
      topic.faqs.push(f);
      topics.set(key, topic);
    }
    // Questions without a topic come last, under "General".
    return [...topics.values()].sort((a, b) => Number(!a.title) - Number(!b.title));
  });
}

import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ContentApi, FaqView, I18nService, LocalizePipe, LocalizedText, MarkdownView, TranslatePipe, markdownText } from '@dynamic-cube/shared';
import { catchError, of } from 'rxjs';
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
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    RouterLink,
    LocalizePipe,
    MarkdownView,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
      <a routerLink="/">{{ 'portal.home' | dcTranslate }}</a>
      <span aria-hidden="true">/</span>
      @if (serviceName(); as name) {
        <a routerLink="/faq">{{ 'faq.title' | dcTranslate }}</a>
        <span aria-hidden="true">/</span>
        <span>{{ name | dcLocalize }}</span>
      } @else {
        <span>{{ 'faq.title' | dcTranslate }}</span>
      }
    </nav>
    <header class="dc-faq__head">
      <h1>{{ 'faq.title' | dcTranslate }}</h1>
      @if (serviceName(); as name) {
        <p class="dc-muted" data-testid="faq-service">
          {{ 'faq.ofService' | dcTranslate }} <a [routerLink]="['/services', service()]">{{ name | dcLocalize }}</a> ·
          <a routerLink="/faq">{{ 'faq.all' | dcTranslate }}</a>
        </p>
      } @else {
        <p class="dc-muted">{{ 'faq.lead' | dcTranslate }}</p>
      }
      <mat-form-field subscriptSizing="dynamic" class="dc-faq__search">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input matInput [placeholder]="'faq.search' | dcTranslate" [ngModel]="query()" (ngModelChange)="query.set($event)" data-testid="faq-search" />
      </mat-form-field>
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
  `,
  styles: `
    .dc-faq__head {
      display: grid;
      gap: 0.5rem;
      margin-bottom: 1.5rem;
    }
    .dc-faq__head h1,
    .dc-faq__head p {
      margin: 0;
    }
    .dc-faq__search {
      max-width: 36rem;
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

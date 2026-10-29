import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ContentApi, I18nService, LocalizePipe, MarkdownView, TranslatePipe } from '@dynamic-cube/shared';

/** A portal page (terms of use, privacy, about) written in the Content Manager. */
@Component({
  selector: 'dc-content-page',
  imports: [MatProgressBarModule, RouterLink, LocalizePipe, MarkdownView, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (page.hasValue()) {
      @let p = page.value();
      <nav class="dc-crumbs" [attr.aria-label]="'portal.breadcrumb' | dcTranslate">
        <a routerLink="/">{{ 'portal.home' | dcTranslate }}</a>
        <span aria-hidden="true">/</span>
        <span>{{ { ar: p.titleAr, en: p.titleEn } | dcLocalize }}</span>
      </nav>
      <article class="dc-content-page">
        <h1 data-testid="page-title">{{ { ar: p.titleAr, en: p.titleEn } | dcLocalize }}</h1>
        <dc-markdown [source]="i18n.localize({ ar: p.bodyAr, en: p.bodyEn })" data-testid="page-body" />
        <p class="dc-muted dc-content-page__updated">{{ 'pages.updated' | dcTranslate }} {{ updated() }}</p>
      </article>
    } @else if (page.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (page.error()) {
      <p class="dc-errors" role="alert" data-testid="page-not-found">{{ 'pages.notFound' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-content-page {
      max-width: 52rem;
    }
    .dc-content-page h1 {
      margin: 0 0 1.25rem;
    }
    .dc-content-page__updated {
      margin-top: 2rem;
      font-size: 13px;
    }
  `,
})
export class ContentPage {
  readonly slug = input.required<string>();
  protected readonly i18n = inject(I18nService);
  private readonly api = inject(ContentApi);

  protected readonly page = rxResource({ params: () => this.slug(), stream: ({ params }) => this.api.page(params) });
  protected readonly updated = computed(() => {
    const at = this.page.hasValue() ? this.page.value().updatedAt : null;
    if (!at) return '';
    const locale = this.i18n.language() === 'ar' ? 'ar-SA-u-ca-islamic-umalqura-nu-latn' : 'en-GB';
    return new Intl.DateTimeFormat(locale, { dateStyle: 'long' }).format(new Date(at));
  });
}

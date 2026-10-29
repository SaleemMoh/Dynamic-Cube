import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ContentApi, I18nService, LocalizePipe, MarkdownView, TranslatePipe } from '@dynamic-cube/shared';
import { PageFeedback } from '../portal/page-feedback';
import { PageHero } from '../portal/page-hero';

/** A portal page (terms of use, privacy, about) written in the Content Manager. */
@Component({
  selector: 'dc-content-page',
  imports: [MatProgressBarModule, LocalizePipe, MarkdownView, PageFeedback, PageHero, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (page.hasValue()) {
      @let p = page.value();
      <dc-page-hero data-testid="page-title" [title]="{ ar: p.titleAr, en: p.titleEn } | dcLocalize" />
      <div class="dc-portal-block dc-page-body">
        <article class="dc-content-page">
          <dc-markdown [source]="i18n.localize({ ar: p.bodyAr, en: p.bodyEn })" data-testid="page-body" />
          <p class="dc-muted dc-content-page__updated">{{ 'pages.updated' | dcTranslate }} {{ updated() }}</p>
        </article>
        <dc-page-feedback [page]="'page:' + slug()" />
      </div>
    } @else if (page.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (page.error()) {
      <div class="dc-portal-block dc-page-body">
        <p class="dc-errors" role="alert" data-testid="page-not-found">{{ 'pages.notFound' | dcTranslate }}</p>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-content-page {
      max-width: 52rem;
      font-size: 16px;
      line-height: 28px;
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

import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from '../i18n/i18n.service';
import { MarkdownView } from './markdown-view';
import {
  PAGE_LAYOUTS,
  PageBlock,
  PageDesign,
  PageSection,
  safeImage,
  safeLink,
} from './page-design';

/** One block of a designed page in the current language. */
@Component({
  selector: 'dc-page-block',
  imports: [MatIconModule, MarkdownView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let b = block();
    @switch (b.kind) {
      @case ('heading') {
        @switch (b.level ?? 2) {
          @case (1) {
            <h1 class="pb__heading pb__h1" [style.text-align]="align()">
              {{ text(b.textAr, b.textEn) }}
            </h1>
          }
          @case (3) {
            <h3 class="pb__heading pb__h3" [style.text-align]="align()">
              {{ text(b.textAr, b.textEn) }}
            </h3>
          }
          @default {
            <h2 class="pb__heading pb__h2" [style.text-align]="align()">
              {{ text(b.textAr, b.textEn) }}
            </h2>
          }
        }
      }
      @case ('text') {
        <dc-markdown [source]="text(b.textAr, b.textEn)" [style.text-align]="align()" />
      }
      @case ('image') {
        @if (image(); as src) {
          <img class="pb__image" [src]="src" [alt]="text(b.altAr, b.altEn)" />
        } @else if (editing()) {
          <div class="pb__image-empty">
            <mat-icon fontSet="material-symbols-outlined">image</mat-icon>
          </div>
        }
      }
      @case ('button') {
        <div [style.text-align]="align()">
          <a
            class="pb__button"
            [class.pb__button--secondary]="b.style === 'secondary'"
            [attr.href]="link(b.url)"
            >{{ text(b.labelAr, b.labelEn) }}</a
          >
        </div>
      }
      @case ('cards') {
        <div class="pb__cards">
          @for (card of b.items ?? []; track $index) {
            <a class="pb__card" [attr.href]="link(card.url)">
              @if (card.icon) {
                <span class="pb__card-icon"
                  ><mat-icon fontSet="material-symbols-outlined">{{ card.icon }}</mat-icon></span
                >
              }
              <strong>{{ text(card.titleAr, card.titleEn) }}</strong>
              @if (card.textAr || card.textEn) {
                <span class="pb__muted">{{ text(card.textAr, card.textEn) }}</span>
              }
            </a>
          }
        </div>
      }
      @case ('callout') {
        <div
          class="pb__callout"
          [class]="'pb__callout pb__callout--' + (b.tone ?? 'info')"
          role="note"
        >
          <mat-icon fontSet="material-symbols-outlined">{{
            b.tone === 'warning' ? 'warning' : b.tone === 'success' ? 'check_circle' : 'info'
          }}</mat-icon>
          <dc-markdown [source]="text(b.textAr, b.textEn)" />
        </div>
      }
      @case ('divider') {
        <hr class="pb__divider" />
      }
      @case ('spacer') {
        <div [style.height.px]="b.size ?? 32"></div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
      --pb-accent: var(--dc-page-accent, var(--dga-green-700, #1b8354));
    }
    .pb__heading {
      margin: 0 0 0.5em;
      line-height: 1.35;
      color: var(--dga-gray-900, #111927);
    }
    .pb__h1 {
      font-size: 32px;
    }
    .pb__h2 {
      font-size: 24px;
    }
    .pb__h3 {
      font-size: 19px;
    }
    .pb__image {
      display: block;
      max-width: 100%;
      height: auto;
      border-radius: 8px;
    }
    .pb__image-empty {
      display: grid;
      place-items: center;
      height: 140px;
      border: 1px dashed var(--dga-gray-300, #d2d6db);
      border-radius: 8px;
      color: var(--dga-gray-400, #9da4ae);
    }
    .pb__button {
      display: inline-block;
      padding: 10px 20px;
      border-radius: 6px;
      background: var(--pb-accent);
      color: #fff;
      font-weight: 600;
      text-decoration: none;
    }
    .pb__button--secondary {
      border: 1px solid var(--pb-accent);
      background: transparent;
      color: var(--pb-accent);
    }
    .pb__cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
      gap: 16px;
    }
    .pb__card {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 18px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 12px;
      background: #fff;
      color: inherit;
      text-decoration: none;
    }
    .pb__card-icon {
      display: grid;
      place-items: center;
      width: 40px;
      height: 40px;
      border-radius: 10px;
      background: color-mix(in srgb, var(--pb-accent) 10%, #fff);
      color: var(--pb-accent);
    }
    .pb__muted {
      color: var(--dga-gray-600, #4d5761);
      font-size: 14px;
    }
    .pb__callout {
      display: flex;
      gap: 10px;
      align-items: flex-start;
      padding: 14px 16px;
      border-radius: 10px;
    }
    .pb__callout--info {
      background: #eff8ff;
      color: #175cd3;
    }
    .pb__callout--success {
      background: #ecfdf3;
      color: #067647;
    }
    .pb__callout--warning {
      background: #fffaeb;
      color: #b54708;
    }
    .pb__divider {
      border: 0;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
      margin: 8px 0;
    }
  `,
})
export class PageBlockView {
  private readonly i18n = inject(I18nService);
  readonly block = input.required<PageBlock>();
  /** In a designer: empty blocks show a placeholder (on the portal they show nothing). */
  readonly editing = input(false);
  protected readonly image = computed(() => safeImage(this.block().src));
  protected readonly align = computed(() => {
    const a = this.block().align;
    return a === 'center' ? 'center' : a === 'end' ? 'end' : 'start';
  });

  protected text(ar: string | undefined, en: string | undefined): string {
    return this.i18n.localize({ ar: ar ?? en ?? '', en: en ?? ar ?? '' });
  }

  protected link(url: string | undefined): string | null {
    return safeLink(url);
  }
}

/** A designed page: its sections (bands with columns) and their blocks. */
@Component({
  selector: 'dc-page-blocks',
  imports: [PageBlockView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (section of design().sections; track section.id) {
      <section class="pb__section" [class]="'pb__section pb__section--' + section.background">
        <div class="pb__columns" [style.grid-template-columns]="columns(section)">
          @for (column of section.columns; track $index) {
            <div class="pb__column">
              @for (block of column; track block.id) {
                <dc-page-block [block]="block" />
              }
            </div>
          }
        </div>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .pb__section {
      padding: 32px 24px;
      border-radius: 12px;
    }
    .pb__section + .pb__section {
      margin-top: 8px;
    }
    .pb__section--light {
      background: var(--dga-gray-50, #f9fafb);
    }
    .pb__section--brand {
      background: color-mix(in srgb, var(--dc-page-accent, var(--dga-green-700, #1b8354)) 8%, #fff);
    }
    .pb__section--dark {
      background: var(--dga-gray-900, #111927);
      color: #fff;
      --dga-gray-900: #fff;
      --dga-gray-600: #d2d6db;
    }
    .pb__columns {
      display: grid;
      gap: 24px;
    }
    .pb__column {
      display: flex;
      flex-direction: column;
      gap: 16px;
      min-width: 0;
    }
    @media (max-width: 720px) {
      .pb__columns {
        grid-template-columns: 1fr !important;
      }
    }
  `,
})
export class PageBlocksView {
  readonly design = input.required<PageDesign>();

  protected columns(section: PageSection): string {
    return (PAGE_LAYOUTS[section.layout] ?? [1]).map((w) => `minmax(0, ${w}fr)`).join(' ');
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { BrandingService } from '@dynamic-cube/shared';
import { PortalStore } from './portal-store';

/**
 * The title band of portal pages: the page title and its purpose in white over the entity's hero image (branding, or
 * the home setting's banner) under a dark green layer; without an image, a glass-facade pattern on the green.
 */
@Component({
  selector: 'dc-page-hero',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-page-hero" [style.--dc-hero-image]="image()" data-testid="page-hero">
      <div class="dc-page-hero__inner">
        <h1 class="dc-page-hero__title">{{ title() }}</h1>
        @if (text()) {
          <p class="dc-page-hero__text">{{ text() }}</p>
        }
        <ng-content />
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .dc-page-hero {
      --dc-facade: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='240' height='240' viewBox='0 0 240 240'%3E%3Cg fill='none' stroke='%23ffffff' stroke-opacity='0.09' stroke-width='2'%3E%3Cpath d='M0 0 L60 240 M60 0 L120 240 M120 0 L180 240 M180 0 L240 240 M-60 0 L0 240 M240 0 L300 240'/%3E%3Cpath d='M0 60 H240 M0 120 H240 M0 180 H240' stroke-opacity='0.05'/%3E%3C/g%3E%3C/svg%3E");
      position: relative;
      color: #ffffff;
      background:
        linear-gradient(90deg, rgb(7 77 49 / 70%) 0%, rgb(7 77 49 / 82%) 45%, rgb(7 77 49 / 94%) 100%), var(--dc-hero-image, var(--dc-facade)),
        linear-gradient(120deg, #0b3d2c, #146c43);
      background-size: cover, var(--dc-hero-size, 240px), cover;
      background-position: center;
    }
    .dc-page-hero[style*='--dc-hero-image'] {
      --dc-hero-size: cover;
    }
    :host-context([dir='ltr']) .dc-page-hero {
      background:
        linear-gradient(270deg, rgb(7 77 49 / 70%) 0%, rgb(7 77 49 / 82%) 45%, rgb(7 77 49 / 94%) 100%), var(--dc-hero-image, var(--dc-facade)),
        linear-gradient(240deg, #0b3d2c, #146c43);
      background-size: cover, var(--dc-hero-size, 240px), cover;
      background-position: center;
    }
    .dc-page-hero__inner {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: var(--dc-portal-width, 80rem);
      min-height: 256px;
      justify-content: center;
      margin-inline: auto;
      padding: 56px 1.5rem;
    }
    .dc-page-hero__title {
      margin: 0;
      color: #ffffff;
      font-size: clamp(30px, 5vw, 52px);
      font-weight: 700;
      line-height: 1.2;
    }
    .dc-page-hero__text {
      max-width: 52rem;
      margin: 0;
      font-size: 20px;
      line-height: 26px;
    }
    @media (max-width: 40rem) {
      .dc-page-hero__inner {
        min-height: 164px;
        padding-block: 32px;
      }
      .dc-page-hero__text {
        font-size: 16px;
        line-height: 26px;
      }
    }
  `,
})
export class PageHero {
  readonly title = input.required<string>();
  readonly text = input<string | null>(null);

  private readonly branding = inject(BrandingService).branding;
  private readonly store = inject(PortalStore);
  protected readonly image = computed(() => {
    const url = this.store.setting.value()?.bannerUrl || this.branding().portalHeroImageUrl;
    return url ? `url("${encodeURI(url)}")` : null;
  });
}

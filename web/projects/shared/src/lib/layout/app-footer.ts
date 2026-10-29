import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FooterLink } from '../apps-control/apps-control.models';
import { I18nService } from '../i18n/i18n.service';
import { LocalizedText } from '../i18n/language';
import { LocalizePipe } from '../i18n/translate.pipe';

/**
 * DGA footer on the dark green: underlined links and the copyright / ownership lines, with the entity logo (in white)
 * and the footer image (e.g. a DGA badge) at the end. Links and the entity name come from
 * Apps Control branding; the wording is the UI texts `footer.copyright` and `footer.owner` with `{name}` and `{year}`.
 */
@Component({
  selector: 'dc-app-footer',
  imports: [RouterLink, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="dc-footer">
      <div class="dc-footer__inner">
        <div class="dc-footer__text">
          @if (links().length) {
            <nav class="dc-footer__links" [attr.aria-label]="i18n.translate('footer.links')">
              @for (link of links(); track link.url) {
                @if (!isRoute(link.url)) {
                  <a
                    class="dc-footer__link"
                    [href]="link.url"
                    [attr.target]="isExternal(link.url) ? '_blank' : null"
                    [attr.rel]="isExternal(link.url) ? 'noopener' : null"
                  >
                    {{ { ar: link.labelAr, en: link.labelEn } | dcLocalize }}
                  </a>
                } @else {
                  <a class="dc-footer__link" [routerLink]="link.url">{{ { ar: link.labelAr, en: link.labelEn } | dcLocalize }}</a>
                }
              }
            </nav>
          }
          <p class="dc-footer__caption">{{ copyright() }}</p>
          <p class="dc-footer__owner">{{ owner() }}</p>
        </div>
        @if (logoUrl() || imageUrl()) {
          <div class="dc-footer__marks">
            @if (logoUrl(); as logo) {
              <img class="dc-footer__logo" [src]="logo" [alt]="i18n.localize(entityName())" data-testid="footer-logo" />
            }
            @if (imageUrl(); as image) {
              <img class="dc-footer__image" [src]="image" alt="" data-testid="footer-image" />
            }
          </div>
        }
      </div>
    </footer>
  `,
  styles: `
    .dc-footer {
      background: var(--dga-green-950);
      color: #ffffff;
      padding: 36px 0;
    }
    .dc-footer__inner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 16px 36px;
      max-width: 80rem;
      margin-inline: auto;
      padding-inline: 1.5rem;
    }
    .dc-footer__text {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .dc-footer__links {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 16px;
      margin-bottom: 8px;
    }
    .dc-footer__link {
      color: #ffffff;
      font-size: 16px;
      line-height: 24px;
      text-decoration: underline;
      text-underline-offset: 4px;
    }
    .dc-footer__link:hover {
      color: var(--dga-green-400, #54c08a);
    }
    .dc-footer__text p {
      margin: 0;
      font-size: 16px;
      line-height: 24px;
    }
    .dc-footer__caption {
      font-weight: 700;
    }
    .dc-footer__owner {
      opacity: 0.9;
    }
    .dc-footer__marks {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 16px;
    }
    /* The entity logo in white on the dark green. */
    .dc-footer__logo {
      max-height: 40px;
      max-width: 14rem;
      object-fit: contain;
      filter: brightness(0) invert(1);
    }
    .dc-footer__image {
      max-height: 40px;
      max-width: 14rem;
      object-fit: contain;
    }
  `,
})
export class AppFooter {
  readonly links = input<FooterLink[]>([]);
  readonly entityName = input<LocalizedText>({ ar: '', en: '' });
  /** The entity's footer image (e.g. a Vision 2030 or DGA badge). */
  readonly imageUrl = input<string | null | undefined>(null);
  /** The entity logo, shown in white. */
  readonly logoUrl = input<string | null | undefined>(null);

  protected readonly i18n = inject(I18nService);
  private readonly year = String(new Date().getFullYear());
  protected readonly copyright = computed(() => this.fill('footer.copyright'));
  protected readonly owner = computed(() => this.fill('footer.owner'));

  /** A page of this application, followed without reloading it. */
  protected isRoute(url: string): boolean {
    return /^\/(?!\/)[^?#]*$/.test(url);
  }

  protected isExternal(url: string): boolean {
    return /^https?:/i.test(url);
  }

  private fill(key: string): string {
    const name = this.i18n.localize(this.entityName()) || this.i18n.translate('footer.entity');
    return this.i18n.translate(key).replace('{name}', name).replace('{year}', this.year);
  }
}

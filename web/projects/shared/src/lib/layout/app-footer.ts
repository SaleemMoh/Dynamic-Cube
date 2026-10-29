import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FooterLink } from '../apps-control/apps-control.models';
import { I18nService } from '../i18n/i18n.service';
import { LocalizedText } from '../i18n/language';
import { LocalizePipe } from '../i18n/translate.pipe';

/**
 * DGA footer on the dark green: links and the copyright / ownership lines. Links and the entity name come from
 * Apps Control branding; the wording is the UI texts `footer.copyright` and `footer.owner` with `{name}` and `{year}`.
 */
@Component({
  selector: 'dc-app-footer',
  imports: [RouterLink, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="dc-footer">
      <div class="dc-footer__inner">
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
        <div class="dc-footer__legal">
          <p class="dc-footer__caption">{{ copyright() }}</p>
          <p>{{ owner() }}</p>
        </div>
      </div>
    </footer>
  `,
  styles: `
    .dc-footer {
      background: var(--dga-green-950);
      color: #ffffff;
      padding: 32px 0 24px;
    }
    .dc-footer__inner {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: 80rem;
      margin-inline: auto;
      padding-inline: 1.5rem;
    }
    .dc-footer__links {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
    }
    .dc-footer__link {
      color: #ffffff;
      font-size: 14px;
      text-decoration: none;
    }
    .dc-footer__link:hover {
      text-decoration: underline;
    }
    .dc-footer__legal p {
      margin: 0;
      font-size: 14px;
      line-height: 20px;
    }
    .dc-footer__caption {
      font-weight: 600;
    }
  `,
})
export class AppFooter {
  readonly links = input<FooterLink[]>([]);
  readonly entityName = input<LocalizedText>({ ar: '', en: '' });

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

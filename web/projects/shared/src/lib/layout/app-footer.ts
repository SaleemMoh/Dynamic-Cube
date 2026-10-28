import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '../i18n/translate.pipe';

export interface FooterLink {
  label: string;
  url: string;
}

/**
 * DGA footer on the dark green: links, copyright and ownership statement. Texts are UI-text keys
 * (`footer.copyright`, `footer.owner`) so each entity sets its own wording in Apps Control.
 */
@Component({
  selector: 'dc-app-footer',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="dc-footer">
      <div class="dc-footer__inner">
        @if (links().length) {
          <nav class="dc-footer__links" [attr.aria-label]="'footer.links' | dcTranslate">
            @for (link of links(); track link.url) {
              <a class="dc-footer__link" [href]="link.url">{{ link.label }}</a>
            }
          </nav>
        }
        <div class="dc-footer__legal">
          <p class="dc-footer__caption">{{ 'footer.copyright' | dcTranslate }} © {{ year }}</p>
          <p>{{ 'footer.owner' | dcTranslate }}</p>
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
  protected readonly year = new Date().getFullYear();
}

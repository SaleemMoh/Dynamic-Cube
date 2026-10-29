import { Clipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { I18nService, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { PortalStore } from './portal-store';

/** Outline drawings of the social networks (24 × 24, stroked in the current colour). */
const SOCIAL_ICONS: Record<string, string> = {
  X: 'M4 4l16 16M20 4L4 20',
  LINKEDIN: 'M4 4h16v16H4zM8 10v6M8 7.5v.5M12 16v-6M12 12.5c0-1.5 1-2.5 2.2-2.5S16 11 16 12.5V16',
  INSTAGRAM: 'M7 3h10a4 4 0 0 1 4 4v10a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V7a4 4 0 0 1 4-4zM12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7zM17.5 6.5v.01',
  YOUTUBE: 'M3 8a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3v8a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3zM10 9l5 3-5 3z',
  FACEBOOK: 'M14 21v-8h3l.5-3H14V8.5c0-1 .5-1.5 1.5-1.5H18V4h-3a4 4 0 0 0-4 4v2H8v3h3v8',
  SNAPCHAT: 'M12 3c3 0 5 2 5 5v3l2 1-2 1c.5 2 2 3 3 3-1 1-2.5 1-3 1.5S16 21 12 21s-4.5-2-5-2.5S5 18 4 17c1 0 2.5-1 3-3l-2-1 2-1V8c0-3 2-5 5-5z',
  TIKTOK: 'M14 3v11a3.5 3.5 0 1 1-3.5-3.5M14 3c.5 2.5 2.5 4.5 5 4.5',
  WHATSAPP: 'M4 20l1.3-4A8 8 0 1 1 8 18.7zM9 9c0 3 3 6 6 6l1-1.5-2-1-1 1c-1-.5-2-1.5-2.5-2.5l1-1-1-2z',
};

/** "Contact the entity": phone and email (copied with one click), the location and the social accounts (DGA side card). */
@Component({
  selector: 'dc-contact-card',
  imports: [MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let c = store.contact();
    @if (c.phone || c.email || c.address || c.social.length) {
      <aside class="dc-contact" data-testid="contact-card">
        <h2>{{ 'contact.title' | dcTranslate }}</h2>
        @if (c.phone; as phone) {
          <div class="dc-contact__item">
            <span class="dc-contact__label"
              ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">call</mat-icon>{{ 'contact.phone' | dcTranslate }}</span
            >
            <button type="button" class="dc-contact__value" (click)="copy(phone)" [attr.aria-label]="('contact.copy' | dcTranslate) + ' ' + phone">
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">content_copy</mat-icon><span dir="ltr">{{ phone }}</span>
            </button>
          </div>
        }
        @if (c.email; as email) {
          <div class="dc-contact__item">
            <span class="dc-contact__label"
              ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">mail</mat-icon>{{ 'contact.email' | dcTranslate }}</span
            >
            <button type="button" class="dc-contact__value" (click)="copy(email)" [attr.aria-label]="('contact.copy' | dcTranslate) + ' ' + email">
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">content_copy</mat-icon><span dir="ltr">{{ email }}</span>
            </button>
          </div>
        }
        @if (c.address; as address) {
          <div class="dc-contact__item">
            <span class="dc-contact__label"
              ><mat-icon fontSet="material-symbols-outlined" aria-hidden="true">location_on</mat-icon>{{ 'contact.location' | dcTranslate }}</span
            >
            @if (c.mapUrl; as map) {
              <a class="dc-contact__value" [href]="map" target="_blank" rel="noopener">
                <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">link</mat-icon>{{ address | dcLocalize }}
              </a>
            } @else {
              <span class="dc-contact__value">{{ address | dcLocalize }}</span>
            }
          </div>
        }
        @if (c.social.length) {
          <h3>{{ 'contact.follow' | dcTranslate }}</h3>
          <div class="dc-contact__social">
            @for (s of c.social; track s.url) {
              <a
                [href]="s.url"
                target="_blank"
                rel="noopener"
                [attr.aria-label]="'branding.network.' + s.network | dcTranslate"
                [attr.data-testid]="'social-' + s.network"
              >
                <svg viewBox="0 0 24 24" width="24" height="24" aria-hidden="true">
                  <path [attr.d]="icon(s.network)" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" />
                </svg>
              </a>
            }
          </div>
        }
      </aside>
    }
  `,
  styles: `
    .dc-contact {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 24px;
      border: 1px solid var(--dga-border);
      border-radius: 16px;
      background: #ffffff;
    }
    .dc-contact h2 {
      margin: 0;
      font-size: 20px;
      font-weight: 700;
    }
    .dc-contact h3 {
      margin: 4px 0 -8px;
      font-size: 18px;
      font-weight: 700;
    }
    .dc-contact__item {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .dc-contact__label {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      font-size: 16px;
      font-weight: 700;
    }
    .dc-contact__label mat-icon {
      color: var(--dga-green-700);
    }
    .dc-contact__value {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 0;
      border: none;
      background: none;
      color: var(--dga-green-700);
      font: inherit;
      font-size: 16px;
      text-align: start;
      text-decoration: none;
      cursor: pointer;
    }
    .dc-contact__value:hover {
      text-decoration: underline;
    }
    .dc-contact__value mat-icon {
      font-size: 20px;
      width: 20px;
      height: 16px;
    }
    .dc-contact__social {
      display: flex;
      gap: 16px;
    }
    .dc-contact__social a {
      display: inline-flex;
      color: var(--dga-text);
    }
    .dc-contact__social a:hover {
      color: var(--dga-green-700);
    }
  `,
})
export class ContactCard {
  protected readonly store = inject(PortalStore);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(I18nService);

  protected icon(network: string): string {
    return SOCIAL_ICONS[network] ?? SOCIAL_ICONS['X'];
  }

  protected copy(text: string): void {
    if (this.clipboard.copy(text)) {
      this.snackBar.open(this.i18n.translate('contact.copied'), undefined, { duration: 2500 });
    }
  }
}

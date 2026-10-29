import { BidiModule } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { BrandingService, BusinessAppSelector, I18nService, LanguageSwitcher, NotificationBell, TranslatePipe, UserMenu } from '@dynamic-cube/shared';
import { NavState } from './nav-state';

/**
 * Internal system frame: the top bar (menu toggle, system name, language, signed-in user) over the workspace. The frame
 * fills the window and the workspace scrolls inside it, so the top bar and the side navigation never move.
 */
@Component({
  selector: 'dc-internal-shell',
  imports: [BidiModule, BusinessAppSelector, MatButtonModule, MatIconModule, LanguageSwitcher, NotificationBell, TranslatePipe, UserMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-ishell" [dir]="i18n.direction()">
      <header class="dc-topbar">
        <button
          matIconButton
          type="button"
          class="dc-topbar__toggle"
          (click)="nav.toggle()"
          [attr.aria-expanded]="nav.open()"
          [attr.aria-label]="'nav.toggle' | dcTranslate"
          data-testid="nav-toggle"
        >
          <mat-icon fontSet="material-symbols-outlined">{{ nav.open() ? 'menu_open' : 'menu' }}</mat-icon>
        </button>
        <a class="dc-topbar__brand" href="/">
          @if (branding().logoUrl; as logo) {
            <img class="dc-topbar__logo" [src]="logo" [alt]="i18n.localize({ ar: branding().nameAr, en: branding().nameEn })" />
          } @else {
            <span class="dc-topbar__tile" aria-hidden="true">
              <mat-icon fontSet="material-symbols-outlined">deployed_code</mat-icon>
            </span>
          }
          <span class="dc-topbar__title">{{ title() }}</span>
        </a>
        <span class="dc-topbar__spacer"></span>
        <div class="dc-topbar__actions">
          <dc-business-app-selector />
          <dc-notification-bell />
          <dc-language-switcher />
          <dc-user-menu />
        </div>
      </header>
      <main class="dc-ishell__main">
        <ng-content />
      </main>
    </div>
  `,
  styles: `
    .dc-ishell {
      display: flex;
      flex-direction: column;
      height: 100vh;
      height: 100dvh;
      overflow: hidden;
    }
    .dc-topbar {
      position: relative;
      z-index: 10;
      flex: none;
      display: flex;
      align-items: center;
      gap: 8px;
      height: var(--shell-header-h);
      padding-inline: 12px 16px;
      background: #ffffff;
      border-bottom: 1px solid var(--dga-gray-200);
    }
    .dc-topbar__brand {
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
      color: var(--dga-gray-900);
      text-decoration: none;
    }
    .dc-topbar__tile {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: none;
      width: 36px;
      height: 36px;
      border-radius: var(--shell-radius);
      background: var(--dynamic-cube-gold-50);
      color: var(--dynamic-cube-gold-700);
    }
    .dc-topbar__logo {
      height: 36px;
      width: auto;
      max-width: 10rem;
      object-fit: contain;
    }
    .dc-topbar__title {
      overflow: hidden;
      font-size: 16px;
      font-weight: 700;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-topbar__spacer {
      flex: 1;
    }
    .dc-topbar__actions {
      display: flex;
      align-items: center;
      gap: 4px;
      --mat-button-text-label-text-color: var(--dga-gray-800);
    }
    .dc-ishell__main {
      display: flex;
      flex: 1;
      min-height: 0;
    }
    /* Phones: the logo stays, the long title goes, language and user show their icons only. */
    @media (max-width: 575.98px) {
      .dc-topbar__title {
        display: none;
      }
      .dc-topbar__actions ::ng-deep .mdc-button__label {
        font-size: 0;
      }
      .dc-topbar__actions ::ng-deep .mat-mdc-button {
        min-width: 40px;
        padding-inline: 8px;
      }
      .dc-topbar__actions ::ng-deep .mat-icon {
        margin: 0;
      }
    }
  `,
})
export class InternalShell {
  readonly title = input.required<string>();
  protected readonly i18n = inject(I18nService);
  protected readonly nav = inject(NavState);
  protected readonly branding = inject(BrandingService).branding;
}

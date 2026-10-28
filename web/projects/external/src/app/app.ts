import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ActingContextSelector, ActingPartyService, AppShell, I18nService, NotificationBell, TranslatePipe, UserMenu } from '@dynamic-cube/shared';
import { APP_TRANSLATIONS } from './translations';

@Component({
  selector: 'dc-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ActingContextSelector, AppShell, NotificationBell, TranslatePipe, UserMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-app-shell [title]="'app.title' | dcTranslate">
      <a dcShellNav class="dc-nav-link" routerLink="/services" routerLinkActive="dc-nav-link--active" data-testid="nav-services">{{ 'nav.services' | dcTranslate }}</a>
      <a dcShellNav class="dc-nav-link" routerLink="/requests" routerLinkActive="dc-nav-link--active" data-testid="nav-requests">{{ 'nav.requests' | dcTranslate }}</a>
      <a dcShellNav class="dc-nav-link" routerLink="/documents" routerLinkActive="dc-nav-link--active" data-testid="nav-documents">{{ 'documents.mine' | dcTranslate }}</a>
      <dc-acting-context-selector dcShellActions />
      <dc-notification-bell dcShellActions [scope]="acting.current()?.partyId" />
      <dc-user-menu dcShellActions />
      <router-outlet />
    </dc-app-shell>
  `,
  styles: `
    .dc-nav-link {
      display: inline-flex;
      align-items: center;
      padding-inline: 12px;
      color: var(--dga-text-1, #161616);
      font-weight: 500;
      text-decoration: none;
      border-bottom: 3px solid transparent;
    }
    .dc-nav-link--active {
      border-bottom-color: var(--dga-primary, #1b8354);
      color: var(--dga-primary-dark, #074d31);
    }
  `,
})
export class App {
  protected readonly acting = inject(ActingPartyService);

  constructor() {
    inject(I18nService).register(APP_TRANSLATIONS);
  }
}

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ActingContextSelector, ActingPartyService, AppShell, AuthService, I18nService, LocalizePipe, NotificationBell, TranslatePipe, UserMenu } from '@dynamic-cube/shared';
import { filter, map } from 'rxjs';
import { HelpButtons } from './portal/help-buttons';
import { PortalStore } from './portal/portal-store';
import { APP_TRANSLATIONS } from './translations';

@Component({
  selector: 'dc-root',
  imports: [MatIconModule, MatMenuModule, RouterOutlet, RouterLink, RouterLinkActive, ActingContextSelector, AppShell, HelpButtons, LocalizePipe, NotificationBell, TranslatePipe, UserMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-app-shell [title]="'app.title' | dcTranslate" [fluid]="isHome()">
      <a dcShellNav class="dc-nav-link" routerLink="/" routerLinkActive="dc-nav-link--active" [routerLinkActiveOptions]="{ exact: true }" data-testid="nav-home">{{ 'portal.home' | dcTranslate }}</a>
      <button dcShellNav class="dc-nav-link" [class.dc-nav-link--active]="inServices()" [matMenuTriggerFor]="categories" data-testid="nav-services">
        {{ 'nav.services' | dcTranslate }}
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">expand_more</mat-icon>
      </button>
      @if (auth.isAuthenticated()) {
        <ng-container dcShellNav>
          <a class="dc-nav-link" routerLink="/requests" routerLinkActive="dc-nav-link--active" data-testid="nav-requests">{{ 'nav.requests' | dcTranslate }}</a>
          <a class="dc-nav-link" routerLink="/documents" routerLinkActive="dc-nav-link--active" data-testid="nav-documents">{{ 'documents.mine' | dcTranslate }}</a>
        </ng-container>
      }
      <a dcShellActions class="dc-header-search" routerLink="/services" [attr.aria-label]="'portal.search' | dcTranslate" data-testid="header-search">
        <mat-icon fontSet="material-symbols-outlined">search</mat-icon>
      </a>
      <dc-acting-context-selector dcShellActions />
      <dc-notification-bell dcShellActions [scope]="acting.current()?.partyId" />
      <dc-user-menu dcShellActions />
      <router-outlet />
    </dc-app-shell>
    <dc-help-buttons />

    <mat-menu #categories="matMenu">
      <a mat-menu-item routerLink="/services" data-testid="menu-all-services">
        <mat-icon fontSet="material-symbols-outlined">apps</mat-icon>{{ 'portal.allServices' | dcTranslate }}
      </a>
      @for (c of store.categories(); track c.code) {
        <a mat-menu-item routerLink="/services" [queryParams]="{ category: c.code }" [attr.data-testid]="'menu-category-' + c.code">
          <mat-icon fontSet="material-symbols-outlined">{{ c.icon || 'folder' }}</mat-icon>{{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
        </a>
      }
    </mat-menu>
  `,
  styles: `
    .dc-nav-link {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding-inline: 12px;
      border: none;
      border-bottom: 3px solid transparent;
      background: none;
      color: var(--dga-text-1, #161616);
      font: inherit;
      font-weight: 500;
      text-decoration: none;
      cursor: pointer;
    }
    .dc-nav-link--active {
      border-bottom-color: var(--dga-primary, #1b8354);
      color: var(--dga-primary-dark, #074d31);
    }
    .dc-header-search {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 56px;
      color: var(--dga-text);
    }
    .dc-header-search:hover {
      background: var(--dga-gray-100);
    }
  `,
})
export class App {
  protected readonly acting = inject(ActingPartyService);
  protected readonly auth = inject(AuthService);
  protected readonly store = inject(PortalStore);
  private readonly url = toSignal(
    inject(Router).events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects.split(/[?#]/)[0]),
    ),
    { initialValue: '' },
  );
  /** The home page runs edge to edge (hero banner); other pages keep the centred content width. */
  protected readonly isHome = () => this.url() === '/' || this.url() === '';
  protected readonly inServices = () => this.url().startsWith('/services');

  constructor() {
    inject(I18nService).register(APP_TRANSLATIONS);
  }
}

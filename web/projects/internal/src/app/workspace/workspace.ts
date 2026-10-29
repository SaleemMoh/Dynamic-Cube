import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, effect, inject, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppsControlApi, LocalizePipe, LocalizedText, MenuGroupNode, MenuNode, NavMenu, RUNTIME_CONFIG, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, filter, map, of } from 'rxjs';
import { NavState } from '../layout/nav-state';

interface Crumb {
  label: LocalizedText;
  link?: string;
}

/**
 * Signed-in layout: the 272px side navigation from the user's Apps Control menu (beside the page on wide screens,
 * over it on narrow ones; opened and closed from the top bar), then the breadcrumb and the page.
 */
@Component({
  selector: 'dc-workspace',
  imports: [MatButtonModule, MatIconModule, MatSidenavModule, RouterLink, RouterLinkActive, RouterOutlet, NavMenu, TranslatePipe, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-sidenav-container class="dc-workspace">
      <mat-sidenav
        class="dc-workspace__nav"
        [mode]="wide() ? 'side' : 'over'"
        [opened]="nav.open()"
        (openedChange)="nav.open() !== $event && nav.set($event)"
        data-testid="side-nav"
      >
        <a
          class="dc-workspace__home"
          routerLink="/"
          routerLinkActive="dc-workspace__home--active"
          [routerLinkActiveOptions]="{ exact: true }"
          data-menu="home"
          (click)="closeIfOver()"
        >
          <mat-icon fontSet="material-symbols-outlined">dashboard</mat-icon>
          {{ 'nav.home' | dcTranslate }}
        </a>
        <dc-nav-menu [groups]="menu()" storageKey="dc.internal.nav.collapsed" (click)="closeIfOver()" />
      </mat-sidenav>
      <mat-sidenav-content class="dc-workspace__content">
        <div class="dc-workspace__page">
          @if (crumbs().length > 1) {
            <nav class="dc-breadcrumb" [attr.aria-label]="'nav.breadcrumb' | dcTranslate" data-testid="breadcrumb">
              <ol>
                @for (crumb of crumbs(); track $index; let last = $last) {
                  <li>
                    @if (crumb.link && !last) {
                      <a [routerLink]="crumb.link">{{ crumb.label | dcLocalize }}</a>
                    } @else {
                      <span [attr.aria-current]="last ? 'page' : null">{{ crumb.label | dcLocalize }}</span>
                    }
                  </li>
                }
              </ol>
            </nav>
          }
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: flex;
      flex: 1;
      min-width: 0;
      min-height: 0;
    }
    .dc-workspace {
      flex: 1;
      height: 100%;
      background: var(--dga-page);
    }
    /* The menu and the page scroll separately: opening a page never moves the menu. */
    .dc-workspace__nav {
      width: 272px;
      max-width: 85vw;
      padding-block: 6px;
      background: #ffffff;
      border-inline-end: 1px solid var(--dga-gray-200);
      --mat-sidenav-container-shape: 0;
    }
    .dc-workspace__content {
      scrollbar-gutter: stable;
    }
    .dc-workspace__home {
      display: flex;
      align-items: center;
      gap: 10px;
      height: var(--dc-nav-item-height, 38px);
      margin: 0 8px 2px;
      padding-inline: 12px;
      border-radius: var(--dc-nav-item-radius, 8px);
      color: var(--dga-gray-800);
      font-size: 14px;
      font-weight: 600;
      text-decoration: none;
    }
    .dc-workspace__home mat-icon {
      width: 20px;
      height: 20px;
      font-size: 20px;
      color: var(--dga-gray-600);
    }
    .dc-workspace__home:hover {
      background: var(--dc-nav-hover-bg, var(--dga-gray-50));
    }
    .dc-workspace__home--active {
      position: relative;
      background: var(--dc-nav-active-bg);
      color: var(--dc-nav-active-color);
    }
    .dc-workspace__home--active mat-icon {
      color: var(--dc-nav-active-icon);
    }
    .dc-workspace__home--active::after {
      content: '';
      position: absolute;
      inset-block: var(--dc-nav-edge-inset, 0);
      inset-inline-start: 0;
      width: var(--dc-nav-edge-width, 3px);
      border-radius: 9999px;
      background: var(--dc-nav-edge);
    }
    .dc-workspace__page {
      max-width: 1440px;
      margin-inline: auto;
      padding: 12px 20px 24px;
    }
    .dc-breadcrumb ol {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin: 0 0 4px;
      padding: 0;
      list-style: none;
      font-size: 13px;
      color: var(--dga-gray-500);
    }
    .dc-breadcrumb li + li::before {
      content: '/';
      margin-inline-end: 6px;
      color: var(--dga-gray-400);
    }
    .dc-breadcrumb a {
      color: var(--dga-gray-600);
      text-decoration: none;
    }
    .dc-breadcrumb a:hover {
      color: var(--dynamic-cube-gold-700);
      text-decoration: underline;
    }
    .dc-breadcrumb [aria-current] {
      color: var(--dga-gray-900);
      font-weight: 600;
    }
    @media (max-width: 575.98px) {
      .dc-workspace__page {
        padding-inline: 12px;
      }
    }
  `,
})
export class Workspace {
  private readonly config = inject(RUNTIME_CONFIG);
  private readonly router = inject(Router);
  protected readonly nav = inject(NavState);
  protected readonly wide = toSignal(
    inject(BreakpointObserver)
      .observe('(min-width: 992px)')
      .pipe(map((s) => s.matches)),
    { initialValue: true },
  );
  protected readonly menu = toSignal(
    inject(AppsControlApi)
      .menu(this.config.app)
      .pipe(catchError(() => of<MenuGroupNode[]>([]))),
    {
      initialValue: [],
    },
  );
  constructor() {
    effect(() => {
      const overlay = !this.wide();
      untracked(() => this.nav.setOverlay(overlay));
    });
  }

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  /** Home, then the menu section and item whose target is the longest prefix of the current address. */
  protected readonly crumbs = computed<Crumb[]>(() => {
    const path = this.url().split(/[?#]/)[0];
    const home: Crumb = { label: { ar: 'لوحة المعلومات', en: 'Dashboard' }, link: '/' };
    let best: { group: MenuGroupNode; item: MenuNode } | null = null;
    for (const group of this.menu()) {
      for (const item of flatten(group.items)) {
        const target = item.target ?? '';
        if (item.itemType === 'SCREEN' && target.startsWith('/') && (path === target || path.startsWith(target + '/'))) {
          if (!best || target.length > (best.item.target ?? '').length) {
            best = { group, item };
          }
        }
      }
    }
    if (!best) {
      return [home];
    }
    const crumbs: Crumb[] = [home];
    if (best.group.code) {
      crumbs.push({ label: { ar: best.group.nameAr, en: best.group.nameEn } });
    }
    crumbs.push({ label: { ar: best.item.nameAr, en: best.item.nameEn }, link: best.item.target ?? undefined });
    return crumbs;
  });

  protected closeIfOver(): void {
    if (!this.wide()) {
      this.nav.set(false);
    }
  }
}

function flatten(items: MenuNode[]): MenuNode[] {
  return items.flatMap((i) => [i, ...flatten(i.children ?? [])]);
}

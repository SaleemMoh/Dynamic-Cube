import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, effect, inject, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { AppsControlApi, LocalizePipe, LocalizedText, MenuGroupNode, MenuNode, NavMenu, RUNTIME_CONFIG, TranslatePipe } from '@dynamic-cube/shared';
import { catchError, filter, map, of } from 'rxjs';
import { NavState } from '../layout/nav-state';

interface Crumb {
  label: LocalizedText;
  link?: string;
}

/**
 * Signed-in layout: the 320px side navigation from the user's Apps Control menu (beside the page on wide screens,
 * over it on narrow ones; opened and closed from the top bar), then the breadcrumb and the page.
 */
@Component({
  selector: 'dc-workspace',
  imports: [MatButtonModule, MatIconModule, MatSidenavModule, RouterLink, RouterOutlet, NavMenu, TranslatePipe, LocalizePipe],
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
        <a matButton routerLink="/" class="dc-workspace__home" data-menu="home" (click)="closeIfOver()">
          <mat-icon fontSet="material-symbols-outlined">home</mat-icon>
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
    }
    .dc-workspace {
      flex: 1;
      background: var(--dga-page);
    }
    .dc-workspace__nav {
      width: 320px;
      max-width: 85vw;
      padding-block: 8px;
      background: #ffffff;
      border-inline-end: 1px solid var(--dga-gray-200);
      --mat-sidenav-container-shape: 0;
    }
    .dc-workspace__home {
      margin-inline: 8px;
    }
    .dc-workspace__page {
      max-width: 1440px;
      margin-inline: auto;
      padding: 16px 24px 32px;
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
  protected readonly wide = toSignal(inject(BreakpointObserver).observe('(min-width: 992px)').pipe(map((s) => s.matches)), { initialValue: true });
  protected readonly menu = toSignal(inject(AppsControlApi).menu(this.config.app).pipe(catchError(() => of<MenuGroupNode[]>([]))), {
    initialValue: [],
  });
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
    const home: Crumb = { label: { ar: 'الرئيسية', en: 'Home' }, link: '/' };
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

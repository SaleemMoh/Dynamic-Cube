import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';
import { LocalizePipe } from '../i18n/translate.pipe';
import { MenuGroupNode, MenuNode } from './apps-control.models';

/**
 * Side navigation built from the user's Apps Control menu (only items the user may view are returned by the API).
 * Each section opens and closes from its title; closed sections are remembered in the browser. One item is marked as
 * the current page: the one whose address is the longest prefix of the current address (so *Issued documents* at
 * `/documents` is not also marked on `/documents/types`).
 * Colours come from `--dc-nav-*` variables (DGA green by default) so an app can restyle it.
 */
@Component({
  selector: 'dc-nav-menu',
  imports: [NgTemplateOutlet, MatIconModule, MatListModule, RouterLink, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="dc-nav">
      @for (group of groups(); track group.code) {
        @if (group.code) {
          <button
            type="button"
            class="dc-nav__group"
            [attr.aria-expanded]="!collapsed().has(group.code)"
            [attr.aria-controls]="'dc-nav-' + group.code"
            [attr.data-nav-group]="group.code"
            (click)="toggle(group.code)"
          >
            @if (group.icon) {
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{
                group.icon
              }}</mat-icon>
            }
            <span class="dc-nav__group-name">{{
              { ar: group.nameAr, en: group.nameEn } | dcLocalize
            }}</span>
            <mat-icon class="dc-nav__chevron" fontSet="material-symbols-outlined" aria-hidden="true"
              >expand_more</mat-icon
            >
          </button>
        }
        @if (!group.code || !collapsed().has(group.code)) {
          <mat-nav-list [id]="'dc-nav-' + group.code">
            @for (item of group.items; track item.code) {
              <ng-container *ngTemplateOutlet="node; context: { $implicit: item }" />
            }
          </mat-nav-list>
        }
      }
    </nav>

    <ng-template #node let-item>
      @if (item.itemType === 'URL') {
        <a
          mat-list-item
          [href]="item.target"
          target="_blank"
          rel="noopener"
          [attr.data-menu]="item.code"
        >
          <mat-icon matListItemIcon fontSet="material-symbols-outlined">{{
            item.icon ?? 'open_in_new'
          }}</mat-icon>
          <span matListItemTitle>{{ label(item) | dcLocalize }}</span>
        </a>
      } @else if (item.itemType === 'GROUP') {
        <div class="dc-nav__subgroup">{{ label(item) | dcLocalize }}</div>
        @for (child of item.children; track child.code) {
          <ng-container *ngTemplateOutlet="node; context: { $implicit: child }" />
        }
      } @else {
        <a
          mat-list-item
          [routerLink]="item.target"
          [activated]="item.code === activeCode()"
          [attr.aria-current]="item.code === activeCode() ? 'page' : null"
          [attr.data-menu]="item.code"
        >
          <mat-icon matListItemIcon fontSet="material-symbols-outlined">{{
            item.icon ?? 'chevron_left'
          }}</mat-icon>
          <span matListItemTitle>{{ label(item) | dcLocalize }}</span>
        </a>
      }
    </ng-template>
  `,
  styles: `
    .dc-nav__group {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      width: calc(100% - 16px);
      margin: var(--dc-nav-group-margin, 0.75rem 8px 0.25rem);
      padding: 6px 8px;
      border: 0;
      border-radius: 4px;
      background: none;
      font: var(--mat-sys-title-small);
      font-size: var(--dc-nav-group-text-size, 14px);
      color: var(--dc-nav-group-color, var(--mat-sys-on-surface-variant));
      text-align: start;
      cursor: pointer;
    }
    .dc-nav__group:hover {
      background: var(--dc-nav-hover-bg, var(--dga-gray-100));
    }
    .dc-nav__group-name {
      flex: 1;
    }
    .dc-nav__chevron {
      transition: transform 150ms ease;
    }
    .dc-nav__group[aria-expanded='false'] .dc-nav__chevron {
      transform: rotate(-90deg);
    }
    :host-context([dir='rtl']) .dc-nav__group[aria-expanded='false'] .dc-nav__chevron {
      transform: rotate(90deg);
    }
    mat-nav-list {
      padding-block: 0;
      --mat-list-list-item-one-line-container-height: var(--dc-nav-item-height, 48px);
      --mat-list-list-item-label-text-size: var(--dc-nav-item-text-size, 16px);
      --mat-list-list-item-leading-icon-size: var(--dc-nav-item-icon-size, 24px);
    }
    .mat-mdc-list-item {
      border-radius: var(--dc-nav-item-radius, 4px);
      margin-inline: 8px;
      margin-block-end: 2px;
      width: auto;
    }
    /* Active item: DGA green with white text and a light selector at its start, unless the app sets --dc-nav-*. */
    .mat-mdc-list-item.mdc-list-item--activated {
      background: var(--dc-nav-active-bg, var(--dga-green-700));
      --mat-list-list-item-label-text-color: var(--dc-nav-active-color, #ffffff);
      --mat-list-list-item-hover-label-text-color: var(--dc-nav-active-color, #ffffff);
      --mat-list-list-item-focus-label-text-color: var(--dc-nav-active-color, #ffffff);
      --mat-list-list-item-leading-icon-color: var(--dc-nav-active-icon, #ffffff);
      --mat-list-list-item-hover-leading-icon-color: var(--dc-nav-active-icon, #ffffff);
      position: relative;
    }
    .mat-mdc-list-item.mdc-list-item--activated::after {
      content: '';
      position: absolute;
      inset-block: var(--dc-nav-edge-inset, 8px);
      inset-inline-start: 0;
      width: var(--dc-nav-edge-width, 4px);
      border-radius: 9999px;
      background: var(--dc-nav-edge, var(--dga-green-400));
    }
    .mat-mdc-list-item.mdc-list-item--activated .mdc-list-item__primary-text {
      font-weight: 600;
    }
    .dc-nav__subgroup {
      padding: 0.5rem 1rem 0;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NavMenu implements OnInit {
  readonly groups = input.required<MenuGroupNode[]>();
  /** Browser storage key for the closed sections. */
  readonly storageKey = input('dc.nav.collapsed');

  protected readonly collapsed = signal<ReadonlySet<string>>(new Set());

  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  /** The item of the current page: its address is the longest prefix of the current one. */
  protected readonly activeCode = computed(() => {
    const path = this.url().split(/[?#]/)[0];
    let best: MenuNode | null = null;
    const visit = (items: MenuNode[]) => {
      for (const item of items) {
        const target = item.itemType === 'SCREEN' ? (item.target ?? '').split(/[?#]/)[0] : '';
        if (
          target.startsWith('/') &&
          (path === target || path.startsWith(target.endsWith('/') ? target : target + '/'))
        ) {
          if (!best || target.length > (best.target ?? '').length) best = item;
        }
        visit(item.children ?? []);
      }
    };
    for (const group of this.groups()) visit(group.items);
    return (best as MenuNode | null)?.code ?? null;
  });

  ngOnInit(): void {
    try {
      const stored: unknown = JSON.parse(localStorage.getItem(this.storageKey()) ?? '[]');
      if (Array.isArray(stored)) {
        this.collapsed.set(new Set(stored.filter((c): c is string => typeof c === 'string')));
      }
    } catch {
      // Unreadable storage: every section starts open.
    }
  }

  protected toggle(code: string): void {
    const next = new Set(this.collapsed());
    if (!next.delete(code)) {
      next.add(code);
    }
    this.collapsed.set(next);
    try {
      localStorage.setItem(this.storageKey(), JSON.stringify([...next]));
    } catch {
      // Storage may be unavailable (private mode); sections then reset on reload.
    }
  }

  protected label(item: MenuNode) {
    return { ar: item.nameAr, en: item.nameEn };
  }
}

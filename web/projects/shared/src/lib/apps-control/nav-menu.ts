import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { LocalizePipe } from '../i18n/translate.pipe';
import { MenuGroupNode, MenuNode } from './apps-control.models';

/** Side navigation built from the user's Apps Control menu (only items the user may view are returned by the API). */
@Component({
  selector: 'dc-nav-menu',
  imports: [NgTemplateOutlet, MatIconModule, MatListModule, RouterLink, RouterLinkActive, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="dc-nav">
      @for (group of groups(); track group.code) {
        @if (group.code) {
          <h3 class="dc-nav__group">
            @if (group.icon) {
              <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">{{ group.icon }}</mat-icon>
            }
            {{ { ar: group.nameAr, en: group.nameEn } | dcLocalize }}
          </h3>
        }
        <mat-nav-list>
          @for (item of group.items; track item.code) {
            <ng-container *ngTemplateOutlet="node; context: { $implicit: item }" />
          }
        </mat-nav-list>
      }
    </nav>

    <ng-template #node let-item>
      @if (item.itemType === 'URL') {
        <a mat-list-item [href]="item.target" target="_blank" rel="noopener" [attr.data-menu]="item.code">
          <mat-icon matListItemIcon fontSet="material-symbols-outlined">{{ item.icon ?? 'open_in_new' }}</mat-icon>
          <span matListItemTitle>{{ label(item) | dcLocalize }}</span>
        </a>
      } @else if (item.itemType === 'GROUP') {
        <div class="dc-nav__subgroup">{{ label(item) | dcLocalize }}</div>
        @for (child of item.children; track child.code) {
          <ng-container *ngTemplateOutlet="node; context: { $implicit: child }" />
        }
      } @else {
        <a mat-list-item [routerLink]="item.target" routerLinkActive #active="routerLinkActive" [activated]="active.isActive" [attr.data-menu]="item.code">
          <mat-icon matListItemIcon fontSet="material-symbols-outlined">{{ item.icon ?? 'chevron_left' }}</mat-icon>
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
      margin: 1rem 1rem 0.25rem;
      font: var(--mat-sys-title-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .dc-nav__subgroup {
      padding: 0.5rem 1rem 0;
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NavMenu {
  readonly groups = input.required<MenuGroupNode[]>();

  protected label(item: MenuNode) {
    return { ar: item.nameAr, en: item.nameEn };
  }
}

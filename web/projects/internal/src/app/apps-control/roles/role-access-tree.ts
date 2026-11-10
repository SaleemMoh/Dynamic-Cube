import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ConfigRow, LocalizePipe, LocalizedText, TranslatePipe } from '@dynamic-cube/shared';
import { PermissionCards } from './permission-cards';
import { MenuAccess, filterGroups, tickGroup, tickMenu } from './role-access';
import {
  GrantRow,
  MenuChoiceGroup,
  ScreenNode,
  actionLabel,
  screenCodes,
  screenLists,
} from './role-models';
import { ScreenData } from './screen-data';

/**
 * A role's menus, screens and data in one place, like the legacy Apps Control's role menus: every app's menu tree, a
 * ticked item is shown and opens its screen; under it, the screen's actions and the values of each of its lists (all, some
 * or none — per screen, so one list can give all values here and some on another screen).
 */
@Component({
  selector: 'dc-role-access-tree',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    PermissionCards,
    ScreenData,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-tree__search">
      <mat-form-field subscriptSizing="dynamic">
        <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
        <input
          matInput
          [placeholder]="'config.search' | dcTranslate"
          (input)="search.set($any($event.target).value)"
          data-testid="role-menu-search"
        />
      </mat-form-field>
      <span class="dc-muted"
        >{{ 'roles.access.count' | dcTranslate }}: {{ access().menus.length }}</span
      >
    </div>
    @for (group of shown(); track group.code) {
      <details
        class="dc-tree__group"
        [open]="!!search().trim() || some(group)"
        [attr.data-menu-group]="group.code"
      >
        <summary>
          <mat-checkbox
            [checked]="all(group)"
            [indeterminate]="some(group) && !all(group)"
            [disabled]="readonly()"
            (change)="changed.emit(tickGroup(access(), groups(), screens(), group, $event.checked))"
            (click)="$event.stopPropagation()"
          >
            <strong>{{ group.name | dcLocalize }}</strong>
          </mat-checkbox>
          <span class="dc-muted dc-tree__count"
            >{{ ticked(group) }} / {{ group.items.length }}</span
          >
        </summary>
        <ul class="dc-tree__items">
          @for (item of group.items; track item.code) {
            <li [attr.data-testid]="'role-menu-' + item.code">
              <div class="dc-tree__item">
                <mat-checkbox
                  [checked]="isTicked(item.code)"
                  [disabled]="readonly()"
                  (change)="
                    changed.emit(tickMenu(access(), groups(), screens(), item.code, $event.checked))
                  "
                >
                  {{ item.name | dcLocalize }}
                </mat-checkbox>
                @if (isTicked(item.code) && screenOf(item.resource); as screen) {
                  <span class="dc-muted dc-tree__summary">{{ summary(screen) | dcLocalize }}</span>
                  <button
                    matIconButton
                    type="button"
                    (click)="toggleOpen(item.code)"
                    [attr.aria-expanded]="isOpen(item.code)"
                    [attr.aria-label]="'roles.access.details' | dcTranslate"
                    [attr.data-testid]="'role-menu-open-' + item.code"
                  >
                    <mat-icon fontSet="material-symbols-outlined">{{
                      isOpen(item.code) ? 'expand_less' : 'tune'
                    }}</mat-icon>
                  </button>
                }
              </div>
              @if (isTicked(item.code) && isOpen(item.code) && screenOf(item.resource); as screen) {
                <div class="dc-tree__details">
                  <dc-permission-cards
                    [screens]="[screen]"
                    [permissions]="access().permissions"
                    [readonly]="readonly()"
                    (toggled)="grant($event)"
                    (removed)="
                      changed.emit(tickMenu(access(), groups(), screens(), item.code, false))
                    "
                  />
                  <dc-screen-data
                    [screens]="[screen]"
                    [lovs]="lovs()"
                    [grants]="access().grants"
                    [readonly]="readonly()"
                    (grantsChange)="
                      changed.emit({
                        menus: access().menus,
                        permissions: access().permissions,
                        grants: $event,
                      })
                    "
                  />
                </div>
              }
            </li>
          }
        </ul>
      </details>
    } @empty {
      <p class="dc-muted">{{ 'roles.menus.none' | dcTranslate }}</p>
    }
  `,
  styles: `
    .dc-tree__search {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-bottom: 8px;
    }
    .dc-tree__search mat-form-field {
      flex: 1 1 16rem;
      max-width: 26rem;
    }
    .dc-tree__group {
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg, 8px);
      margin-bottom: 8px;
      padding: 4px 12px;
    }
    .dc-tree__group > summary {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
    }
    .dc-tree__count {
      margin-inline-start: auto;
      font-size: 13px;
    }
    .dc-tree__items {
      list-style: none;
      margin: 0 0 8px;
      padding-inline-start: 24px;
      border-inline-start: 2px solid var(--dga-gray-200);
    }
    .dc-tree__item {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 8px;
    }
    .dc-tree__summary {
      font-size: 13px;
    }
    .dc-tree__details {
      display: grid;
      gap: 12px;
      margin: 4px 0 12px;
      padding-inline-start: 28px;
    }
  `,
})
export class RoleAccessTree {
  readonly groups = input.required<MenuChoiceGroup[]>();
  readonly screens = input.required<ScreenNode[]>();
  readonly lovs = input<ConfigRow[]>([]);
  readonly access = input.required<MenuAccess>();
  readonly readonly = input(false);
  readonly changed = output<MenuAccess>();

  protected readonly tickMenu = tickMenu;
  protected readonly tickGroup = tickGroup;
  protected readonly search = signal('');
  private readonly opened = signal(new Set<string>());
  protected readonly shown = computed(() => filterGroups(this.groups(), this.search()));
  private readonly byCode = computed(() => new Map(this.screens().map((s) => [s.code, s])));

  protected isTicked(code: string): boolean {
    return this.access().menus.includes(code);
  }

  protected all(group: MenuChoiceGroup): boolean {
    return group.items.every((i) => this.isTicked(i.code));
  }

  protected some(group: MenuChoiceGroup): boolean {
    return group.items.some((i) => this.isTicked(i.code));
  }

  protected ticked(group: MenuChoiceGroup): number {
    return group.items.filter((i) => this.isTicked(i.code)).length;
  }

  protected screenOf(resource: string | null | undefined): ScreenNode | null {
    return resource ? (this.byCode().get(resource) ?? null) : null;
  }

  protected isOpen(code: string): boolean {
    return this.opened().has(code);
  }

  protected toggleOpen(code: string): void {
    this.opened.update((open) => {
      const next = new Set(open);
      if (!next.delete(code)) next.add(code);
      return next;
    });
  }

  /** "View, Edit · 2 lists limited": what the item gives, at a glance. */
  protected summary(screen: ScreenNode): LocalizedText {
    const codes = screenCodes(screen);
    const actions = [...new Set(codes.flatMap((c) => this.access().permissions[c] ?? []))].map(
      actionLabel,
    );
    const lists = screenLists(screen, this.lovs(), this.access().grants).length;
    const limited = this.access().grants.filter(
      (g) => g.resourceCode && codes.includes(g.resourceCode) && g.grantType !== 'ALL',
    ).length;
    const text = (lang: 'ar' | 'en') =>
      [
        actions.map((a) => a[lang]).join(lang === 'ar' ? '، ' : ', '),
        lists
          ? lang === 'ar'
            ? `${lists} قوائم${limited ? `، ${limited} محدودة` : ''}`
            : `${lists} lists${limited ? `, ${limited} limited` : ''}`
          : '',
      ]
        .filter(Boolean)
        .join(' · ');
    return { ar: text('ar'), en: text('en') };
  }

  protected grant(change: { resource: string; action: string; granted: boolean }): void {
    const access = this.access();
    const current = access.permissions[change.resource] ?? [];
    const actions = change.granted
      ? [...new Set([...current, change.action])]
      : current.filter((a) => a !== change.action);
    this.changed.emit({
      ...access,
      permissions: { ...access.permissions, [change.resource]: actions },
    });
  }
}

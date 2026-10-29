import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { MenuChoiceGroup } from './role-models';

/** The menu items a role shows, by menu group: a group's box ticks or clears all its items. */
@Component({
  selector: 'dc-role-menus',
  imports: [MatCheckboxModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-menus">
      @for (group of groups(); track group.code) {
        <section class="dc-menus__group" [attr.data-menu-group]="group.code">
          <mat-checkbox
            class="dc-menus__title"
            [checked]="all(group)"
            [indeterminate]="some(group) && !all(group)"
            [disabled]="readonly()"
            (change)="toggleGroup(group, $event.checked)"
          >
            {{ group.name | dcLocalize }}
          </mat-checkbox>
          <div class="dc-menus__items">
            @for (item of group.items; track item.code) {
              <mat-checkbox
                [checked]="selected().includes(item.code)"
                [disabled]="readonly()"
                (change)="toggle(item.code, $event.checked)"
                [attr.data-testid]="'role-menu-' + item.code"
              >
                {{ item.name | dcLocalize }}
              </mat-checkbox>
            }
          </div>
        </section>
      } @empty {
        <p class="dc-muted">{{ 'roles.menus.none' | dcTranslate }}</p>
      }
    </div>
  `,
  styles: `
    .dc-menus {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 12px;
    }
    .dc-menus__group {
      padding: 8px 12px 10px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg, 8px);
    }
    .dc-menus__title {
      font-weight: 700;
    }
    .dc-menus__items {
      display: grid;
      margin-inline-start: 18px;
      padding-inline-start: 6px;
      border-inline-start: 2px solid var(--dga-gray-200);
    }
  `,
})
export class RoleMenus {
  readonly groups = input.required<MenuChoiceGroup[]>();
  readonly selected = input<string[]>([]);
  readonly readonly = input(false);
  readonly selectedChange = output<string[]>();

  protected all(group: MenuChoiceGroup): boolean {
    return group.items.every((i) => this.selected().includes(i.code));
  }

  protected some(group: MenuChoiceGroup): boolean {
    return group.items.some((i) => this.selected().includes(i.code));
  }

  protected toggle(code: string, checked: boolean): void {
    const rest = this.selected().filter((c) => c !== code);
    this.selectedChange.emit(checked ? [...rest, code] : rest);
  }

  protected toggleGroup(group: MenuChoiceGroup, checked: boolean): void {
    const codes = group.items.map((i) => i.code);
    const rest = this.selected().filter((c) => !codes.includes(c));
    this.selectedChange.emit(checked ? [...rest, ...codes] : rest);
  }
}

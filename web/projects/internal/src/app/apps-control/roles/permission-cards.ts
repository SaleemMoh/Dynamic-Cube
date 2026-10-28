import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import { Permissions, ScreenNode, actionLabel } from './role-models';

/** One card per screen of a role: its actions and those of its sub-resources, ticked when granted (read-only: granted ones only). */
@Component({
  selector: 'dc-permission-cards',
  imports: [MatButtonModule, MatCheckboxModule, MatIconModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-cards">
      @for (screen of screens(); track screen.code) {
        <section class="dc-perm" [attr.data-screen]="screen.code">
          <header>
            <h4>{{ screen.name | dcLocalize }}</h4>
            @if (!readonly()) {
              <button
                matIconButton
                type="button"
                (click)="removed.emit(screen.code)"
                [attr.aria-label]="'roles.removeScreen' | dcTranslate"
                [attr.data-testid]="'remove-screen-' + screen.code"
              >
                <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
              </button>
            }
          </header>
          <div class="dc-perm__body">
            @for (node of nodes(screen); track node.code; let first = $first) {
              @if (!first) {
                <h5>{{ node.name | dcLocalize }}</h5>
              }
              @for (action of shown(node); track action) {
                <mat-checkbox
                  [checked]="granted(node.code, action)"
                  [disabled]="readonly()"
                  (change)="toggled.emit({ resource: node.code, action, granted: $event.checked })"
                  [attr.data-testid]="'perm-' + node.code + '-' + action"
                >
                  {{ label(action) | dcLocalize }}
                </mat-checkbox>
              }
            }
          </div>
        </section>
      } @empty {
        <p class="dc-empty">{{ 'roles.noScreens' | dcTranslate }}</p>
      }
    </div>
  `,
  styles: `
    .dc-cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
      gap: 16px;
    }
    .dc-perm {
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg);
      background: #ffffff;
      overflow: hidden;
    }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      min-height: 52px;
      padding: 4px 8px 4px 16px;
      border-bottom: 1px solid var(--dga-gray-200);
      background: var(--dga-gray-50);
    }
    :host-context([dir='rtl']) header {
      padding: 4px 16px 4px 8px;
    }
    h4 {
      margin: 0;
      font-size: 15px;
      font-weight: 700;
      color: var(--dga-gray-900);
    }
    h5 {
      margin: 8px 0 0;
      padding-top: 8px;
      border-top: 1px solid var(--dga-gray-100);
      font-size: 13px;
      font-weight: 600;
      color: var(--dga-gray-600);
    }
    .dc-perm__body {
      display: flex;
      flex-direction: column;
      padding: 8px 12px 12px;
    }
    .dc-perm__body mat-checkbox {
      align-self: flex-start;
    }
  `,
})
export class PermissionCards {
  readonly screens = input.required<ScreenNode[]>();
  readonly permissions = input.required<Permissions>();
  readonly readonly = input(false);
  readonly toggled = output<{ resource: string; action: string; granted: boolean }>();
  readonly removed = output<string>();

  protected readonly label = actionLabel;

  /** The screen and its sub-resources; read-only cards leave out sub-resources with nothing granted. */
  protected nodes(screen: ScreenNode): ScreenNode[] {
    return [screen, ...screen.children.filter((c) => !this.readonly() || this.permissions()[c.code]?.length)];
  }

  /** Every action when editing; only the granted ones when read-only. */
  protected shown(node: ScreenNode): string[] {
    return this.readonly() ? node.actions.filter((a) => this.granted(node.code, a)) : node.actions;
  }

  protected granted(resource: string, action: string): boolean {
    return this.permissions()[resource]?.includes(action) ?? false;
  }
}

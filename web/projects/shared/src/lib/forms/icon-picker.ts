import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { ICON_GROUPS, ICON_NAME, searchIcons } from './icons';

/**
 * Chooses a Material Symbols icon from a searchable grid grouped by subject; a name that is not listed can be typed and
 * used as is. Closes with the chosen name, or `''` to clear it.
 */
@Component({
  selector: 'dc-icon-picker',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'icons.title' | dcTranslate }}</h2>
    <mat-dialog-content class="dc-icons">
      <div class="dc-icons__bar">
        <mat-form-field subscriptSizing="dynamic" class="dc-icons__search">
          <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
          <input
            matInput
            dir="ltr"
            [placeholder]="'icons.search' | dcTranslate"
            [ngModel]="term()"
            (ngModelChange)="term.set($event)"
            data-testid="icon-search"
          />
        </mat-form-field>
        <div class="dc-icons__groups" role="tablist">
          <button type="button" class="dc-icons__group" [class.dc-icons__group--on]="!group()" (click)="group.set(null)">
            {{ 'icons.all' | dcTranslate }}
          </button>
          @for (g of groups; track g.code) {
            <button type="button" class="dc-icons__group" [class.dc-icons__group--on]="group() === g.code" (click)="group.set(g.code)">
              {{ g.label | dcLocalize }}
            </button>
          }
        </div>
      </div>
      <div class="dc-icons__grid" role="listbox" [attr.aria-label]="'icons.title' | dcTranslate">
        @for (icon of shown(); track icon) {
          <button
            type="button"
            role="option"
            class="dc-icons__item"
            [class.dc-icons__item--on]="icon === selected()"
            [attr.aria-selected]="icon === selected()"
            (click)="selected.set(icon)"
            (dblclick)="choose(icon)"
            [attr.data-icon]="icon"
          >
            <mat-icon fontSet="material-symbols-outlined">{{ icon }}</mat-icon>
            <span dir="ltr">{{ icon }}</span>
          </button>
        } @empty {
          <p class="dc-muted">{{ 'icons.none' | dcTranslate }}</p>
        }
      </div>
      @if (typed(); as name) {
        <button matButton type="button" class="dc-icons__typed" (click)="choose(name)" data-testid="icon-use-typed">
          <mat-icon fontSet="material-symbols-outlined">{{ name }}</mat-icon>
          {{ 'icons.useTyped' | dcTranslate }} <span dir="ltr">{{ name }}</span>
        </button>
      }
    </mat-dialog-content>
    <mat-dialog-actions>
      @if (current) {
        <button matButton type="button" (click)="choose('')" class="dc-icons__clear">{{ 'icons.clear' | dcTranslate }}</button>
      }
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button matButton="filled" type="button" [disabled]="!selected()" (click)="choose(selected()!)" data-testid="icon-choose">
        @if (selected(); as s) {
          <mat-icon fontSet="material-symbols-outlined">{{ s }}</mat-icon>
        }
        {{ 'icons.choose' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dc-icons {
      display: grid;
      gap: 10px;
      min-width: min(44rem, 90vw);
    }
    .dc-icons__bar {
      display: grid;
      gap: 8px;
    }
    .dc-icons__search {
      width: 100%;
    }
    .dc-icons__groups {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .dc-icons__group {
      padding: 4px 10px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 9999px;
      background: #fff;
      font: inherit;
      font-size: 12px;
      cursor: pointer;
    }
    .dc-icons__group--on {
      border-color: var(--mat-sys-primary);
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      font-weight: 600;
    }
    .dc-icons__grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(6.5rem, 1fr));
      gap: 6px;
      max-height: 22rem;
      overflow: auto;
      padding: 2px;
    }
    .dc-icons__item {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 10px 4px;
      border: 1px solid transparent;
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
      font: inherit;
      cursor: pointer;
    }
    .dc-icons__item:hover {
      border-color: var(--dga-gray-300, #d2d6db);
    }
    .dc-icons__item--on {
      border-color: var(--mat-sys-primary);
      background: var(--mat-sys-primary-container);
    }
    .dc-icons__item span {
      max-width: 100%;
      overflow: hidden;
      color: var(--dga-gray-600, #4d5761);
      font-size: 10px;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-icons__typed {
      justify-self: start;
    }
    .dc-icons__clear {
      margin-inline-end: auto;
    }
  `,
})
export class IconPicker {
  protected readonly current = inject<string | null>(MAT_DIALOG_DATA) ?? null;
  private readonly ref = inject(MatDialogRef<IconPicker, string>);
  protected readonly groups = ICON_GROUPS;
  protected readonly term = signal('');
  protected readonly group = signal<string | null>(null);
  protected readonly selected = signal<string | null>(this.current || null);

  protected readonly shown = computed(() => {
    const group = this.groups.find((g) => g.code === this.group());
    return searchIcons(this.term(), group ? group.icons : undefined);
  });

  /** A valid name typed in the search that the grid does not list, offered as is. */
  protected readonly typed = computed(() => {
    const name = this.term().trim().toLowerCase();
    return ICON_NAME.test(name) && !this.shown().includes(name) ? name : null;
  });

  protected choose(icon: string): void {
    this.ref.close(icon);
  }
}

/** Opens the icon picker; resolves with the chosen name, `''` when cleared, or undefined when cancelled. */
export function pickIcon(dialog: MatDialog, current: string | null | undefined): Observable<string | undefined> {
  return dialog
    .open<IconPicker, string | null, string>(IconPicker, { data: current ?? null, width: '48rem', maxWidth: '95vw', autoFocus: 'first-tabbable' })
    .afterClosed();
}

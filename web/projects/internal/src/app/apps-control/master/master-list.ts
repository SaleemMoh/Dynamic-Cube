import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { LocalizePipe, LocalizedText, TranslatePipe } from '@dynamic-cube/shared';

/** One row of a master list: name, code, a status pill and what may be done with it. */
export interface MasterItem {
  code: string;
  title: LocalizedText;
  /** Pill under the code, e.g. "13 values" or "Not used". */
  badge?: { label: string; tone: 'green' | 'gray' | 'gold' };
  /** System rows are shown but not changed. */
  locked?: boolean;
  canEdit: boolean;
  canDelete: boolean;
  /** Why delete is unavailable (shown on the disabled button). */
  deleteBlockedReason?: string;
}

/**
 * The start-side panel of a two-panel screen: a searchable list with a count, an add button, and view / edit /
 * delete on every row. Choosing a row shows its content in the end-side panel.
 */
@Component({
  selector: 'dc-master-list',
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTooltipModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-panel dc-master" [attr.data-testid]="testId()">
      <header class="dc-panel__header">
        <h2>
          {{ title() | dcLocalize }}
          <span class="dc-count" data-testid="master-count">{{ items().length }}</span>
        </h2>
        @if (canCreate()) {
          <button matButton="filled" (click)="add.emit()" [attr.data-testid]="testId() + '-add'">
            <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
            {{ addLabel() | dcLocalize }}
          </button>
        }
      </header>
      <div class="dc-panel__search">
        <mat-form-field subscriptSizing="dynamic">
          <mat-icon matPrefix fontSet="material-symbols-outlined">search</mat-icon>
          <input
            matInput
            [placeholder]="'config.search' | dcTranslate"
            (input)="search.set($any($event.target).value)"
            [attr.data-testid]="testId() + '-search'"
          />
        </mat-form-field>
      </div>
      <ul class="dc-master__list" role="listbox" [attr.aria-label]="title() | dcLocalize">
        @for (item of filtered(); track item.code) {
          <li
            role="option"
            tabindex="0"
            class="dc-master__item"
            [class.dc-master__item--selected]="item.code === selected()"
            [attr.aria-selected]="item.code === selected()"
            [attr.data-testid]="'master-item-' + item.code"
            (click)="selectItem.emit(item.code)"
            (keydown.enter)="selectItem.emit(item.code)"
          >
            <div class="dc-master__text">
              <span class="dc-master__title">{{ item.title | dcLocalize }}</span>
              <span class="mono dc-master__code">{{ item.code }}</span>
              @if (item.badge; as badge) {
                <span class="dc-pill" [class.dc-pill--green]="badge.tone === 'green'" [class.dc-pill--gold]="badge.tone === 'gold'">{{ badge.label }}</span>
              }
            </div>
            <div class="dc-master__actions">
              @if (item.locked) {
                <mat-icon fontSet="material-symbols-outlined" class="dc-master__lock" [matTooltip]="'config.system' | dcTranslate">lock</mat-icon>
              }
              <button
                matIconButton
                (click)="$event.stopPropagation(); view.emit(item.code)"
                [attr.aria-label]="'config.view' | dcTranslate"
                [attr.data-testid]="'view-' + item.code"
              >
                <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
              </button>
              @if (item.canEdit) {
                <button
                  matIconButton
                  (click)="$event.stopPropagation(); edit.emit(item.code)"
                  [attr.aria-label]="'config.edit' | dcTranslate"
                  [attr.data-testid]="'edit-' + item.code"
                >
                  <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                </button>
              }
              @if (item.canDelete || item.deleteBlockedReason) {
                <span [matTooltip]="item.deleteBlockedReason ?? ''" [matTooltipDisabled]="item.canDelete">
                  <button
                    matIconButton
                    [disabled]="!item.canDelete"
                    (click)="$event.stopPropagation(); remove.emit(item.code)"
                    [attr.aria-label]="'config.delete' | dcTranslate"
                    [attr.data-testid]="'delete-' + item.code"
                  >
                    <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                  </button>
                </span>
              }
            </div>
          </li>
        } @empty {
          <li class="dc-empty dc-master__empty">{{ 'config.empty' | dcTranslate }}</li>
        }
      </ul>
    </section>
  `,
  styles: `
    .dc-master__list {
      max-height: calc(100vh - 300px);
      min-height: 12rem;
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
    }
    .dc-master__item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 14px 20px;
      border-bottom: 1px solid var(--dga-gray-100);
      border-inline-start: 3px solid transparent;
      cursor: pointer;
      outline: none;
    }
    .dc-master__item:hover {
      background: var(--dga-gray-50);
    }
    .dc-master__item:focus-visible {
      box-shadow: var(--shell-focus);
    }
    .dc-master__item--selected,
    .dc-master__item--selected:hover {
      border-inline-start-color: var(--dynamic-cube-gold);
      background: var(--dynamic-cube-gold-50);
    }
    .dc-master__text {
      display: grid;
      flex: 1;
      gap: 4px;
      justify-items: start;
      min-width: 0;
    }
    .dc-master__title {
      font-size: 16px;
      font-weight: 600;
      color: var(--dga-gray-900);
    }
    .dc-master__code {
      color: var(--dga-gray-500);
      overflow-wrap: anywhere;
    }
    .dc-master__actions {
      display: flex;
      align-items: center;
      color: var(--dga-gray-600);
    }
    .dc-master__lock {
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--dga-gray-400);
    }
    .dc-master__empty {
      padding: 20px;
    }
  `,
})
export class MasterList {
  readonly title = input.required<LocalizedText>();
  readonly addLabel = input<LocalizedText>({ ar: 'إضافة', en: 'Add' });
  readonly items = input.required<MasterItem[]>();
  readonly selected = input<string | null>(null);
  readonly canCreate = input(false);
  readonly testId = input('master');
  readonly selectItem = output<string>();
  readonly view = output<string>();
  readonly edit = output<string>();
  readonly remove = output<string>();
  readonly add = output<void>();

  protected readonly search = signal('');
  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return term ? this.items().filter((i) => `${i.code} ${i.title.ar} ${i.title.en}`.toLowerCase().includes(term)) : this.items();
  });

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    // The chosen row stays in sight when the list (re)loads, e.g. after opening a row far down the list.
    afterRenderEffect(() => {
      const code = this.selected();
      if (!code || !this.filtered().some((i) => i.code === code)) return;
      const row = this.host.nativeElement.querySelector<HTMLElement>(`[data-testid="master-item-${CSS.escape(code)}"]`);
      row?.scrollIntoView({ block: 'nearest' });
    });
  }
}

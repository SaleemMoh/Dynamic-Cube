import { ChangeDetectionStrategy, Component, computed, forwardRef, inject, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService } from '../i18n/i18n.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { LocalizedText } from '../i18n/language';

/** An item of a {@link DualList}: its value, label and an optional second line. */
export interface DualListOption {
  value: string;
  label: LocalizedText;
  hint?: LocalizedText | null;
}

/**
 * Chooses several items by moving them between two searchable lists: what is available and what is chosen. A click
 * picks an item, a double click moves it; the arrows move the picked items or all shown items. The value is the chosen
 * values, in the order they were added. Works as a form control.
 */
@Component({
  selector: 'dc-dual-list',
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTooltipModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DualList), multi: true }],
  template: `
    <div class="dc-dual" [class.dc-dual--readonly]="disabled()">
      @if (!disabled()) {
        <section class="dc-dual__list" [attr.data-testid]="testId() + '-available'">
          <header>{{ availableLabel() ?? ('dualList.available' | dcTranslate) }}</header>
          <div class="dc-dual__search">
            <mat-form-field subscriptSizing="dynamic">
              <input matInput [placeholder]="'dualList.search' | dcTranslate" (input)="availableSearch.set($any($event.target).value)" />
              <mat-icon matSuffix fontSet="material-symbols-outlined">search</mat-icon>
            </mat-form-field>
          </div>
          <ul role="listbox" aria-multiselectable="true" [attr.aria-label]="availableLabel() ?? ('dualList.available' | dcTranslate)">
            @for (item of available(); track item.value) {
              <li
                role="option"
                tabindex="0"
                [attr.aria-selected]="pickedAvailable().has(item.value)"
                [class.dc-picked]="pickedAvailable().has(item.value)"
                (click)="toggle(pickedAvailable, item.value)"
                (keydown.enter)="toggle(pickedAvailable, item.value)"
                (dblclick)="add([item.value])"
                [attr.data-testid]="testId() + '-available-' + item.value"
              >
                <strong>{{ item.label | dcLocalize }}</strong>
                @if (item.hint) {
                  <span>{{ item.hint | dcLocalize }}</span>
                }
              </li>
            } @empty {
              <li class="dc-empty">{{ 'dualList.none' | dcTranslate }}</li>
            }
          </ul>
        </section>
        <div class="dc-dual__moves">
          <button
            matIconButton
            type="button"
            (click)="add(shownPicked(pickedAvailable(), available()))"
            [disabled]="!pickedAvailable().size"
            [matTooltip]="'dualList.addPicked' | dcTranslate"
            [attr.data-testid]="testId() + '-add'"
          >
            <mat-icon fontSet="material-symbols-outlined">{{ forward() }}</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            (click)="add(values(available()))"
            [disabled]="!available().length"
            [matTooltip]="'dualList.addAll' | dcTranslate"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              forward() === 'chevron_right' ? 'keyboard_double_arrow_right' : 'keyboard_double_arrow_left'
            }}</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            (click)="remove(shownPicked(pickedChosen(), chosen()))"
            [disabled]="!pickedChosen().size"
            [matTooltip]="'dualList.removePicked' | dcTranslate"
            [attr.data-testid]="testId() + '-remove'"
          >
            <mat-icon fontSet="material-symbols-outlined">{{ back() }}</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            (click)="remove(values(chosen()))"
            [disabled]="!chosen().length"
            [matTooltip]="'dualList.removeAll' | dcTranslate"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              back() === 'chevron_left' ? 'keyboard_double_arrow_left' : 'keyboard_double_arrow_right'
            }}</mat-icon>
          </button>
        </div>
      }
      <section class="dc-dual__list" [attr.data-testid]="testId() + '-chosen'">
        <header>
          {{ chosenLabel() ?? ('dualList.chosen' | dcTranslate) }} <span class="dc-count">{{ value().length }}</span>
        </header>
        <div class="dc-dual__search">
          <mat-form-field subscriptSizing="dynamic">
            <input matInput [placeholder]="'dualList.search' | dcTranslate" (input)="chosenSearch.set($any($event.target).value)" />
            <mat-icon matSuffix fontSet="material-symbols-outlined">search</mat-icon>
          </mat-form-field>
        </div>
        <ul role="listbox" aria-multiselectable="true" [attr.aria-label]="chosenLabel() ?? ('dualList.chosen' | dcTranslate)">
          @for (item of chosen(); track item.value) {
            <li
              role="option"
              tabindex="0"
              [attr.aria-selected]="pickedChosen().has(item.value)"
              [class.dc-picked]="pickedChosen().has(item.value)"
              (click)="!disabled() && toggle(pickedChosen, item.value)"
              (keydown.enter)="!disabled() && toggle(pickedChosen, item.value)"
              (dblclick)="!disabled() && remove([item.value])"
              [attr.data-testid]="testId() + '-chosen-' + item.value"
            >
              <strong>{{ item.label | dcLocalize }}</strong>
              @if (item.hint) {
                <span>{{ item.hint | dcLocalize }}</span>
              }
            </li>
          } @empty {
            <li class="dc-empty">{{ 'dualList.noneChosen' | dcTranslate }}</li>
          }
        </ul>
      </section>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .dc-dual {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
      gap: 16px;
      align-items: stretch;
    }
    .dc-dual--readonly {
      grid-template-columns: minmax(0, 1fr);
    }
    .dc-dual__list {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg, 8px);
      overflow: hidden;
    }
    .dc-dual__list header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 14px;
      border-bottom: 1px solid var(--dga-gray-200);
      background: var(--dga-gray-50);
      font-weight: 600;
    }
    .dc-count {
      padding: 0 8px;
      border-radius: 9999px;
      background: var(--dga-gray-200);
      font-size: 12px;
    }
    .dc-dual__search {
      padding: 8px 10px;
      border-bottom: 1px solid var(--dga-gray-100);
    }
    .dc-dual__search mat-form-field {
      width: 100%;
    }
    .dc-dual__list ul {
      height: 14rem;
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
    }
    .dc-dual__list li {
      display: grid;
      gap: 2px;
      padding: 8px 14px;
      border-inline-start: 3px solid transparent;
      cursor: pointer;
      user-select: none;
    }
    .dc-dual__list li:hover {
      background: var(--dga-gray-50);
    }
    .dc-dual__list li:focus-visible {
      outline: none;
      box-shadow: var(--shell-focus);
    }
    .dc-dual__list li.dc-picked {
      border-inline-start-color: var(--dynamic-cube-gold, var(--mat-sys-primary));
      background: var(--dynamic-cube-gold-50, var(--dga-gray-100));
    }
    .dc-dual__list li strong {
      font-weight: 500;
      color: var(--dga-gray-900);
    }
    .dc-dual__list li span {
      font-size: 13px;
      color: var(--dga-gray-500);
    }
    .dc-dual__list li.dc-empty {
      cursor: default;
      color: var(--dga-gray-500);
    }
    .dc-dual__moves {
      display: flex;
      flex-direction: column;
      justify-content: center;
      gap: 8px;
    }
    .dc-dual__moves button {
      border: 1px solid var(--dga-gray-300);
      border-radius: var(--shell-radius, 4px);
    }
    @media (max-width: 760px) {
      .dc-dual {
        grid-template-columns: minmax(0, 1fr);
      }
      .dc-dual__moves {
        flex-direction: row;
        justify-content: center;
      }
    }
  `,
})
export class DualList implements ControlValueAccessor {
  readonly options = input<DualListOption[]>([]);
  readonly availableLabel = input<string | null>(null);
  readonly chosenLabel = input<string | null>(null);
  /** Prefix of the lists' and items' `data-testid`. */
  readonly testId = input('dual');

  private readonly i18n = inject(I18nService);
  protected readonly value = signal<string[]>([]);
  protected readonly disabled = signal(false);
  protected readonly availableSearch = signal('');
  protected readonly chosenSearch = signal('');
  protected readonly pickedAvailable = signal(new Set<string>());
  protected readonly pickedChosen = signal(new Set<string>());

  private readonly byValue = computed(() => new Map(this.options().map((o) => [o.value, o])));
  protected readonly available = computed(() =>
    this.filter(
      this.options().filter((o) => !this.value().includes(o.value)),
      this.availableSearch(),
    ),
  );
  /** Chosen items in the order added; a value without an option still shows, by its code. */
  protected readonly chosen = computed(() =>
    this.filter(
      this.value().map((v) => this.byValue().get(v) ?? { value: v, label: { ar: v, en: v } }),
      this.chosenSearch(),
    ),
  );
  protected readonly forward = computed(() => (this.i18n.direction() === 'rtl' ? 'chevron_left' : 'chevron_right'));
  protected readonly back = computed(() => (this.i18n.direction() === 'rtl' ? 'chevron_right' : 'chevron_left'));

  private onChange: (value: string[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: unknown): void {
    this.value.set(Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : []);
  }

  registerOnChange(fn: (value: string[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected values(items: DualListOption[]): string[] {
    return items.map((i) => i.value);
  }

  /** The picked values still shown (a search may hide some). */
  protected shownPicked(picked: Set<string>, shown: DualListOption[]): string[] {
    return shown.filter((i) => picked.has(i.value)).map((i) => i.value);
  }

  protected toggle(list: typeof this.pickedAvailable, value: string): void {
    const next = new Set(list());
    if (!next.delete(value)) next.add(value);
    list.set(next);
  }

  protected add(values: string[]): void {
    const added = values.filter((v) => !this.value().includes(v));
    if (!added.length) return;
    this.set([...this.value(), ...added]);
    this.pickedAvailable.set(new Set());
  }

  protected remove(values: string[]): void {
    if (!values.length) return;
    this.set(this.value().filter((v) => !values.includes(v)));
    this.pickedChosen.set(new Set());
  }

  private set(value: string[]): void {
    this.value.set(value);
    this.onChange(value);
    this.onTouched();
  }

  private filter(items: DualListOption[], search: string): DualListOption[] {
    const term = search.trim().toLowerCase();
    return term ? items.filter((i) => `${i.value} ${i.label.ar} ${i.label.en} ${i.hint?.ar ?? ''} ${i.hint?.en ?? ''}`.toLowerCase().includes(term)) : items;
  }
}

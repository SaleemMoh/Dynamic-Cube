import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { LocalizedText } from '../i18n/language';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { FormOption } from './form-schema';

/**
 * A column of a table, or a property of an object: `tags` is a list of words typed with commas, `value` is text or
 * JSON (a JSONLogic expression such as `{"var": "task.assignee"}`), `table` a nested list of rows, `object` nested
 * properties (its `columns`), `map` free names and values.
 */
export interface StructuredColumn {
  key: string;
  label: LocalizedText;
  type: 'text' | 'number' | 'checkbox' | 'select' | 'date' | 'tags' | 'value' | 'table' | 'object' | 'map';
  options?: FormOption[];
  /** Options of a `select` column are numbers (e.g. week days). */
  numeric?: boolean;
  required?: boolean;
  /** Value for new rows. */
  defaultValue?: unknown;
  /** Left-to-right content (codes, paths, URLs). */
  ltr?: boolean;
  /** For `table` and `object` columns. */
  columns?: StructuredColumn[];
  /** The options are the rows of a configuration type (codes), filled in by the screen that shows the field. */
  ref?: string;
  /** The options are the values of a class, filled in by the screen that shows the field. */
  refDomains?: string;
  /** The options come from an API returning `{ code, nameAr, nameEn }[]`, filled in by the screen that shows the field. */
  optionsUrl?: string;
}

/** Columns of a shape (nested tables included) whose options come from `ref` or `refDomains`. */
export function lookupColumns(shape: StructuredShape | undefined): StructuredColumn[] {
  if (!shape) return [];
  const own = shape.kind === 'table' ? shape.columns : shape.kind === 'object' ? shape.properties : [];
  return own.flatMap((c) => [...(c.ref || c.refDomains || c.optionsUrl ? [c] : []), ...(c.columns ? lookupColumns({ kind: 'table', columns: c.columns }) : [])]);
}

/** The shape with the options of its lookup columns filled in (by `ref` or `refDomains`). */
export function withColumnOptions(shape: StructuredShape, options: (column: StructuredColumn) => FormOption[] | undefined): StructuredShape {
  const fill = (columns: StructuredColumn[]): StructuredColumn[] =>
    columns.map((c) => ({
      ...c,
      options: (c.ref || c.refDomains || c.optionsUrl ? options(c) : undefined) ?? c.options,
      columns: c.columns ? fill(c.columns) : undefined,
    }));
  return shape.kind === 'table'
    ? { ...shape, columns: fill(shape.columns) }
    : shape.kind === 'object'
      ? { ...shape, properties: fill(shape.properties) }
      : shape;
}

/**
 * What a JSON value holds: `list` (texts or numbers, or a choice of options), `table` (rows with columns), `object`
 * (fixed properties), `map` (free names and values, e.g. attributes), `json` (anything, e.g. a JSONLogic rule).
 */
export type StructuredShape =
  | { kind: 'list'; item: 'text' | 'number'; options?: FormOption[] }
  | { kind: 'table'; columns: StructuredColumn[] }
  | { kind: 'object'; properties: StructuredColumn[] }
  | { kind: 'map'; keyLabel?: LocalizedText; valueLabel?: LocalizedText }
  | { kind: 'json' };

type Row = Record<string, unknown>;

/** A value as the JSON view shows it. */
export function jsonText(value: unknown): string {
  return value === null || value === undefined || value === '' ? '' : JSON.stringify(value, null, 2);
}

/** A `value` column: JSON when it reads as JSON (an object, list, number or true/false), else the text itself. */
export function parseLoose(text: string): unknown {
  const trimmed = text.trim();
  if (!trimmed) return null;
  if (/^[[{]/.test(trimmed) || /^(true|false|-?\d+(\.\d+)?)$/.test(trimmed)) {
    try {
      return JSON.parse(trimmed);
    } catch {
      return text;
    }
  }
  return text;
}

export function looseText(value: unknown): string {
  return value === null || value === undefined ? '' : typeof value === 'string' ? value : JSON.stringify(value);
}

/** Rows for editing a map (`{ a: 1 }` → `[{ key: 'a', value: '1' }]`) and back. */
export function mapRows(value: unknown): Row[] {
  return value && typeof value === 'object' && !Array.isArray(value) ? Object.entries(value as Row).map(([key, v]) => ({ key, value: looseText(v) })) : [];
}

export function rowsMap(rows: Row[]): Row {
  return Object.fromEntries(rows.filter((r) => String(r['key'] ?? '').trim()).map((r) => [String(r['key']).trim(), parseLoose(String(r['value'] ?? ''))]));
}

/**
 * Edits a JSON value through a form made for its shape (rows of a table, items of a list, properties of an object) — no
 * JSON to write. The JSON view shows the same value and accepts pasted JSON.
 */
@Component({
  selector: 'dc-structured-input',
  imports: [
    NgTemplateOutlet,
    forwardRef(() => StructuredInput),
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatIconModule,
    MatSelectModule,
    MatTooltipModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => StructuredInput), multi: true }],
  template: `
    <div class="dc-si" [attr.data-testid]="testId()">
      @if (shape().kind !== 'json') {
        <mat-button-toggle-group class="dc-si__mode" [ngModel]="mode()" (ngModelChange)="setMode($event)" hideSingleSelectionIndicator>
          <mat-button-toggle value="form">{{ 'structured.form' | dcTranslate }}</mat-button-toggle>
          <mat-button-toggle value="json" [attr.data-testid]="testId() ? testId() + '-json' : null">JSON</mat-button-toggle>
        </mat-button-toggle-group>
      }
      @if (mode() === 'json' || shape().kind === 'json') {
        <textarea
          class="dc-si__json"
          dir="ltr"
          spellcheck="false"
          rows="6"
          [ngModel]="text()"
          (ngModelChange)="typed($event)"
          (blur)="touched()"
          [disabled]="disabled()"
          [attr.data-testid]="testId() ? testId() + '-text' : null"
        ></textarea>
        @if (jsonError()) {
          <small class="dc-si__error" role="alert">{{ 'form.invalidJson' | dcTranslate }}</small>
        }
      } @else {
        @switch (shape().kind) {
          @case ('list') {
            @if (listOptions(); as options) {
              <div class="dc-si__checks">
                @for (o of options; track o.value) {
                  <mat-checkbox [checked]="listHas(o.value)" (change)="listToggle(o.value, $event.checked)" [disabled]="disabled()">{{
                    o.label | dcLocalize
                  }}</mat-checkbox>
                }
              </div>
            } @else {
              <div class="dc-si__chips">
                @for (item of listItems(); track $index) {
                  <span class="dc-si__chip" dir="ltr">
                    {{ item }}
                    <button type="button" (click)="listRemove($index)" [disabled]="disabled()" [attr.aria-label]="'structured.remove' | dcTranslate">
                      <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                    </button>
                  </span>
                }
                <input
                  class="dc-si__chip-input"
                  dir="ltr"
                  [type]="listNumeric() ? 'number' : 'text'"
                  [placeholder]="'structured.addItem' | dcTranslate"
                  (keydown.enter)="$event.preventDefault(); listAdd($any($event.target))"
                  (blur)="listAdd($any($event.target)); touched()"
                  [disabled]="disabled()"
                  [attr.data-testid]="testId() ? testId() + '-add' : null"
                />
              </div>
            }
          }
          @case ('object') {
            <div class="dc-si__object">
              @for (c of columns(); track c.key) {
                <div class="dc-si__prop" [class.dc-si__prop--wide]="c.type === 'table' || c.type === 'object' || c.type === 'map'">
                  <span
                    >{{ c.label | dcLocalize }}
                    @if (c.required) {
                      <span class="dc-required">*</span>
                    }
                  </span>
                  <ng-container *ngTemplateOutlet="cell; context: { $implicit: objectValue(), column: c, index: -1 }" />
                </div>
              }
            </div>
          }
          @default {
            <div class="dc-si__table-wrap">
              <table class="dc-si__table">
                <thead>
                  <tr>
                    @for (c of columns(); track c.key) {
                      <th>
                        {{ c.label | dcLocalize }}
                        @if (c.required) {
                          <span class="dc-required">*</span>
                        }
                      </th>
                    }
                    <th class="dc-si__actions"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track $index; let i = $index) {
                    <tr [attr.data-row]="i">
                      @for (c of columns(); track c.key) {
                        <td [class.dc-si__td-check]="c.type === 'checkbox'">
                          <ng-container *ngTemplateOutlet="cell; context: { $implicit: row, column: c, index: i }" />
                        </td>
                      }
                      <td class="dc-si__actions">
                        <button
                          type="button"
                          class="dc-si__icon"
                          (click)="move(i, -1)"
                          [disabled]="disabled() || i === 0"
                          [matTooltip]="'structured.up' | dcTranslate"
                        >
                          <mat-icon fontSet="material-symbols-outlined">arrow_upward</mat-icon>
                        </button>
                        <button
                          type="button"
                          class="dc-si__icon"
                          (click)="removeRow(i)"
                          [disabled]="disabled()"
                          [matTooltip]="'structured.remove' | dcTranslate"
                        >
                          <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                        </button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td [attr.colspan]="columns().length + 1" class="dc-si__empty">{{ 'structured.empty' | dcTranslate }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <button
              matButton
              type="button"
              class="dc-si__add"
              (click)="addRow()"
              [disabled]="disabled()"
              [attr.data-testid]="testId() ? testId() + '-add' : null"
            >
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ 'structured.addRow' | dcTranslate }}
            </button>
          }
        }
      }
    </div>

    <ng-template #cell let-row let-c="column" let-i="index">
      @switch (c.type) {
        @case ('checkbox') {
          <mat-checkbox
            [checked]="!!row[c.key]"
            (change)="setCell(i, c.key, $event.checked)"
            [disabled]="disabled()"
            [attr.aria-label]="c.label | dcLocalize"
          />
        }
        @case ('select') {
          <mat-select
            class="dc-si__select"
            [value]="selectValue(c, row[c.key])"
            (selectionChange)="setCell(i, c.key, $event.value)"
            [disabled]="disabled()"
            [attr.aria-label]="c.label | dcLocalize"
          >
            @if (!c.required) {
              <mat-option [value]="null">—</mat-option>
            }
            @for (o of selectOptions(c, row[c.key]); track o.value) {
              <mat-option [value]="o.value">{{ o.label | dcLocalize }}</mat-option>
            }
          </mat-select>
        }
        @case ('number') {
          <input
            class="dc-si__input"
            type="number"
            dir="ltr"
            [value]="row[c.key] ?? ''"
            (change)="setNumber(i, c.key, $any($event.target).value)"
            (blur)="touched()"
            [disabled]="disabled()"
            [attr.aria-label]="c.label | dcLocalize"
          />
        }
        @case ('date') {
          <input
            class="dc-si__input"
            type="date"
            dir="ltr"
            [value]="row[c.key] ?? ''"
            (change)="setCell(i, c.key, $any($event.target).value || null)"
            (blur)="touched()"
            [disabled]="disabled()"
            [attr.aria-label]="c.label | dcLocalize"
          />
        }
        @case ('tags') {
          <input
            class="dc-si__input"
            dir="ltr"
            [value]="tagsText(row[c.key])"
            (change)="setTags(i, c.key, $any($event.target).value)"
            (blur)="touched()"
            [disabled]="disabled()"
            [placeholder]="'structured.commas' | dcTranslate"
            [attr.aria-label]="c.label | dcLocalize"
          />
        }
        @case ('value') {
          <textarea
            class="dc-si__input dc-si__expr"
            rows="1"
            spellcheck="false"
            dir="ltr"
            [value]="loose(row[c.key])"
            (change)="setLoose(i, c.key, $any($event.target).value)"
            (blur)="touched()"
            [disabled]="disabled()"
            [attr.aria-label]="c.label | dcLocalize"
          ></textarea>
        }
        @case ('table') {
          <dc-structured-input
            class="dc-si__nested"
            [shape]="{ kind: 'table', columns: c.columns ?? [] }"
            [ngModel]="row[c.key] ?? []"
            (ngModelChange)="setCell(i, c.key, $event)"
            [disabled]="disabled()"
          />
        }
        @case ('object') {
          <dc-structured-input
            class="dc-si__nested"
            [shape]="{ kind: 'object', properties: c.columns ?? [] }"
            [ngModel]="row[c.key] ?? {}"
            (ngModelChange)="setCell(i, c.key, $event)"
            [disabled]="disabled()"
          />
        }
        @case ('map') {
          <dc-structured-input
            class="dc-si__nested"
            [shape]="{ kind: 'map' }"
            [ngModel]="row[c.key] ?? {}"
            (ngModelChange)="setCell(i, c.key, $event)"
            [disabled]="disabled()"
          />
        }
        @default {
          <input
            class="dc-si__input"
            [attr.dir]="c.ltr ? 'ltr' : null"
            [value]="row[c.key] ?? ''"
            (change)="setCell(i, c.key, $any($event.target).value || null)"
            (blur)="touched()"
            [disabled]="disabled()"
            [attr.aria-label]="c.label | dcLocalize"
          />
        }
      }
    </ng-template>
  `,
  styles: `
    .dc-si {
      display: grid;
      gap: 6px;
    }
    .dc-si__mode {
      justify-self: end;
      height: 28px;
      align-items: center;
      font-size: 12px;
    }
    .dc-si__json {
      width: 100%;
      box-sizing: border-box;
      padding: 8px 10px;
      border: 1px solid var(--dga-gray-300, #d0d5dd);
      border-radius: var(--shell-radius, 8px);
      font-family: ui-monospace, monospace;
      font-size: 13px;
      resize: vertical;
    }
    .dc-si__error {
      color: var(--dga-error-600, #d92d20);
    }
    .dc-si__table-wrap {
      overflow-x: auto;
      border: 1px solid var(--dga-gray-200, #eaecf0);
      border-radius: var(--shell-radius, 8px);
    }
    .dc-si__table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .dc-si__table th {
      padding: 6px 8px;
      background: var(--dga-gray-50, #f9fafb);
      color: var(--dga-gray-600, #475467);
      font-weight: 600;
      text-align: start;
      white-space: nowrap;
    }
    .dc-si__table td {
      padding: 3px 4px;
      border-top: 1px solid var(--dga-gray-100, #f2f4f7);
      vertical-align: middle;
    }
    .dc-si__td-check {
      text-align: center;
    }
    .dc-si__input,
    .dc-si__select {
      width: 100%;
      min-width: 6rem;
      box-sizing: border-box;
      padding: 6px 8px;
      border: 1px solid var(--dga-gray-200, #eaecf0);
      border-radius: 6px;
      background: #fff;
      font: inherit;
    }
    .dc-si__expr {
      display: block;
      field-sizing: content;
      min-height: 2rem;
      max-height: 12rem;
      resize: vertical;
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
    .dc-si__input:focus,
    .dc-si__select:focus {
      outline: 2px solid var(--dynamic-cube-gold, #b08d57);
      outline-offset: -1px;
    }
    .dc-si__actions {
      width: 1%;
      white-space: nowrap;
    }
    .dc-si__icon {
      display: inline-grid;
      place-items: center;
      width: 28px;
      height: 28px;
      border: 0;
      border-radius: 6px;
      background: none;
      color: var(--dga-gray-500, #667085);
      cursor: pointer;
    }
    .dc-si__icon:hover:not(:disabled) {
      background: var(--dga-gray-100, #f2f4f7);
    }
    .dc-si__icon mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
    }
    .dc-si__empty {
      padding: 10px !important;
      color: var(--dga-gray-400, #98a2b3);
    }
    .dc-si__add {
      justify-self: start;
    }
    .dc-si__object {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 8px 12px;
    }
    .dc-si__prop--wide {
      grid-column: 1 / -1;
    }
    .dc-si__prop {
      display: grid;
      gap: 4px;
      font-size: 13px;
      color: var(--dga-gray-600, #475467);
    }
    .dc-si__checks {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 12px;
    }
    .dc-si__chips {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
      padding: 6px;
      border: 1px solid var(--dga-gray-300, #d0d5dd);
      border-radius: var(--shell-radius, 8px);
    }
    .dc-si__chip {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding: 2px 4px 2px 10px;
      border-radius: 9999px;
      background: var(--dga-gray-100, #f2f4f7);
      font-size: 13px;
    }
    .dc-si__chip button {
      display: grid;
      place-items: center;
      width: 20px;
      height: 20px;
      padding: 0;
      border: 0;
      border-radius: 9999px;
      background: none;
      cursor: pointer;
    }
    .dc-si__chip mat-icon {
      width: 14px;
      height: 14px;
      font-size: 14px;
    }
    .dc-si__chip-input {
      flex: 1;
      min-width: 8rem;
      padding: 4px;
      border: 0;
      outline: none;
      font: inherit;
    }
    .dc-si__nested {
      min-width: 22rem;
    }
  `,
})
export class StructuredInput implements ControlValueAccessor {
  readonly shape = input.required<StructuredShape>();
  readonly testId = input<string | null>(null);

  protected readonly value = signal<unknown>(null);
  protected readonly mode = signal<'form' | 'json'>('form');
  protected readonly text = signal('');
  protected readonly jsonError = signal(false);
  protected readonly disabled = signal(false);
  /** Rows of a map while it is edited (a row may have no name yet). */
  private readonly mapState = signal<Row[]>([]);
  private onChange: (value: unknown) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: unknown): void {
    // The value this input just sent comes back from the form: keep what is being typed.
    if (JSON.stringify(value ?? null) === JSON.stringify(this.value())) return;
    this.value.set(value ?? null);
    this.text.set(jsonText(value));
    this.mapState.set(mapRows(value));
    this.jsonError.set(false);
  }

  registerOnChange(fn: (value: unknown) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }

  protected touched(): void {
    this.onTouched();
  }

  protected setMode(mode: 'form' | 'json'): void {
    if (mode === 'form' && this.jsonError()) return;
    this.text.set(jsonText(this.value()));
    this.mode.set(mode);
  }

  /** Typed JSON is applied once it parses; until then the form keeps the last valid value. */
  protected typed(text: string): void {
    this.text.set(text);
    if (!text.trim()) {
      this.jsonError.set(false);
      this.emit(this.shape().kind === 'json' ? null : this.empty(), { text: false });
      return;
    }
    try {
      const parsed = JSON.parse(text);
      this.jsonError.set(false);
      this.emit(parsed, { text: false });
    } catch {
      this.jsonError.set(true);
    }
  }

  protected columns(): StructuredColumn[] {
    const shape = this.shape();
    return shape.kind === 'table' ? shape.columns : shape.kind === 'object' ? shape.properties : shape.kind === 'map' ? this.mapColumns(shape) : [];
  }

  protected rows(): Row[] {
    const shape = this.shape();
    if (shape.kind === 'map') return this.mapState();
    const value = this.value();
    return Array.isArray(value) ? (value as Row[]).map((r) => (r && typeof r === 'object' ? r : {})) : [];
  }

  protected objectValue(): Row {
    const value = this.value();
    return value && typeof value === 'object' && !Array.isArray(value) ? (value as Row) : {};
  }

  protected selectOptions(column: StructuredColumn, current: unknown): FormOption[] {
    const options = column.options ?? [];
    if (current === null || current === undefined || current === '' || options.some((o) => this.same(o.value, current, column))) return options;
    return [...options, { value: String(current), label: { ar: String(current), en: String(current) } }];
  }

  protected selectValue(column: StructuredColumn, value: unknown): unknown {
    return value === null || value === undefined ? null : column.numeric ? String(value) : value;
  }

  protected setCell(index: number, key: string, value: unknown): void {
    const column = this.columns().find((c) => c.key === key);
    const stored = column?.type === 'select' && column.numeric && value !== null ? Number(value) : value;
    if (this.shape().kind === 'object') {
      this.emit({ ...this.objectValue(), [key]: stored });
      return;
    }
    const rows = this.rows().map((r, i) => (i === index ? { ...r, [key]: stored } : r));
    this.emitRows(rows);
  }

  protected setNumber(index: number, key: string, text: string): void {
    this.setCell(index, key, text.trim() === '' ? null : Number(text));
  }

  protected tagsText(value: unknown): string {
    return Array.isArray(value) ? value.join(', ') : '';
  }

  protected setTags(index: number, key: string, text: string): void {
    this.setCell(
      index,
      key,
      text
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean),
    );
  }

  protected loose(value: unknown): string {
    return looseText(value);
  }

  protected setLoose(index: number, key: string, text: string): void {
    this.setCell(index, key, parseLoose(text));
  }

  protected addRow(): void {
    const row: Row = Object.fromEntries(
      this.columns()
        .filter((c) => c.defaultValue !== undefined || ['checkbox', 'tags', 'table', 'object', 'map'].includes(c.type))
        .map((c) => [c.key, c.defaultValue ?? (c.type === 'checkbox' ? false : c.type === 'object' || c.type === 'map' ? {} : [])]),
    );
    this.emitRows([...this.rows(), row]);
  }

  protected removeRow(index: number): void {
    this.emitRows(this.rows().filter((_, i) => i !== index));
  }

  protected move(index: number, by: number): void {
    const rows = [...this.rows()];
    const target = index + by;
    if (target < 0 || target >= rows.length) return;
    [rows[index], rows[target]] = [rows[target], rows[index]];
    this.emitRows(rows);
  }

  protected listItems(): unknown[] {
    const value = this.value();
    return Array.isArray(value) ? value : [];
  }

  protected listNumeric(): boolean {
    const shape = this.shape();
    return shape.kind === 'list' && shape.item === 'number';
  }

  protected listOptions(): FormOption[] | null {
    const shape = this.shape();
    return shape.kind === 'list' && shape.options?.length ? shape.options : null;
  }

  protected listHas(option: string): boolean {
    return this.listItems().some((v) => String(v) === option);
  }

  protected listToggle(option: string, checked: boolean): void {
    const item = this.listNumeric() ? Number(option) : option;
    const rest = this.listItems().filter((v) => String(v) !== option);
    const order = (this.listOptions() ?? []).map((o) => o.value);
    this.emit(checked ? [...rest, item].sort((a, b) => order.indexOf(String(a)) - order.indexOf(String(b))) : rest);
  }

  protected listAdd(input: HTMLInputElement): void {
    const text = input.value.trim();
    if (!text) return;
    const item = this.listNumeric() ? Number(text) : text;
    if (this.listNumeric() && Number.isNaN(item)) return;
    input.value = '';
    if (!this.listItems().some((v) => v === item)) this.emit([...this.listItems(), item]);
  }

  protected listRemove(index: number): void {
    this.emit(this.listItems().filter((_, i) => i !== index));
  }

  private mapColumns(shape: { keyLabel?: LocalizedText; valueLabel?: LocalizedText }): StructuredColumn[] {
    return [
      { key: 'key', label: shape.keyLabel ?? { ar: 'الاسم', en: 'Name' }, type: 'text', ltr: true, required: true },
      { key: 'value', label: shape.valueLabel ?? { ar: 'القيمة', en: 'Value' }, type: 'value' },
    ];
  }

  private emitRows(rows: Row[]): void {
    if (this.shape().kind === 'map') {
      // Rows being typed (a name still empty) stay on screen; the value holds the named ones.
      this.mapState.set(rows);
      this.emit(rowsMap(rows), { rows: false });
      return;
    }
    this.emit(rows);
  }

  private emit(value: unknown, sync: { text?: boolean; rows?: boolean } = {}): void {
    this.value.set(value);
    if (sync.text !== false) this.text.set(jsonText(value));
    if (sync.rows !== false) this.mapState.set(mapRows(value));
    this.onChange(value);
  }

  private empty(): unknown {
    const kind = this.shape().kind;
    return kind === 'object' || kind === 'map' ? {} : [];
  }

  private same(option: string, value: unknown, column: StructuredColumn): boolean {
    return column.numeric ? Number(option) === Number(value) : option === value;
  }
}

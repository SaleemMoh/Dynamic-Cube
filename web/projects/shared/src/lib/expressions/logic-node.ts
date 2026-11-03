import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService } from '../i18n/i18n.service';
import { LocalizedText, localize } from '../i18n/language';
import { TranslatePipe } from '../i18n/translate.pipe';
import { JsonLogicRule, JsonValue } from './jsonlogic';
import { LogicField, LogicOption, LogicValues, operatorsFor } from './logic-fields';
import {
  CompareOp,
  LIST_OPS,
  LogicCondition,
  LogicValue,
  MathOp,
  NO_VALUE_OPS,
  writeValue,
} from './logic-model';

type ValueKind = LogicValue['kind'];

/** Picks a field from the menu (grouped, searched by typing); another path can be typed when it is not listed. */
@Component({
  selector: 'dc-logic-field',
  imports: [FormsModule, MatAutocompleteModule, MatFormFieldModule, MatInputModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-form-field subscriptSizing="dynamic" class="lf">
      <input
        matInput
        [placeholder]="'logic.pickField' | dcTranslate"
        [ngModel]="text()"
        (ngModelChange)="text.set($event)"
        (blur)="commit()"
        [matAutocomplete]="menu"
        [attr.aria-label]="'logic.field' | dcTranslate"
        data-testid="logic-field"
      />
      <mat-autocomplete #menu="matAutocomplete" (optionSelected)="pick($event.option.value)">
        @for (g of groups(); track g.key) {
          @if (g.label) {
            <mat-optgroup [label]="g.label">
              @for (f of g.fields; track f.path) {
                <mat-option [value]="f.path"
                  >{{ name(f.label) }}
                  <small dir="ltr" class="lf__path">{{ f.path }}</small></mat-option
                >
              }
            </mat-optgroup>
          } @else {
            @for (f of g.fields; track f.path) {
              <mat-option [value]="f.path"
                >{{ name(f.label) }}
                <small dir="ltr" class="lf__path">{{ f.path }}</small></mat-option
              >
            }
          }
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
      flex: 1 1 11rem;
      min-width: 0;
    }
    .lf {
      width: 100%;
    }
    .lf__path {
      color: var(--dga-text-3, #6c737f);
    }
  `,
})
export class LogicFieldPicker {
  private readonly i18n = inject(I18nService);
  readonly fields = input<LogicField[]>([]);
  readonly path = input<string>('');
  readonly pathChange = output<string>();

  /** What the input shows: the field's name when it is listed, else the path. */
  protected readonly text = linkedSignal(() => {
    const f = this.fields().find((x) => x.path === this.path());
    return f ? this.name(f.label) : this.path();
  });

  protected readonly groups = computed(() => {
    const typed = this.text().trim().toLowerCase();
    const current = this.fields().find((x) => x.path === this.path());
    const showAll = !typed || (current && this.name(current.label).toLowerCase() === typed);
    const matches = this.fields().filter(
      (f) =>
        showAll ||
        this.name(f.label).toLowerCase().includes(typed) ||
        f.path.toLowerCase().includes(typed),
    );
    const groups = new Map<string, { key: string; label: string; fields: LogicField[] }>();
    for (const f of matches) {
      const label = f.group ? this.name(f.group) : '';
      if (!groups.has(label)) groups.set(label, { key: label || '-', label, fields: [] });
      groups.get(label)!.fields.push(f);
    }
    return [...groups.values()];
  });

  protected name(text: LocalizedText): string {
    return localize(text, this.i18n.language());
  }

  protected pick(path: string): void {
    this.pathChange.emit(path);
  }

  /** A typed text that is not a listed field's name is taken as a path. */
  protected commit(): void {
    const typed = this.text().trim();
    const byName = this.fields().find((f) => this.name(f.label) === typed || f.path === typed);
    const path = byName ? byName.path : typed;
    if (path !== this.path()) this.pathChange.emit(path);
  }
}

/** A fixed value, typed or chosen as the compared field allows (its options, class values or list values). */
@Component({
  selector: 'dc-logic-fixed',
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let options = choices();
    @if (options === null) {
      <mat-form-field subscriptSizing="dynamic" class="lx">
        <input matInput disabled [placeholder]="'logic.loading' | dcTranslate" />
      </mat-form-field>
    } @else if (options.length && multiple()) {
      <!-- A select cannot change between one and several choices once made: two selects. -->
      <mat-form-field subscriptSizing="dynamic" class="lx">
        <mat-select
          multiple
          [ngModel]="asList()"
          (ngModelChange)="valueChange.emit($event)"
          [placeholder]="'logic.values' | dcTranslate"
          [attr.aria-label]="'logic.values' | dcTranslate"
          data-testid="logic-value"
        >
          @for (o of withCurrent(options); track o.value) {
            <mat-option [value]="o.value">{{ name(o.label) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    } @else if (options.length) {
      <mat-form-field subscriptSizing="dynamic" class="lx">
        <mat-select
          [ngModel]="value()"
          (ngModelChange)="valueChange.emit($event)"
          [placeholder]="'logic.value' | dcTranslate"
          [attr.aria-label]="'logic.value' | dcTranslate"
          data-testid="logic-value"
        >
          @for (o of withCurrent(options); track o.value) {
            <mat-option [value]="o.value">{{ name(o.label) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    } @else if (like()?.type === 'boolean' && !multiple()) {
      <mat-form-field subscriptSizing="dynamic" class="lx">
        <mat-select
          [ngModel]="value()"
          (ngModelChange)="valueChange.emit($event)"
          [attr.aria-label]="'logic.value' | dcTranslate"
          data-testid="logic-value"
        >
          <mat-option [value]="true">{{ 'logic.yes' | dcTranslate }}</mat-option>
          <mat-option [value]="false">{{ 'logic.no' | dcTranslate }}</mat-option>
        </mat-select>
      </mat-form-field>
    } @else {
      <mat-form-field subscriptSizing="dynamic" class="lx">
        <input
          matInput
          dir="auto"
          [type]="inputType()"
          [placeholder]="(multiple() ? 'logic.values' : 'logic.value') | dcTranslate"
          [ngModel]="shown()"
          (ngModelChange)="typed($event)"
          [ngModelOptions]="{ updateOn: 'blur' }"
          [attr.aria-label]="'logic.value' | dcTranslate"
          data-testid="logic-value"
        />
      </mat-form-field>
    }
  `,
  styles: `
    :host {
      display: block;
      flex: 1 1 9rem;
      min-width: 0;
    }
    .lx {
      width: 100%;
    }
  `,
})
export class LogicFixedValue {
  private readonly i18n = inject(I18nService);
  private readonly values = inject(LogicValues);
  /** The field this value is compared with or stands for. */
  readonly like = input<LogicField | undefined>(undefined);
  readonly value = input<JsonValue>(null);
  readonly multiple = input(false);
  readonly valueChange = output<JsonValue>();

  protected readonly choices = computed<LogicOption[] | null>(() => {
    const field = this.like();
    if (field?.options?.length) return field.options;
    const source = this.values.source(field);
    return source ? source() : [];
  });

  protected readonly inputType = computed(() => {
    if (this.multiple()) return 'text';
    const type = this.like()?.type;
    return type === 'number' ? 'number' : type === 'date' ? 'date' : 'text';
  });

  protected readonly asList = computed(() => {
    const v = this.value();
    return Array.isArray(v) ? v : v === null || v === undefined ? [] : [v];
  });

  protected readonly shown = computed(() => {
    const v = this.value();
    if (Array.isArray(v)) return v.join(', ');
    return v === null || v === undefined ? '' : String(v);
  });

  protected name(text: LocalizedText): string {
    return localize(text, this.i18n.language());
  }

  /** Keeps a stored value that is not among the choices visible (shown by its code). */
  protected withCurrent(options: LogicOption[]): LogicOption[] {
    const extra = this.asList()
      .filter((v) => v !== null && !options.some((o) => o.value === String(v)))
      .map((v) => ({ value: String(v), label: { ar: String(v), en: String(v) } }));
    return [...options, ...extra];
  }

  protected typed(text: string | number | null): void {
    if (this.multiple()) {
      const items = String(text ?? '')
        .split(',')
        .map((x) => x.trim())
        .filter((x) => x !== '')
        .map((x) => (this.like()?.type === 'number' && !Number.isNaN(Number(x)) ? Number(x) : x));
      this.valueChange.emit(items);
      return;
    }
    if (text === null || text === '') {
      this.valueChange.emit(null);
      return;
    }
    if (this.like()?.type === 'number' || typeof text === 'number') {
      const n = Number(text);
      this.valueChange.emit(Number.isNaN(n) ? String(text) : n);
      return;
    }
    this.valueChange.emit(String(text));
  }
}

const KINDS: ValueKind[] = ['field', 'fixed', 'math', 'choose', 'aggregate', 'join', 'test', 'raw'];
const MATH_OPS: MathOp[] = ['+', '-', '*', '/', '%', 'min', 'max'];

function newCompare(): LogicCondition {
  return {
    kind: 'compare',
    left: { kind: 'field', path: '' },
    op: 'eq',
    right: { kind: 'fixed', value: null },
  };
}

/**
 * One piece of a rule in the Logic Builder, edited in place: a group of conditions, a comparison, a condition on the
 * rows of a list, a value (field, fixed value, calculation, if/then, sum or count of rows, joined texts) or an advanced
 * piece kept as JSON. It holds pieces of itself, so any depth of groups and calculations is edited the same way.
 */
@Component({
  selector: 'dc-logic-node',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
    LogicFieldPicker,
    LogicFixedValue,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let n = node();
    @switch (n.kind) {
      @case ('group') {
        @let g = asGroup(n);
        <div
          class="ln-group"
          [class.ln-group--nested]="depth() > 0"
          [class.ln-group--not]="g.not"
          data-testid="logic-group"
        >
          @if (g.items.length > 1 || g.not || depth() > 0) {
            <div class="ln-group__head">
              <mat-form-field subscriptSizing="dynamic" class="ln-match">
                <mat-select
                  [ngModel]="g.match"
                  (ngModelChange)="emit({ ...g, match: $event })"
                  [attr.aria-label]="'logic.all' | dcTranslate"
                  data-testid="logic-match"
                >
                  <mat-option value="all">{{ 'logic.all' | dcTranslate }}</mat-option>
                  <mat-option value="any">{{ 'logic.any' | dcTranslate }}</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-checkbox
                [ngModel]="g.not"
                (ngModelChange)="emit({ ...g, not: $event })"
                data-testid="logic-not"
                >{{ 'logic.not' | dcTranslate }}</mat-checkbox
              >
              @if (depth() > 0 && removable()) {
                <button
                  matIconButton
                  type="button"
                  class="ln-remove"
                  (click)="removed.emit()"
                  [matTooltip]="'logic.remove' | dcTranslate"
                >
                  <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
                </button>
              }
            </div>
          }
          @for (item of g.items; track $index; let i = $index) {
            @if (i > 0) {
              <span class="ln-joiner">{{
                (g.match === 'all' ? 'logic.and' : 'logic.or') | dcTranslate
              }}</span>
            }
            <dc-logic-node
              role="condition"
              [node]="item"
              [fields]="fields()"
              [depth]="depth() + 1"
              (nodeChange)="setItem(g, i, $event)"
              (removed)="removeItem(g, i)"
              [attr.data-testid]="'logic-item-' + i"
            />
          }
          <div class="ln-add">
            <button
              matButton
              type="button"
              (click)="emit({ ...g, items: [...g.items, newCompare()] })"
              data-testid="logic-add-condition"
            >
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon
              >{{ 'logic.addCondition' | dcTranslate }}
            </button>
            <button
              matButton
              type="button"
              (click)="emit({ ...g, items: [...g.items, groupOfOne()] })"
              data-testid="logic-add-group"
            >
              <mat-icon fontSet="material-symbols-outlined">library_add</mat-icon
              >{{ 'logic.addGroup' | dcTranslate }}
            </button>
            @if (rowFields().length) {
              <button
                matButton
                type="button"
                (click)="emit({ ...g, items: [...g.items, newRows()] })"
                data-testid="logic-add-rows"
              >
                <mat-icon fontSet="material-symbols-outlined">table_rows</mat-icon
                >{{ 'logic.addRows' | dcTranslate }}
              </button>
            }
          </div>
        </div>
      }
      @case ('compare') {
        @let c = asCompare(n);
        @let left = fieldOf(c.left);
        <div class="ln-row" data-testid="logic-compare">
          @if (c.left.kind === 'field') {
            <dc-logic-field
              [fields]="fields()"
              [path]="c.left.path"
              (pathChange)="setLeft(c, $event)"
            />
          } @else {
            <div class="ln-sub">
              <dc-logic-node
                role="value"
                [node]="c.left"
                [fields]="fields()"
                [depth]="depth() + 1"
                (nodeChange)="emit({ ...c, left: $any($event) })"
              />
            </div>
          }
          <mat-form-field subscriptSizing="dynamic" class="ln-op">
            <mat-select
              [ngModel]="c.op"
              (ngModelChange)="setOp(c, $event)"
              [attr.aria-label]="'logic.operator' | dcTranslate"
              data-testid="logic-operator"
            >
              @for (op of operatorsFor(left, c.op); track op) {
                <mat-option [value]="op">{{ 'logic.op.' + op | dcTranslate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (!noValue(c.op) && c.right) {
            @if (c.right.kind === 'fixed') {
              <dc-logic-fixed
                [like]="left"
                [value]="c.right.value"
                [multiple]="listOp(c.op)"
                (valueChange)="emit({ ...c, right: { kind: 'fixed', value: $event } })"
              />
            } @else {
              <div class="ln-sub">
                <dc-logic-node
                  role="value"
                  [node]="c.right"
                  [fields]="fields()"
                  [like]="left"
                  [depth]="depth() + 1"
                  (nodeChange)="emit({ ...c, right: $any($event) })"
                />
              </div>
            }
            @if (c.op === 'between') {
              <span class="ln-word">{{ 'logic.to' | dcTranslate }}</span>
              @if (!c.to || c.to.kind === 'fixed') {
                <dc-logic-fixed
                  [like]="left"
                  [value]="c.to?.kind === 'fixed' ? c.to.value : null"
                  (valueChange)="emit({ ...c, to: { kind: 'fixed', value: $event } })"
                />
              } @else {
                <div class="ln-sub">
                  <dc-logic-node
                    role="value"
                    [node]="c.to"
                    [fields]="fields()"
                    [like]="left"
                    [depth]="depth() + 1"
                    (nodeChange)="emit({ ...c, to: $any($event) })"
                  />
                </div>
              }
            }
          }
          <button
            matIconButton
            type="button"
            [matMenuTriggerFor]="more"
            [matTooltip]="'logic.compareWith' | dcTranslate"
            data-testid="logic-more"
          >
            <mat-icon fontSet="material-symbols-outlined">more_vert</mat-icon>
          </button>
          <mat-menu #more="matMenu">
            <button
              mat-menu-item
              type="button"
              (click)="
                emit({
                  ...c,
                  left:
                    c.left.kind === 'field'
                      ? { kind: 'math', op: '+', args: [c.left, { kind: 'fixed', value: 0 }] }
                      : { kind: 'field', path: '' },
                })
              "
            >
              <mat-icon fontSet="material-symbols-outlined">calculate</mat-icon
              >{{
                (c.left.kind === 'field' ? 'logic.kind.math' : 'logic.kind.field') | dcTranslate
              }}
              ({{ 'logic.field' | dcTranslate }})
            </button>
            @if (!noValue(c.op) && !listOp(c.op)) {
              <button
                mat-menu-item
                type="button"
                (click)="
                  emit({
                    ...c,
                    right:
                      c.right?.kind === 'fixed'
                        ? { kind: 'field', path: '' }
                        : { kind: 'fixed', value: null },
                  })
                "
              >
                <mat-icon fontSet="material-symbols-outlined">swap_horiz</mat-icon
                >{{ 'logic.compareWith' | dcTranslate }}:
                {{
                  (c.right?.kind === 'fixed' ? 'logic.kind.field' : 'logic.kind.fixed')
                    | dcTranslate
                }}
              </button>
            }
          </mat-menu>
          <button
            matIconButton
            type="button"
            class="ln-remove"
            (click)="removed.emit()"
            [matTooltip]="'logic.remove' | dcTranslate"
            data-testid="logic-remove"
          >
            <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
          </button>
        </div>
      }
      @case ('rows') {
        @let r = asRows(n);
        <div class="ln-group ln-group--nested" data-testid="logic-rows">
          <div class="ln-row">
            <mat-form-field subscriptSizing="dynamic" class="ln-op">
              <mat-select
                [ngModel]="r.quantifier"
                (ngModelChange)="emit({ ...r, quantifier: $event })"
                [attr.aria-label]="'logic.list' | dcTranslate"
              >
                <mat-option value="some">{{ 'logic.q.some' | dcTranslate }}</mat-option>
                <mat-option value="all">{{ 'logic.q.all' | dcTranslate }}</mat-option>
                <mat-option value="none">{{ 'logic.q.none' | dcTranslate }}</mat-option>
              </mat-select>
            </mat-form-field>
            <span class="ln-word">{{ 'logic.rowsOf' | dcTranslate }}</span>
            <dc-logic-field
              [fields]="rowFields()"
              [path]="r.list"
              (pathChange)="emit({ ...r, list: $event })"
            />
            <span class="ln-word">{{ 'logic.where' | dcTranslate }}</span>
            <button
              matIconButton
              type="button"
              class="ln-remove"
              (click)="removed.emit()"
              [matTooltip]="'logic.remove' | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">delete</mat-icon>
            </button>
          </div>
          <dc-logic-node
            role="condition"
            [removable]="false"
            [node]="r.where"
            [fields]="columnsOf(r.list)"
            [depth]="depth() + 1"
            (nodeChange)="emit({ ...r, where: $any($event) })"
          />
        </div>
      }
      @case ('raw') {
        <div class="ln-raw" data-testid="logic-raw">
          @if (role() === 'value') {
            <mat-form-field subscriptSizing="dynamic" class="ln-kind">
              <mat-select
                [ngModel]="'raw'"
                (ngModelChange)="setKind(asValue(n), $event)"
                [attr.aria-label]="'logic.kind' | dcTranslate"
              >
                @for (k of kinds; track k) {
                  <mat-option [value]="k">{{ 'logic.kind.' + k | dcTranslate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          }
          <span class="ln-raw__label">{{ 'logic.advancedPiece' | dcTranslate }}</span>
          <textarea
            class="ln-raw__json"
            dir="ltr"
            spellcheck="false"
            rows="2"
            [ngModel]="rawText()"
            (ngModelChange)="rawTyped($event)"
          ></textarea>
          @if (rawError()) {
            <span class="ln-raw__error" role="alert">{{ 'logic.invalidJson' | dcTranslate }}</span>
          }
          @if (role() === 'condition') {
            <button
              matIconButton
              type="button"
              class="ln-remove"
              (click)="removed.emit()"
              [matTooltip]="'logic.remove' | dcTranslate"
            >
              <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
            </button>
          }
        </div>
      }
      @default {
        <!-- a value -->
        @let v = asValue(n);
        <div class="ln-value" data-testid="logic-value-node">
          <mat-form-field subscriptSizing="dynamic" class="ln-kind">
            <mat-select
              [ngModel]="v.kind"
              (ngModelChange)="setKind(v, $event)"
              [attr.aria-label]="'logic.kind' | dcTranslate"
              data-testid="logic-kind"
            >
              @for (k of kinds; track k) {
                <mat-option [value]="k">{{ 'logic.kind.' + k | dcTranslate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @switch (v.kind) {
            @case ('field') {
              <dc-logic-field
                [fields]="fields()"
                [path]="asField(v).path"
                (pathChange)="emit({ kind: 'field', path: $event })"
              />
            }
            @case ('fixed') {
              <dc-logic-fixed
                [like]="like()"
                [value]="asFixed(v).value"
                (valueChange)="emit({ kind: 'fixed', value: $event })"
              />
            }
            @case ('math') {
              @let m = asMath(v);
              <div class="ln-block">
                <mat-form-field subscriptSizing="dynamic" class="ln-op">
                  <mat-select
                    [ngModel]="m.op"
                    (ngModelChange)="emit({ ...m, op: $event })"
                    [attr.aria-label]="'logic.kind.math' | dcTranslate"
                    data-testid="logic-math-op"
                  >
                    @for (op of mathOps; track op) {
                      <mat-option [value]="op">{{ 'logic.math.' + op | dcTranslate }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                @for (a of m.args; track $index; let i = $index) {
                  <div class="ln-operand">
                    <dc-logic-node
                      role="value"
                      [node]="a"
                      [fields]="fields()"
                      [depth]="depth() + 1"
                      (nodeChange)="setArg(m, i, $event)"
                    />
                    @if (m.args.length > 1) {
                      <button
                        matIconButton
                        type="button"
                        class="ln-remove"
                        (click)="emit({ ...m, args: without(m.args, i) })"
                        [matTooltip]="'logic.remove' | dcTranslate"
                      >
                        <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                      </button>
                    }
                  </div>
                }
                <button
                  matButton
                  type="button"
                  (click)="emit({ ...m, args: [...m.args, { kind: 'fixed', value: 0 }] })"
                  data-testid="logic-add-operand"
                >
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon
                  >{{ 'logic.addOperand' | dcTranslate }}
                </button>
              </div>
            }
            @case ('join') {
              @let j = asJoin(v);
              <div class="ln-block">
                @for (a of j.args; track $index; let i = $index) {
                  <div class="ln-operand">
                    <dc-logic-node
                      role="value"
                      [node]="a"
                      [fields]="fields()"
                      [depth]="depth() + 1"
                      (nodeChange)="setArg(j, i, $event)"
                    />
                    @if (j.args.length > 1) {
                      <button
                        matIconButton
                        type="button"
                        class="ln-remove"
                        (click)="emit({ ...j, args: without(j.args, i) })"
                        [matTooltip]="'logic.remove' | dcTranslate"
                      >
                        <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                      </button>
                    }
                  </div>
                }
                <button
                  matButton
                  type="button"
                  (click)="emit({ ...j, args: [...j.args, { kind: 'fixed', value: '' }] })"
                >
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon
                  >{{ 'logic.addOperand' | dcTranslate }}
                </button>
              </div>
            }
            @case ('choose') {
              @let ch = asChoose(v);
              <div class="ln-block" data-testid="logic-choose">
                @for (c of ch.cases; track $index; let i = $index) {
                  <div class="ln-case">
                    <div class="ln-case__head">
                      <strong>{{ 'logic.if' | dcTranslate }}</strong>
                      @if (ch.cases.length > 1) {
                        <button
                          matIconButton
                          type="button"
                          class="ln-remove"
                          (click)="emit({ ...ch, cases: without(ch.cases, i) })"
                          [matTooltip]="'logic.remove' | dcTranslate"
                        >
                          <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
                        </button>
                      }
                    </div>
                    <dc-logic-node
                      role="condition"
                      [removable]="false"
                      [node]="c.when"
                      [fields]="fields()"
                      [depth]="depth() + 1"
                      (nodeChange)="setCase(ch, i, { when: $any($event), then: c.then })"
                    />
                    <div class="ln-case__then">
                      <strong>{{ 'logic.then' | dcTranslate }}</strong>
                      <dc-logic-node
                        role="value"
                        [node]="c.then"
                        [fields]="fields()"
                        [like]="like()"
                        [depth]="depth() + 1"
                        (nodeChange)="setCase(ch, i, { when: c.when, then: $any($event) })"
                      />
                    </div>
                  </div>
                }
                <button
                  matButton
                  type="button"
                  (click)="
                    emit({
                      ...ch,
                      cases: [
                        ...ch.cases,
                        { when: groupOfOne(), then: { kind: 'fixed', value: null } },
                      ],
                    })
                  "
                  data-testid="logic-add-case"
                >
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon
                  >{{ 'logic.addCase' | dcTranslate }}
                </button>
                <div class="ln-case__then">
                  <strong>{{ 'logic.otherwise' | dcTranslate }}</strong>
                  <dc-logic-node
                    role="value"
                    [node]="ch.otherwise"
                    [fields]="fields()"
                    [like]="like()"
                    [depth]="depth() + 1"
                    (nodeChange)="emit({ ...ch, otherwise: $any($event) })"
                  />
                </div>
              </div>
            }
            @case ('aggregate') {
              @let ag = asAggregate(v);
              <div class="ln-block" data-testid="logic-aggregate">
                <div class="ln-row">
                  <mat-form-field subscriptSizing="dynamic" class="ln-op">
                    <mat-select
                      [ngModel]="ag.fn"
                      (ngModelChange)="
                        emit({ ...ag, fn: $event, column: $event === 'count' ? null : ag.column })
                      "
                      [attr.aria-label]="'logic.kind.aggregate' | dcTranslate"
                    >
                      <mat-option value="sum">{{ 'logic.agg.sum' | dcTranslate }}</mat-option>
                      <mat-option value="count">{{ 'logic.agg.count' | dcTranslate }}</mat-option>
                    </mat-select>
                  </mat-form-field>
                  @if (ag.fn === 'sum') {
                    <dc-logic-field
                      [fields]="columnsOf(ag.list)"
                      [path]="ag.column ?? ''"
                      (pathChange)="emit({ ...ag, column: $event || null })"
                    />
                  }
                  <span class="ln-word">{{ 'logic.rowsOf' | dcTranslate }}</span>
                  <dc-logic-field
                    [fields]="rowFields()"
                    [path]="ag.list"
                    (pathChange)="emit({ ...ag, list: $event })"
                  />
                </div>
                <mat-checkbox
                  [ngModel]="ag.where !== null"
                  (ngModelChange)="emit({ ...ag, where: $event ? groupOfOne() : null })"
                  >{{ 'logic.onlyRows' | dcTranslate }}</mat-checkbox
                >
                @if (ag.where) {
                  <dc-logic-node
                    role="condition"
                    [removable]="false"
                    [node]="ag.where"
                    [fields]="columnsOf(ag.list)"
                    [depth]="depth() + 1"
                    (nodeChange)="emit({ ...ag, where: $any($event) })"
                  />
                }
              </div>
            }
            @case ('test') {
              <div class="ln-block">
                <dc-logic-node
                  role="condition"
                  [removable]="false"
                  [node]="asTest(v).condition"
                  [fields]="fields()"
                  [depth]="depth() + 1"
                  (nodeChange)="emit({ kind: 'test', condition: $any($event) })"
                />
              </div>
            }
          }
        </div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .ln-group {
      display: grid;
      gap: 6px;
    }
    .ln-group--nested {
      padding: 8px 10px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-inline-start: 3px solid var(--mat-sys-primary, #1b8354);
      border-radius: 8px;
      background: var(--dga-gray-50, #f9fafb);
    }
    .ln-group--not {
      border-inline-start-color: #b42318;
    }
    .ln-group__head,
    .ln-row,
    .ln-add,
    .ln-value,
    .ln-operand,
    .ln-case__head,
    .ln-case__then {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .ln-value {
      align-items: flex-start;
    }
    .ln-match {
      width: 14rem;
    }
    .ln-op {
      flex: 0 1 11rem;
    }
    .ln-kind {
      flex: 0 0 10rem;
    }
    .ln-sub,
    .ln-block {
      flex: 1 1 16rem;
      min-width: 0;
    }
    .ln-block {
      display: grid;
      gap: 6px;
    }
    .ln-operand {
      align-items: flex-start;
      flex-wrap: nowrap;
    }
    .ln-operand > dc-logic-node {
      flex: 1 1 auto;
    }
    .ln-case {
      display: grid;
      gap: 6px;
      padding: 8px;
      border: 1px dashed var(--dga-gray-300, #d2d6db);
      border-radius: 8px;
    }
    .ln-case__then > dc-logic-node {
      flex: 1 1 auto;
    }
    .ln-joiner,
    .ln-word {
      font-size: 12px;
      font-weight: 600;
      color: var(--dga-text-3, #6c737f);
    }
    .ln-joiner {
      padding-inline-start: 4px;
    }
    .ln-raw {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 6px;
      padding: 6px 8px;
      border: 1px dashed #b54708;
      border-radius: 8px;
    }
    .ln-raw__label {
      font-size: 12px;
      color: #b54708;
    }
    .ln-raw__json {
      flex: 1 1 14rem;
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
    .ln-raw__error {
      color: #b42318;
      font-size: 12px;
    }
    .ln-remove {
      margin-inline-start: auto;
    }
  `,
})
export class LogicNodeEditor {
  readonly node = input.required<LogicCondition | LogicValue>();
  readonly role = input<'condition' | 'value'>('condition');
  readonly fields = input<LogicField[]>([]);
  /** For values: the field the value is compared with or stands for (its choices and type). */
  readonly like = input<LogicField | undefined>(undefined);
  readonly depth = input(0);
  /** False where the parent removes this piece itself (an if/then case's condition, a "where" on rows). */
  readonly removable = input(true);
  readonly nodeChange = output<LogicCondition | LogicValue>();
  readonly removed = output<void>();

  protected readonly kinds = KINDS;
  protected readonly mathOps = MATH_OPS;
  protected readonly operatorsFor = operatorsFor;
  protected readonly newCompare = newCompare;

  protected readonly rowFields = computed(() => this.fields().filter((f) => f.type === 'rows'));
  protected readonly rawText = linkedSignal(() => {
    const n = this.node();
    return n.kind === 'raw' ? JSON.stringify(n.rule, null, 1) : '';
  });
  protected readonly rawError = linkedSignal(() => this.node() && false);

  protected emit(node: LogicCondition | LogicValue): void {
    this.nodeChange.emit(node);
  }

  protected groupOfOne(): LogicCondition {
    return { kind: 'group', match: 'all', not: false, items: [newCompare()] };
  }

  protected newRows(): LogicCondition {
    return {
      kind: 'rows',
      quantifier: 'some',
      list: this.rowFields()[0]?.path ?? '',
      where: this.groupOfOne(),
    };
  }

  protected fieldOf(value: LogicValue): LogicField | undefined {
    if (value.kind === 'field') return this.fields().find((f) => f.path === value.path);
    if (value.kind === 'math' || value.kind === 'aggregate')
      return { path: '', label: { ar: '', en: '' }, type: 'number' };
    return undefined;
  }

  protected columnsOf(list: string): LogicField[] {
    return this.fields().find((f) => f.path === list)?.columns ?? [];
  }

  protected noValue(op: CompareOp): boolean {
    return NO_VALUE_OPS.includes(op);
  }

  protected listOp(op: CompareOp): boolean {
    return LIST_OPS.includes(op);
  }

  protected setItem(
    group: Extract<LogicCondition, { kind: 'group' }>,
    index: number,
    item: LogicCondition | LogicValue,
  ): void {
    this.emit({
      ...group,
      items: group.items.map((x, i) => (i === index ? (item as LogicCondition) : x)),
    });
  }

  protected removeItem(group: Extract<LogicCondition, { kind: 'group' }>, index: number): void {
    this.emit({ ...group, items: group.items.filter((_, i) => i !== index) });
  }

  protected setLeft(c: Extract<LogicCondition, { kind: 'compare' }>, path: string): void {
    const field = this.fields().find((f) => f.path === path);
    const ops = operatorsFor(field);
    const op = ops.includes(c.op) ? c.op : ops[0];
    this.emit(this.withOp({ ...c, left: { kind: 'field', path } }, op));
  }

  protected setOp(c: Extract<LogicCondition, { kind: 'compare' }>, op: CompareOp): void {
    this.emit(this.withOp(c, op));
  }

  /** Changing the comparison keeps what still fits on the right (a list for "one of", nothing for "is filled"). */
  private withOp(c: Extract<LogicCondition, { kind: 'compare' }>, op: CompareOp): LogicCondition {
    const next = { ...c, op, bare: undefined };
    if (NO_VALUE_OPS.includes(op)) return { ...next, right: null, to: undefined };
    const right = c.right ?? { kind: 'fixed', value: null };
    if (LIST_OPS.includes(op)) {
      const v = right.kind === 'fixed' ? right.value : null;
      return {
        ...next,
        right: { kind: 'fixed', value: Array.isArray(v) ? v : v === null ? [] : [v] },
        to: undefined,
      };
    }
    const fixed =
      right.kind === 'fixed' && Array.isArray(right.value)
        ? { kind: 'fixed' as const, value: right.value[0] ?? null }
        : right;
    return {
      ...next,
      right: fixed,
      to: op === 'between' ? (c.to ?? { kind: 'fixed', value: null }) : undefined,
    };
  }

  protected setKind(current: LogicValue, kind: ValueKind): void {
    switch (kind) {
      case 'field':
        return this.emit({ kind, path: current.kind === 'field' ? current.path : '' });
      case 'fixed':
        return this.emit({ kind, value: current.kind === 'fixed' ? current.value : null });
      case 'math':
        return this.emit({ kind, op: '+', args: [current, { kind: 'fixed', value: 0 }] });
      case 'join':
        return this.emit({ kind, args: [current, { kind: 'fixed', value: '' }] });
      case 'choose':
        return this.emit({
          kind,
          cases: [{ when: this.groupOfOne(), then: current }],
          otherwise: { kind: 'fixed', value: null },
        });
      case 'aggregate':
        return this.emit({
          kind,
          fn: 'sum',
          list: this.rowFields()[0]?.path ?? '',
          column: null,
          where: null,
        });
      case 'test':
        return this.emit({ kind, condition: this.groupOfOne() });
      case 'raw':
        return this.emit({ kind, rule: writeValue(current) as JsonLogicRule });
    }
  }

  protected setArg(
    node: Extract<LogicValue, { kind: 'math' | 'join' }>,
    index: number,
    arg: LogicCondition | LogicValue,
  ): void {
    this.emit({
      ...node,
      args: node.args.map((x, i) => (i === index ? (arg as LogicValue) : x)),
    } as LogicValue);
  }

  protected setCase(
    node: Extract<LogicValue, { kind: 'choose' }>,
    index: number,
    item: { when: LogicCondition; then: LogicValue },
  ): void {
    this.emit({ ...node, cases: node.cases.map((c, i) => (i === index ? item : c)) });
  }

  protected without<T>(list: T[], index: number): T[] {
    return list.filter((_, i) => i !== index);
  }

  protected rawTyped(text: string): void {
    this.rawText.set(text);
    try {
      const rule = JSON.parse(text) as JsonLogicRule;
      this.rawError.set(false);
      this.emit({ kind: 'raw', rule });
    } catch {
      this.rawError.set(true);
    }
  }

  // Narrowing helpers for the template.
  protected asGroup(n: LogicCondition | LogicValue) {
    return n as Extract<LogicCondition, { kind: 'group' }>;
  }
  protected asCompare(n: LogicCondition | LogicValue) {
    return n as Extract<LogicCondition, { kind: 'compare' }>;
  }
  protected asRows(n: LogicCondition | LogicValue) {
    return n as Extract<LogicCondition, { kind: 'rows' }>;
  }
  protected asValue(n: LogicCondition | LogicValue) {
    return n as LogicValue;
  }
  protected asField(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'field' }>;
  }
  protected asFixed(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'fixed' }>;
  }
  protected asMath(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'math' }>;
  }
  protected asJoin(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'join' }>;
  }
  protected asChoose(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'choose' }>;
  }
  protected asAggregate(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'aggregate' }>;
  }
  protected asTest(n: LogicValue) {
    return n as Extract<LogicValue, { kind: 'test' }>;
  }
}

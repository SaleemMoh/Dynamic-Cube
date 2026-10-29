import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { I18nService } from '../i18n/i18n.service';
import { LocalizedText, localize } from '../i18n/language';
import { TranslatePipe } from '../i18n/translate.pipe';
import { JsonLogicError, JsonLogicRule, JsonValue, jsonLogic } from './jsonlogic';
import { RULE_OPERATORS, RuleCondition, RuleGroup, RuleOperator, UNARY_OPERATORS, buildRule, parseRule, varPath } from './rule-model';

/** Something a rule can read, e.g. `data.CLS_SECTORS` or `level`. */
export interface RuleVariable {
  path: string;
  label: LocalizedText;
  type?: 'text' | 'number' | 'date' | 'boolean' | 'list';
  options?: { value: string; label: LocalizedText }[];
}

type ValueKind = 'variable' | 'text' | 'number' | 'json';

/**
 * Edits a JSONLogic rule without writing JSON. As a condition: all or any of comparisons of a variable with a value
 * (equals, greater than, one of, contains, filled, empty). As a value: a variable, a fixed text or number. Anything
 * else, and anything wanted beyond that, is edited as JSON, checked as it is typed.
 */
@Component({
  selector: 'dc-rule-editor',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-rule" [attr.data-testid]="testId()">
      <div class="dc-rule__head">
        @if (label()) {
          <span class="dc-rule__label">{{ label() }}</span>
        }
        <button matButton type="button" class="dc-rule__switch" (click)="toggleRaw()" [disabled]="raw() && !canLeaveRaw()" data-testid="rule-mode">
          {{ (raw() ? 'rule.visual' : 'rule.json') | dcTranslate }}
        </button>
      </div>

      @if (raw()) {
        <textarea
          class="dc-rule__json"
          dir="ltr"
          spellcheck="false"
          rows="4"
          [ngModel]="text()"
          (ngModelChange)="typed($event)"
          data-testid="rule-json"
        ></textarea>
        @if (error(); as e) {
          <p class="dc-rule__error" role="alert" dir="ltr">{{ e }}</p>
        } @else if (raw() && !canLeaveRaw()) {
          <p class="dc-rule__hint">{{ 'rule.onlyJson' | dcTranslate }}</p>
        }
      } @else if (mode() === 'condition') {
        @let g = group()!;
        @if (g.conditions.length > 1) {
          <mat-form-field subscriptSizing="dynamic" class="dc-rule__match">
            <mat-select
              [ngModel]="g.match"
              (ngModelChange)="setGroup({ match: $event, conditions: g.conditions })"
              [attr.aria-label]="'rule.match' | dcTranslate"
            >
              <mat-option value="all">{{ 'rule.all' | dcTranslate }}</mat-option>
              <mat-option value="any">{{ 'rule.any' | dcTranslate }}</mat-option>
            </mat-select>
          </mat-form-field>
        }
        @for (c of g.conditions; track $index; let i = $index) {
          <div class="dc-rule__row" [attr.data-testid]="'rule-row-' + i">
            <mat-form-field subscriptSizing="dynamic" class="dc-rule__var">
              <input
                matInput
                dir="ltr"
                [placeholder]="'rule.variable' | dcTranslate"
                [ngModel]="c.variable"
                (ngModelChange)="setCondition(i, { variable: $event })"
                [matAutocomplete]="vars"
                [ngModelOptions]="{ updateOn: 'blur' }"
                data-testid="rule-variable"
              />
              <mat-autocomplete #vars="matAutocomplete" (optionSelected)="setCondition(i, { variable: $event.option.value })">
                @for (v of variables(); track v.path) {
                  <mat-option [value]="v.path"
                    >{{ name(v.label) }} <small dir="ltr" class="dc-rule__path">{{ v.path }}</small></mat-option
                  >
                }
              </mat-autocomplete>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic" class="dc-rule__op">
              <mat-select
                [ngModel]="c.operator"
                (ngModelChange)="setCondition(i, { operator: $event })"
                [attr.aria-label]="'rule.operator' | dcTranslate"
                data-testid="rule-operator"
              >
                @for (op of operators; track op) {
                  <mat-option [value]="op">{{ 'rule.op.' + op | dcTranslate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            @if (!unary(c.operator)) {
              @let v = variable(c.variable);
              @if (v?.options?.length) {
                <mat-form-field subscriptSizing="dynamic" class="dc-rule__value">
                  <mat-select
                    [multiple]="c.operator === 'in'"
                    [ngModel]="c.value"
                    (ngModelChange)="setCondition(i, { value: $event })"
                    [attr.aria-label]="'rule.value' | dcTranslate"
                    data-testid="rule-value"
                  >
                    @for (o of v!.options!; track o.value) {
                      <mat-option [value]="o.value">{{ name(o.label) }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              } @else {
                <mat-form-field subscriptSizing="dynamic" class="dc-rule__value">
                  <input
                    matInput
                    dir="auto"
                    [type]="v?.type === 'number' && c.operator !== 'in' ? 'number' : 'text'"
                    [placeholder]="(c.operator === 'in' ? 'rule.values' : 'rule.value') | dcTranslate"
                    [ngModel]="shown(c)"
                    (ngModelChange)="setCondition(i, { value: parsed($event, c, v) })"
                    [ngModelOptions]="{ updateOn: 'blur' }"
                    data-testid="rule-value"
                  />
                </mat-form-field>
              }
            }
            <button matIconButton type="button" (click)="removeCondition(i)" [matTooltip]="'rule.remove' | dcTranslate">
              <mat-icon fontSet="material-symbols-outlined">close</mat-icon>
            </button>
          </div>
        }
        <button matButton type="button" (click)="addCondition()" data-testid="rule-add">
          <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
          {{ 'rule.add' | dcTranslate }}
        </button>
      } @else {
        @let k = valueKind();
        <div class="dc-rule__row">
          <mat-form-field subscriptSizing="dynamic" class="dc-rule__op">
            <mat-select [ngModel]="k" (ngModelChange)="setKind($event)" [attr.aria-label]="'rule.valueKind' | dcTranslate" data-testid="rule-kind">
              @for (kind of kinds; track kind) {
                <mat-option [value]="kind">{{ 'rule.kind.' + kind | dcTranslate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (k === 'variable') {
            <mat-form-field subscriptSizing="dynamic" class="dc-rule__var">
              <input
                matInput
                dir="ltr"
                [placeholder]="'rule.variable' | dcTranslate"
                [ngModel]="valuePath()"
                (ngModelChange)="emit($event ? { var: $event } : null)"
                [matAutocomplete]="valueVars"
                [ngModelOptions]="{ updateOn: 'blur' }"
                data-testid="rule-variable"
              />
              <mat-autocomplete #valueVars="matAutocomplete" (optionSelected)="emit({ var: $event.option.value })">
                @for (v of variables(); track v.path) {
                  <mat-option [value]="v.path"
                    >{{ name(v.label) }} <small dir="ltr" class="dc-rule__path">{{ v.path }}</small></mat-option
                  >
                }
              </mat-autocomplete>
            </mat-form-field>
          } @else {
            <mat-form-field subscriptSizing="dynamic" class="dc-rule__var">
              <input
                matInput
                dir="auto"
                [type]="k === 'number' ? 'number' : 'text'"
                [ngModel]="literal()"
                (ngModelChange)="emit(k === 'number' ? ($event === '' || $event === null ? null : +$event) : $event || null)"
                [ngModelOptions]="{ updateOn: 'blur' }"
                data-testid="rule-value"
              />
            </mat-form-field>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .dc-rule {
      display: grid;
      gap: 0.375rem;
    }
    .dc-rule__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
    }
    .dc-rule__label {
      font-size: 13px;
      font-weight: 600;
    }
    .dc-rule__switch {
      font-size: 12px;
    }
    .dc-rule__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.25rem;
    }
    .dc-rule__var {
      flex: 1 1 9rem;
      min-width: 0;
    }
    .dc-rule__op {
      flex: 0 1 8rem;
    }
    .dc-rule__value {
      flex: 1 1 7rem;
      min-width: 0;
    }
    .dc-rule__match {
      width: 12rem;
    }
    .dc-rule__path {
      color: var(--dga-text-3, #6c737f);
    }
    .dc-rule__json {
      width: 100%;
      box-sizing: border-box;
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
    .dc-rule__error {
      margin: 0;
      color: #b42318;
      font-size: 12px;
    }
    .dc-rule__hint {
      margin: 0;
      color: var(--dga-text-3, #6c737f);
      font-size: 12px;
    }
  `,
})
export class RuleEditor {
  private readonly i18n = inject(I18nService);
  readonly rule = input<JsonLogicRule | null | undefined>(null);
  readonly variables = input<RuleVariable[]>([]);
  /** `condition` (true or false) or `value` (what the rule gives). */
  readonly mode = input<'condition' | 'value'>('condition');
  readonly label = input<string>('');
  readonly testId = input<string>('rule-editor');
  readonly ruleChange = output<JsonLogicRule | null>();

  protected readonly operators = RULE_OPERATORS;
  protected readonly kinds: ValueKind[] = ['variable', 'text', 'number', 'json'];

  protected readonly group = linkedSignal<RuleGroup | null>(() => (this.mode() === 'condition' ? parseRule(this.rule()) : null));
  protected readonly valueKind = linkedSignal<ValueKind>(() => kindOf(this.rule()));
  protected readonly raw = linkedSignal<boolean>(() => (this.mode() === 'condition' ? this.group() === null : this.valueKind() === 'json'));
  protected readonly text = linkedSignal(() => format(this.rule()));
  protected readonly error = linkedSignal<string | null>(() => null);
  protected readonly valuePath = computed(() => varPath(this.rule() as JsonValue) ?? '');
  protected readonly literal = computed(() => {
    const rule = this.rule();
    return typeof rule === 'string' || typeof rule === 'number' ? rule : '';
  });
  /** The JSON can be shown in the visual form. */
  protected readonly canLeaveRaw = computed(() => (this.mode() === 'condition' ? parseRule(this.rule()) !== null : kindOf(this.rule()) !== 'json'));

  protected name(text: LocalizedText): string {
    return localize(text, this.i18n.language());
  }

  protected unary(op: RuleOperator): boolean {
    return UNARY_OPERATORS.includes(op);
  }

  protected variable(path: string): RuleVariable | undefined {
    return this.variables().find((v) => v.path === path);
  }

  protected toggleRaw(): void {
    if (this.raw()) {
      if (!this.canLeaveRaw()) return;
      this.raw.set(false);
    } else {
      this.text.set(format(this.rule()));
      this.raw.set(true);
    }
  }

  protected typed(text: string): void {
    this.text.set(text);
    if (!text.trim()) {
      this.error.set(null);
      this.ruleChange.emit(null);
      return;
    }
    try {
      const rule = JSON.parse(text) as JsonLogicRule;
      jsonLogic.evaluate(rule, {
        data: {},
        level: null,
        party: {},
        actions: {},
        decisions: {},
        documents: {},
      });
      this.error.set(null);
      this.ruleChange.emit(rule);
    } catch (e) {
      this.error.set(e instanceof JsonLogicError ? e.message : this.i18n.translate('rule.invalidJson'));
    }
  }

  protected setGroup(group: RuleGroup): void {
    this.group.set(group);
    this.ruleChange.emit(buildRule(group));
  }

  protected setCondition(index: number, patch: Partial<RuleCondition>): void {
    const g = this.group();
    if (!g) return;
    const conditions = g.conditions.map((c, i) => {
      if (i !== index) return c;
      const next = { ...c, ...patch };
      // A list operator takes a list, the others one value.
      if (patch.operator)
        next.value =
          patch.operator === 'in'
            ? Array.isArray(c.value)
              ? c.value
              : c.value === null || c.value === ''
                ? []
                : [c.value]
            : Array.isArray(c.value)
              ? (c.value[0] ?? null)
              : c.value;
      return next;
    });
    this.setGroup({ ...g, conditions });
  }

  protected addCondition(): void {
    const g = this.group() ?? { match: 'all' as const, conditions: [] };
    this.group.set({
      ...g,
      conditions: [...g.conditions, { variable: '', operator: '==', value: null }],
    });
  }

  protected removeCondition(index: number): void {
    const g = this.group();
    if (g) this.setGroup({ ...g, conditions: g.conditions.filter((_, i) => i !== index) });
  }

  protected shown(c: RuleCondition): string {
    if (Array.isArray(c.value)) return c.value.join(', ');
    return c.value === null || c.value === undefined ? '' : String(c.value);
  }

  protected parsed(text: string, c: RuleCondition, v: RuleVariable | undefined): JsonValue {
    const one = (s: string): JsonValue => {
      const t = s.trim();
      if (v?.type === 'number' || (v === undefined && t !== '' && !Number.isNaN(Number(t)))) return t === '' ? null : Number(t);
      if (t === 'true' || t === 'false') return t === 'true';
      return t;
    };
    if (c.operator === 'in')
      return String(text ?? '')
        .split(',')
        .map(one)
        .filter((x) => x !== '' && x !== null);
    return one(String(text ?? ''));
  }

  protected setKind(kind: ValueKind): void {
    this.valueKind.set(kind);
    if (kind === 'json') {
      this.text.set(format(this.rule()));
      this.raw.set(true);
      return;
    }
    this.raw.set(false);
  }

  protected emit(rule: JsonLogicRule | null): void {
    this.ruleChange.emit(rule);
  }
}

function kindOf(rule: JsonLogicRule | null | undefined): ValueKind {
  if (rule === null || rule === undefined) return 'variable';
  if (varPath(rule as JsonValue) !== null) return 'variable';
  if (typeof rule === 'number') return 'number';
  if (typeof rule === 'string') return 'text';
  return 'json';
}

function format(rule: JsonLogicRule | null | undefined): string {
  return rule === null || rule === undefined ? '' : JSON.stringify(rule, null, 1).replace(/\n\s*/g, ' ');
}

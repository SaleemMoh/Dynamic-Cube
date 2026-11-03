import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { I18nService } from '../i18n/i18n.service';
import { TranslatePipe } from '../i18n/translate.pipe';
import { JsonLogicRule, JsonValue, jsonLogic } from './jsonlogic';
import { LogicField, LogicValues } from './logic-fields';
import {
  LogicCondition,
  LogicValue,
  LogicWords,
  describeCondition,
  describeValue,
  readCondition,
  readValue,
  writeCondition,
  writeValue,
} from './logic-model';
import { LogicNodeEditor } from './logic-node';
import { choiceName, logicLabel, logicWords, registerLogicTexts } from './logic-text';

/** Field paths a rule reads at its own level (not the columns read inside a condition on rows). */
function pathsOf(node: LogicCondition | LogicValue, into: Set<string>): Set<string> {
  switch (node.kind) {
    case 'field':
      if (node.path) into.add(node.path);
      break;
    case 'group':
      node.items.forEach((i) => pathsOf(i, into));
      break;
    case 'compare':
      pathsOf(node.left, into);
      if (node.right) pathsOf(node.right, into);
      if (node.to) pathsOf(node.to, into);
      break;
    case 'rows':
    case 'aggregate':
      if (node.list) into.add(node.list);
      break;
    case 'math':
    case 'join':
      node.args.forEach((a) => pathsOf(a, into));
      break;
    case 'choose':
      node.cases.forEach((c) => {
        pathsOf(c.when, into);
        pathsOf(c.then, into);
      });
      pathsOf(node.otherwise, into);
      break;
    case 'test':
      pathsOf(node.condition, into);
      break;
  }
  return into;
}

/** A typed sample value: a number, yes/no, a JSON list or object, else the text. */
function loose(text: string): JsonValue {
  const t = text.trim();
  if (t === '') return null;
  if (t === 'true' || t === 'false') return t === 'true';
  if (!Number.isNaN(Number(t))) return Number(t);
  if (t.startsWith('[') || t.startsWith('{')) {
    try {
      return JSON.parse(t) as JsonValue;
    } catch {
      return t;
    }
  }
  return t;
}

function setPath(target: Record<string, JsonValue>, path: string, value: JsonValue): void {
  const parts = path.split('.');
  let node = target;
  for (const part of parts.slice(0, -1)) {
    if (typeof node[part] !== 'object' || node[part] === null || Array.isArray(node[part]))
      node[part] = {};
    node = node[part] as Record<string, JsonValue>;
  }
  node[parts[parts.length - 1]] = value;
}

/**
 * The Logic Builder: writes a JSONLogic rule through a form — fields chosen from a menu (answers, applicant, task,
 * table columns…), comparisons that fit the field's type, values chosen from the field's options, class or list of
 * values, groups of conditions, conditions on the rows of a list, and calculations (arithmetic, if/then, sum or count
 * of rows, joined texts). The rule is also said in words, can be tried with sample values, and is shown as JSON under
 * *Advanced* for experts; a piece the form cannot show is kept as JSON in its place.
 */
@Component({
  selector: 'dc-logic-builder',
  imports: [FormsModule, MatButtonModule, MatIconModule, TranslatePipe, LogicNodeEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="lb" [attr.data-testid]="testId()">
      <div class="lb__head">
        @if (label()) {
          <span class="lb__label">{{ label() }}</span>
        }
        <span class="lb__tools">
          <button
            matButton
            type="button"
            (click)="trying.set(!trying())"
            [class.lb__on]="trying()"
            data-testid="logic-try"
          >
            <mat-icon fontSet="material-symbols-outlined">science</mat-icon
            >{{ 'logic.try' | dcTranslate }}
          </button>
          <button
            matButton
            type="button"
            (click)="toggleAdvanced()"
            [class.lb__on]="advanced()"
            data-testid="logic-advanced"
          >
            <mat-icon fontSet="material-symbols-outlined">{{
              advanced() ? 'view_agenda' : 'data_object'
            }}</mat-icon
            >{{ (advanced() ? 'logic.form' : 'logic.advanced') | dcTranslate }}
          </button>
        </span>
      </div>

      @if (advanced()) {
        <textarea
          class="lb__json"
          dir="ltr"
          spellcheck="false"
          rows="5"
          [ngModel]="jsonText()"
          (ngModelChange)="typedJson($event)"
          data-testid="logic-json"
        ></textarea>
        @if (jsonError()) {
          <p class="lb__error" role="alert">{{ 'logic.invalidJson' | dcTranslate }}</p>
        }
      } @else {
        <dc-logic-node
          [role]="mode()"
          [node]="node()"
          [fields]="fields()"
          (nodeChange)="changed($event)"
        />
      }

      <p class="lb__sentence" data-testid="logic-sentence">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">notes</mat-icon>
        <span>{{ sentence() }}</span>
      </p>

      @if (trying()) {
        <div class="lb__try" data-testid="logic-try-panel">
          <p class="lb__hint">{{ 'logic.tryHint' | dcTranslate }}</p>
          @for (p of usedPaths(); track p) {
            <label class="lb__sample">
              <span
                >{{ labelOf(p) }} <small dir="ltr">{{ p }}</small></span
              >
              <input
                dir="auto"
                [ngModel]="samples()[p] ?? ''"
                (ngModelChange)="sample(p, $event)"
                [attr.data-testid]="'logic-sample-' + p"
              />
            </label>
          }
          <p class="lb__result" data-testid="logic-result">
            <strong>{{ 'logic.result' | dcTranslate }}:</strong>
            <code dir="ltr">{{ result() }}</code>
          </p>
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .lb {
      display: grid;
      gap: 6px;
    }
    .lb__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 6px;
    }
    .lb__label {
      font-size: 13px;
      font-weight: 600;
    }
    .lb__tools {
      display: inline-flex;
      gap: 2px;
      margin-inline-start: auto;
    }
    .lb__tools button {
      font-size: 12px;
    }
    .lb__on {
      background: var(--dga-gray-100, #f3f4f6);
    }
    .lb__json {
      width: 100%;
      box-sizing: border-box;
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
    .lb__error {
      margin: 0;
      color: #b42318;
      font-size: 12px;
    }
    .lb__sentence {
      display: flex;
      align-items: flex-start;
      gap: 6px;
      margin: 0;
      padding: 6px 8px;
      border-radius: 6px;
      background: var(--dga-gray-50, #f9fafb);
      color: var(--dga-text-2, #384250);
      font-size: 12px;
    }
    .lb__sentence mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    .lb__try {
      display: grid;
      gap: 6px;
      padding: 8px;
      border: 1px dashed var(--dga-gray-300, #d2d6db);
      border-radius: 8px;
    }
    .lb__hint {
      margin: 0;
      font-size: 12px;
      color: var(--dga-text-3, #6c737f);
    }
    .lb__sample {
      display: grid;
      grid-template-columns: minmax(10rem, 1fr) 2fr;
      gap: 8px;
      align-items: center;
      font-size: 12px;
    }
    .lb__sample small {
      color: var(--dga-text-3, #6c737f);
    }
    .lb__sample input {
      padding: 4px 6px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 4px;
    }
    .lb__result {
      margin: 0;
      font-size: 13px;
    }
  `,
})
export class LogicBuilder {
  private readonly i18n = inject(I18nService);
  private readonly values = inject(LogicValues);
  readonly rule = input<JsonLogicRule | null | undefined>(null);
  readonly fields = input<LogicField[]>([]);
  /** `condition` (true or false) or `value` (what the rule gives). */
  readonly mode = input<'condition' | 'value'>('condition');
  readonly label = input<string>('');
  readonly testId = input<string>('logic-builder');
  readonly ruleChange = output<JsonLogicRule | null>();

  /** The JSON last sent out: when it comes back as the input, the form keeps its own state (e.g. an unfinished row). */
  private lastEmitted: string | undefined;

  protected readonly node = linkedSignal<
    { rule: JsonLogicRule | null | undefined; mode: string },
    LogicCondition | LogicValue
  >({
    source: () => ({ rule: this.rule(), mode: this.mode() }),
    computation: (source, previous) => {
      if (previous && JSON.stringify(source.rule ?? null) === this.lastEmitted)
        return previous.value;
      return source.mode === 'condition' ? readCondition(source.rule) : readValue(source.rule);
    },
  });

  protected readonly advanced = signal(false);
  protected readonly trying = signal(false);
  protected readonly samples = signal<Record<string, string>>({});
  protected readonly jsonText = linkedSignal(() => JSON.stringify(this.written(), null, 2) ?? '');
  protected readonly jsonError = signal(false);

  private readonly written = computed<JsonLogicRule | null>(() => {
    const n = this.node();
    return this.mode() === 'condition'
      ? writeCondition(n as LogicCondition)
      : writeValue(n as LogicValue);
  });

  protected readonly sentence = computed(() => {
    const words = this.words();
    const label = (p: string) => this.labelOf(p);
    const n = this.node();
    return this.mode() === 'condition'
      ? describeCondition(n as LogicCondition, words, label)
      : describeValue(n as LogicValue, words, label);
  });

  protected readonly usedPaths = computed(() => [...pathsOf(this.node(), new Set<string>())]);

  protected readonly result = computed(() => {
    const data: Record<string, JsonValue> = {};
    for (const [path, text] of Object.entries(this.samples()))
      if (this.usedPaths().includes(path)) setPath(data, path, loose(text));
    try {
      const rule = this.written();
      if (rule === null) return this.mode() === 'condition' ? 'true' : 'null';
      const value =
        this.mode() === 'condition'
          ? jsonLogic.evaluateTruthy(rule, data)
          : jsonLogic.evaluate(rule, data);
      return JSON.stringify(value);
    } catch (e) {
      return e instanceof Error ? e.message : String(e);
    }
  });

  private readonly words = computed<LogicWords>(() => {
    const language = this.i18n.language();
    return {
      ...logicWords(this.i18n),
      valueName: (path, value) => choiceName(this.fields(), this.values, path, value, language),
    };
  });

  constructor() {
    registerLogicTexts(this.i18n);
  }

  protected labelOf(path: string): string {
    return logicLabel(this.fields(), path, this.i18n.language());
  }

  protected changed(node: LogicCondition | LogicValue): void {
    this.node.set(node);
    this.send(this.written());
  }

  private send(rule: JsonLogicRule | null): void {
    this.lastEmitted = JSON.stringify(rule ?? null);
    this.ruleChange.emit(rule);
  }

  protected toggleAdvanced(): void {
    this.jsonError.set(false);
    this.advanced.set(!this.advanced());
  }

  /** Typed JSON is taken as soon as it is valid; the box keeps the text as typed. */
  protected typedJson(text: string): void {
    let rule: JsonLogicRule | null;
    try {
      rule = text.trim() === '' ? null : (JSON.parse(text) as JsonLogicRule);
    } catch {
      this.jsonError.set(true);
      this.jsonText.set(text);
      return;
    }
    this.jsonError.set(false);
    this.node.set(this.mode() === 'condition' ? readCondition(rule) : readValue(rule));
    this.send(rule);
    this.jsonText.set(text);
  }

  protected sample(path: string, text: string): void {
    this.samples.update((s) => ({ ...s, [path]: text }));
  }
}

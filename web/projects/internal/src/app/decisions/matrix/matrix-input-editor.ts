import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  IntegrationApi,
  JsonLogicRule,
  LocalizePipe,
  LogicChip,
  LogicField,
  TestOperationResult,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ConfigEditor, ConfigEditorData } from '../../apps-control/config/config-editor';
import { SERVICE_SECTIONS } from '../../apps-control/config/module-sections';
import {
  INPUT_TYPES,
  Matrix,
  MatrixInput,
  MatrixSource,
  SOURCE_ICONS,
  SOURCE_KINDS,
  SourceKind,
  inputFields,
  projectFields,
} from './matrix-model';
import { MatrixReferences } from './matrix-references';

export interface MatrixInputEditorData {
  matrix: Matrix;
  /** The input to edit, or a new one (its code not used yet). */
  input: MatrixInput;
  isNew: boolean;
  references: MatrixReferences;
}

/** The answer type a question asking for this input has. */
const QUESTION_TYPES: Record<string, string> = {
  number: 'NUMBER',
  text: 'TEXT',
  option: 'SELECT',
  boolean: 'CHECKBOX',
  date: 'DATE',
  list: 'SELECT',
};

/**
 * One input of the matrix and where its value comes from: a question the applicant answers (picked from the bank or
 * created here), a field the reviewer fills in on a workflow task, an integration operation's output (tried here), a
 * party fact, a formula over other inputs, a sum or count over the applicant's projects, or a value given as it is.
 */
@Component({
  selector: 'dc-matrix-input-editor',
  imports: [
    FormsModule,
    JsonPipe,
    LocalizePipe,
    LogicChip,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ (data.isNew ? 'mx.input.new' : 'mx.input.edit') | dcTranslate }}
      @if (!data.isNew) {
        <span dir="ltr" class="mx-code">{{ input().code }}</span>
      }
    </h2>
    <mat-dialog-content class="mx-ie">
      <div class="mx-ie__row">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.code' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="input().code"
            (ngModelChange)="set({ code: $event })"
            [disabled]="!data.isNew"
            data-testid="mx-input-code"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.nameAr' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="rtl"
            [ngModel]="input().nameAr"
            (ngModelChange)="set({ nameAr: $event })"
            data-testid="mx-input-name-ar"
          />
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.nameEn' | dcTranslate }}</mat-label>
          <input
            matInput
            dir="ltr"
            [ngModel]="input().nameEn"
            (ngModelChange)="set({ nameEn: $event })"
          />
        </mat-form-field>
      </div>
      <div class="mx-ie__row">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>{{ 'mx.input.type' | dcTranslate }}</mat-label>
          <mat-select
            [ngModel]="input().type"
            (ngModelChange)="set({ type: $event })"
            data-testid="mx-input-type"
          >
            @for (t of types; track t) {
              <mat-option [value]="t">{{ 'mx.type.' + t | dcTranslate }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (input().type === 'option' || input().type === 'list') {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'mx.input.optionSet' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="input().optionSet ?? null"
              (ngModelChange)="set({ optionSet: $event })"
            >
              <mat-option [value]="null">—</mat-option>
              @for (s of data.matrix.optionSets; track s.code) {
                <mat-option [value]="s.code"
                  >{{ { ar: s.nameAr, en: s.nameEn } | dcLocalize }} ({{ s.code }})</mat-option
                >
              }
            </mat-select>
          </mat-form-field>
        }
        @if (input().type === 'number') {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'mx.input.unit' | dcTranslate }}</mat-label>
            <input
              matInput
              [ngModel]="input().unit ?? ''"
              (ngModelChange)="set({ unit: $event || null })"
            />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic" class="mx-narrow">
            <mat-label>{{ 'mx.min' | dcTranslate }}</mat-label>
            <input
              matInput
              type="number"
              dir="ltr"
              [ngModel]="input().min ?? null"
              (ngModelChange)="set({ min: num($event) })"
            />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic" class="mx-narrow">
            <mat-label>{{ 'mx.max' | dcTranslate }}</mat-label>
            <input
              matInput
              type="number"
              dir="ltr"
              [ngModel]="input().max ?? null"
              (ngModelChange)="set({ max: num($event) })"
            />
          </mat-form-field>
        }
      </div>

      <h3>{{ 'mx.source.title' | dcTranslate }}</h3>
      <mat-button-toggle-group
        class="mx-ie__kinds"
        [ngModel]="source().kind"
        (ngModelChange)="kind($event)"
        [attr.aria-label]="'mx.source.title' | dcTranslate"
        data-testid="mx-source-kind"
      >
        @for (k of kinds; track k) {
          <mat-button-toggle [value]="k" [attr.data-testid]="'mx-source-' + k">
            <mat-icon fontSet="material-symbols-outlined">{{ icons[k] }}</mat-icon>
            {{ 'mx.source.' + k | dcTranslate }}
          </mat-button-toggle>
        }
      </mat-button-toggle-group>
      <p class="dc-muted">{{ 'mx.source.' + source().kind + 'Hint' | dcTranslate }}</p>

      @switch (source().kind) {
        @case ('applicant') {
          <div class="mx-ie__row">
            <mat-form-field subscriptSizing="dynamic" class="mx-wide">
              <mat-label>{{ 'mx.source.question' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="source().question ?? null"
                (ngModelChange)="setSource({ question: $event })"
                data-testid="mx-source-question"
              >
                @if (questionMissing()) {
                  <mat-option [value]="source().question">{{ source().question }}</mat-option>
                }
                @for (q of refs.questions(); track q.code) {
                  <mat-option [value]="q.code"
                    >{{ { ar: q.nameAr, en: q.nameEn } | dcLocalize }} ({{ q.code }} ·
                    {{ q['type'] }})</mat-option
                  >
                }
              </mat-select>
            </mat-form-field>
            <button matButton type="button" (click)="newQuestion()" data-testid="mx-new-question">
              <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
              {{ 'mx.source.newQuestion' | dcTranslate }}
            </button>
            @if (source().question) {
              <button matButton type="button" (click)="editQuestion()">
                <mat-icon fontSet="material-symbols-outlined">edit</mat-icon>
                {{ 'mx.source.editQuestion' | dcTranslate }}
              </button>
            }
          </div>
          @if (questionMissing()) {
            <p class="mx-warn" data-testid="mx-question-missing">
              {{ 'mx.source.questionMissing' | dcTranslate }}
            </p>
          }
          @if (questionMismatch()) {
            <p class="mx-warn">{{ 'mx.source.typeMismatch' | dcTranslate }}</p>
          }
        }
        @case ('reviewer') {
          <div class="mx-ie__row">
            <mat-form-field subscriptSizing="dynamic" class="mx-wide">
              <mat-label>{{ 'mx.source.task' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="taskKey()"
                (ngModelChange)="chooseTask($event)"
                data-testid="mx-source-task"
              >
                @for (t of refs.tasks(); track t.workflow + '/' + t.step) {
                  <mat-option [value]="t.workflow + '|' + t.step"
                    >{{ t.workflowName | dcLocalize }} › {{ t.stepName | dcLocalize }} ({{
                      t.step
                    }})</mat-option
                  >
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'mx.source.field' | dcTranslate }}</mat-label>
              @if (taskFields().length) {
                <mat-select
                  [ngModel]="source().field ?? null"
                  (ngModelChange)="setSource({ field: $event })"
                  data-testid="mx-source-field"
                >
                  @for (f of taskFields(); track f.code) {
                    <mat-option [value]="f.code"
                      >{{ { ar: f.labelAr, en: f.labelEn } | dcLocalize }} ({{
                        f.code
                      }})</mat-option
                    >
                  }
                </mat-select>
              } @else {
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="source().field ?? ''"
                  (ngModelChange)="setSource({ field: $event || null })"
                  data-testid="mx-source-field"
                />
              }
            </mat-form-field>
          </div>
          @if (taskWorkflow(); as wf) {
            <p class="dc-muted">
              {{ 'mx.source.fieldMissing' | dcTranslate }}
              <a href="/workflows/designer?workflowCode={{ wf }}" target="_blank" rel="noopener">{{
                'mx.source.openDesigner' | dcTranslate
              }}</a>
            </p>
          }
        }
        @case ('integration') {
          <div class="mx-ie__row">
            <mat-form-field subscriptSizing="dynamic" class="mx-wide">
              <mat-label>{{ 'mx.source.operation' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="source().operation ?? null"
                (ngModelChange)="chooseOperation($event)"
                data-testid="mx-source-operation"
              >
                @if (source().operation && !operation()) {
                  <mat-option [value]="source().operation">{{ source().operation }}</mat-option>
                }
                @for (o of refs.operations(); track o.code) {
                  <mat-option [value]="o.code">{{ o.name | dcLocalize }} ({{ o.code }})</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'mx.source.output' | dcTranslate }}</mat-label>
              @if (!operation()) {
                <input
                  matInput
                  dir="ltr"
                  [ngModel]="source().output ?? ''"
                  (ngModelChange)="setSource({ output: $event || null })"
                  data-testid="mx-source-output"
                />
              } @else {
                <mat-select
                  [ngModel]="source().output ?? null"
                  (ngModelChange)="setSource({ output: $event })"
                  data-testid="mx-source-output"
                >
                  @for (o of operation()?.outputs ?? []; track o.name) {
                    <mat-option [value]="o.name"
                      >{{ o.name }} <span class="dc-muted" dir="ltr">{{ o.path }}</span></mat-option
                    >
                  }
                </mat-select>
              }
            </mat-form-field>
          </div>
          @if (source().operation) {
            <div class="mx-ie__row">
              <mat-form-field subscriptSizing="dynamic" class="mx-wide">
                <mat-label>{{ 'mx.source.callStep' | dcTranslate }}</mat-label>
                <mat-select
                  [ngModel]="source().step ?? null"
                  (ngModelChange)="setSource({ step: $event })"
                  data-testid="mx-source-call-step"
                >
                  <mat-option [value]="null">{{ 'mx.source.anyCall' | dcTranslate }}</mat-option>
                  @for (c of callSteps(); track c.workflow + '/' + c.step) {
                    <mat-option [value]="c.result"
                      >{{ c.workflowName | dcLocalize }} › {{ c.stepName | dcLocalize }} ({{
                        c.step
                      }})</mat-option
                    >
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'mx.source.formAnswer' | dcTranslate }}</mat-label>
                <mat-select
                  [ngModel]="source().question ?? null"
                  (ngModelChange)="setSource({ question: $event })"
                >
                  <mat-option [value]="null">—</mat-option>
                  @for (q of refs.questions(); track q.code) {
                    <mat-option [value]="q.code"
                      >{{ { ar: q.nameAr, en: q.nameEn } | dcLocalize }} ({{ q.code }})</mat-option
                    >
                  }
                </mat-select>
              </mat-form-field>
            </div>
            @if (!callSteps().length && !formCalls().length && !source().question) {
              <p class="mx-warn" data-testid="mx-operation-not-called">
                {{ 'mx.source.notCalled' | dcTranslate }}
              </p>
            } @else {
              <p class="dc-muted">{{ 'mx.source.calledBy' | dcTranslate }} {{ calledBy() }}</p>
            }
          }
          @if (source().operation && !operation()) {
            <p class="mx-warn" data-testid="mx-operation-missing">
              {{ 'mx.source.operationMissing' | dcTranslate }}
              <a routerLink="/integration/operations" target="_blank">{{
                'mx.source.openOperations' | dcTranslate
              }}</a>
            </p>
          }
          @if (operation(); as op) {
            @if (op.parameters.length) {
              <table class="mx-ie__params">
                <thead>
                  <tr>
                    <th>{{ 'mx.source.parameter' | dcTranslate }}</th>
                    <th>{{ 'mx.source.sentAs' | dcTranslate }}</th>
                    <th>{{ 'mx.source.tryValue' | dcTranslate }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (p of op.parameters; track p.name) {
                    <tr>
                      <td dir="ltr" class="mx-code">{{ p.name }}{{ p.required ? ' *' : '' }}</td>
                      <td>
                        <dc-logic-chip
                          mode="value"
                          [rule]="source().parameters?.[p.name] ?? null"
                          [fields]="parameterFields()"
                          [title]="p.name"
                          (ruleChange)="setParameter(p.name, $event)"
                        />
                      </td>
                      <td>
                        <input
                          class="mx-ie__try-input"
                          dir="ltr"
                          [ngModel]="tryValues()[p.name] ?? p.sample ?? ''"
                          (ngModelChange)="setTry(p.name, $event)"
                        />
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
            <div class="mx-ie__row">
              <button
                matButton="tonal"
                type="button"
                (click)="tryOperation()"
                [disabled]="trying()"
                data-testid="mx-try-operation"
              >
                <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon>
                {{ 'mx.source.try' | dcTranslate }}
              </button>
            </div>
            @if (tryResult(); as r) {
              <div class="mx-ie__result" data-testid="mx-try-result">
                <p>
                  <span class="dc-badge" [class.dc-badge--success]="r.outcome === 'SUCCESS'">{{
                    r.outcome
                  }}</span>
                  {{ { ar: r.messageAr ?? '', en: r.messageEn ?? '' } | dcLocalize }}
                </p>
                <table class="mx-ie__outputs">
                  @for (o of outputRows(); track o.name) {
                    <tr [class.mx-ie__picked]="o.name === source().output">
                      <td dir="ltr" class="mx-code">{{ o.name }}</td>
                      <td dir="ltr" class="mx-code">{{ o.value | json }}</td>
                      <td>
                        <button matButton type="button" (click)="setSource({ output: o.name })">
                          {{ 'mx.source.useOutput' | dcTranslate }}
                        </button>
                      </td>
                    </tr>
                  }
                </table>
              </div>
            }
          }
        }
        @case ('party') {
          <mat-form-field subscriptSizing="dynamic" class="mx-wide">
            <mat-label>{{ 'mx.source.partyFact' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="source().path ?? null"
              (ngModelChange)="setSource({ path: $event })"
              data-testid="mx-source-party"
            >
              @for (f of refs.partyFacts(); track f.code) {
                <mat-option [value]="f.code">{{ f.name | dcLocalize }} ({{ f.code }})</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        @case ('derived') {
          <span class="dc-field-label">{{ 'mx.source.formula' | dcTranslate }}</span>
          <dc-logic-chip
            mode="value"
            [rule]="source().rule ?? null"
            [fields]="otherInputs()"
            [title]="'mx.source.formula' | dcTranslate"
            (ruleChange)="setSource({ rule: $event })"
            data-testid="mx-source-formula"
          />
        }
        @case ('projects') {
          <div class="mx-ie__row">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'mx.source.function' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="source().function ?? 'sum'"
                (ngModelChange)="setSource({ function: $event })"
              >
                @for (f of functions; track f) {
                  <mat-option [value]="f">{{ 'mx.fn.' + f | dcTranslate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            @if (source().function !== 'count') {
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>{{ 'mx.source.column' | dcTranslate }}</mat-label>
                <mat-select
                  [ngModel]="source().column ?? null"
                  (ngModelChange)="setSource({ column: $event })"
                >
                  @for (f of projectColumns(); track f.path) {
                    <mat-option [value]="f.path"
                      >{{ f.label | dcLocalize }} ({{ f.path }})</mat-option
                    >
                  }
                </mat-select>
              </mat-form-field>
            }
          </div>
          <span class="dc-field-label">{{ 'mx.source.where' | dcTranslate }}</span>
          <dc-logic-chip
            mode="condition"
            [rule]="source().where ?? null"
            [fields]="projectColumns()"
            [title]="'mx.source.where' | dcTranslate"
            (ruleChange)="setSource({ where: $event })"
          />
        }
      }
      @if (
        source().kind === 'integration' ||
        source().kind === 'derived' ||
        source().kind === 'projects' ||
        source().kind === 'party'
      ) {
        <mat-checkbox
          [ngModel]="source().allowOverride === true"
          (ngModelChange)="setSource({ allowOverride: $event })"
        >
          {{ 'mx.source.allowOverride' | dcTranslate }}
        </mat-checkbox>
      }
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (e of errors(); track $index) {
            <li>{{ e }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (!data.isNew) {
        <button
          matButton
          type="button"
          class="mx-danger"
          (click)="remove()"
          data-testid="mx-input-remove"
        >
          {{ 'mx.remove' | dcTranslate }}
        </button>
      }
      <span class="mx-spacer"></span>
      <button matButton mat-dialog-close type="button">{{ 'form.cancel' | dcTranslate }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="done()"
        [disabled]="!valid()"
        data-testid="mx-input-done"
      >
        {{ 'logic.done' | dcTranslate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .mx-ie {
      display: grid;
      gap: 0.75rem;
    }
    .mx-ie h3 {
      margin: 0.5rem 0 0;
      font-size: 16px;
    }
    .mx-ie__row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
    }
    .mx-ie__row mat-form-field {
      flex: 1 1 12rem;
    }
    .mx-ie__row .mx-narrow {
      flex: 0 1 7rem;
    }
    .mx-ie__row .mx-wide,
    .mx-wide {
      flex: 2 1 22rem;
      width: 100%;
    }
    .mx-ie__kinds {
      flex-wrap: wrap;
    }
    .mx-ie__kinds mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      vertical-align: middle;
    }
    .mx-ie__params,
    .mx-ie__outputs {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .mx-ie__params th {
      text-align: start;
      font-weight: 600;
      padding: 4px;
    }
    .mx-ie__params td,
    .mx-ie__outputs td {
      padding: 4px;
      border-top: 1px solid var(--dga-gray-200, #e5e7eb);
      vertical-align: middle;
    }
    .mx-ie__try-input {
      width: 100%;
      padding: 6px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      font: inherit;
    }
    .mx-ie__result {
      padding: 0.5rem 0.75rem;
      background: var(--dga-gray-50, #f9fafb);
      border-radius: 8px;
    }
    .mx-ie__picked {
      background: color-mix(in srgb, var(--mat-sys-primary, #1b8354) 12%, transparent);
    }
    .mx-code {
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .mx-warn {
      color: var(--dga-warning-700, #b54708);
      margin: 0;
    }
    .mx-danger {
      color: var(--dga-error-600, #d92d20);
    }
    .mx-spacer {
      flex: 1;
    }
  `,
})
export class MatrixInputEditor {
  protected readonly data = inject<MatrixInputEditorData>(MAT_DIALOG_DATA);
  private readonly ref = inject(
    MatDialogRef<MatrixInputEditor, { input: MatrixInput | null; removed?: boolean }>,
  );
  private readonly dialog = inject(MatDialog);
  private readonly integration = inject(IntegrationApi);
  protected readonly refs = this.data.references;
  protected readonly types = INPUT_TYPES;
  protected readonly kinds = SOURCE_KINDS;
  protected readonly icons = SOURCE_ICONS;
  protected readonly functions = ['sum', 'max', 'count', 'distinct'] as const;

  protected readonly input = signal<MatrixInput>(structuredClone(this.data.input));
  protected readonly source = computed(() => this.input().source);
  protected readonly errors = signal<string[]>([]);
  protected readonly tryValues = signal<Record<string, string | undefined>>({});
  protected readonly tryResult = signal<TestOperationResult | null>(null);
  protected readonly trying = signal(false);

  protected readonly otherInputs = computed<LogicField[]>(() =>
    inputFields(this.data.matrix, this.input().code),
  );
  protected readonly projectColumns = computed<LogicField[]>(() => projectFields(this.data.matrix));
  protected readonly parameterFields = computed<LogicField[]>(() => [
    ...this.otherInputs(),
    ...this.refs.partyFacts().map((f) => ({
      path: f.code,
      label: f.name,
      group: { ar: 'بيانات المنشأة', en: 'Party data' },
    })),
  ]);
  protected readonly operation = computed(() => this.refs.operation(this.source().operation));
  /** Workflow steps that call the chosen operation (one can be named when the operation is called more than once). */
  protected readonly callSteps = computed(() =>
    this.refs
      .integrationSteps()
      .filter((c) => c.operation.toLowerCase() === (this.source().operation ?? '').toLowerCase()),
  );
  /** Services whose form calls the operation in an integration step. */
  protected readonly formCalls = computed(
    () => this.refs.formOperations().get(this.source().operation ?? '') ?? [],
  );
  protected readonly calledBy = computed(() =>
    [
      ...this.callSteps().map((c) => `${c.workflow} › ${c.step}`),
      ...this.formCalls().map((f) => `${f} (form)`),
    ].join(', '),
  );
  protected readonly outputRows = computed(() =>
    Object.entries(this.tryResult()?.outputs ?? {}).map(([name, value]) => ({ name, value })),
  );
  protected readonly taskKey = computed(() => {
    const s = this.source();
    const task = this.refs
      .tasks()
      .find((t) => t.step === s.step && (!s.path || t.workflow === s.path));
    return task ? `${task.workflow}|${task.step}` : null;
  });
  protected readonly taskWorkflow = computed(() => this.taskKey()?.split('|')[0] ?? null);
  protected readonly taskFields = computed(() => {
    const key = this.taskKey();
    return key
      ? (this.refs.tasks().find((t) => `${t.workflow}|${t.step}` === key)?.fields ?? [])
      : [];
  });
  /** A question named by an imported guide that the question bank does not hold yet. */
  protected readonly questionMissing = computed(
    () => !!this.source().question && !this.refs.question(this.source().question),
  );
  protected readonly questionMismatch = computed(() => {
    const q = this.refs.question(this.source().question);
    return (
      !!q &&
      QUESTION_TYPES[this.input().type] !== q['type'] &&
      !(this.input().type === 'text' && q['type'] === 'TEXTAREA')
    );
  });
  protected readonly valid = computed(
    () =>
      /^[A-Za-z][A-Za-z0-9_-]*$/.test(this.input().code) &&
      (!this.data.isNew || !this.data.matrix.inputs.some((i) => i.code === this.input().code)),
  );

  protected num(value: unknown): number | null {
    return value === '' || value === null || value === undefined ? null : Number(value);
  }

  protected set(change: Partial<MatrixInput>): void {
    this.input.update((i) => ({ ...i, ...change }));
  }

  protected setSource(change: Partial<MatrixSource>): void {
    this.input.update((i) => ({ ...i, source: { ...i.source, ...change } }));
  }

  /** A new source kind keeps only what applies to it. */
  protected kind(kind: SourceKind): void {
    this.tryResult.set(null);
    this.input.update((i) => ({ ...i, source: { kind, allowOverride: i.source.allowOverride } }));
  }

  /** The workflow is kept in `path` so a step code repeated in two workflows stays clear. */
  protected chooseTask(key: string): void {
    const [workflow, step] = key.split('|');
    this.setSource({ path: workflow, step, field: null });
  }

  protected chooseOperation(code: string): void {
    const op = this.refs.operation(code);
    this.tryResult.set(null);
    this.setSource({
      operation: code,
      output: op?.outputs.length === 1 ? op.outputs[0].name : null,
      parameters: {},
    });
  }

  protected setParameter(name: string, rule: JsonLogicRule | null): void {
    this.setSource({ parameters: { ...(this.source().parameters ?? {}), [name]: rule } });
  }

  protected setTry(name: string, value: string): void {
    this.tryValues.update((v) => ({ ...v, [name]: value }));
  }

  protected async tryOperation(): Promise<void> {
    const op = this.operation();
    if (!op) return;
    this.errors.set([]);
    this.trying.set(true);
    const inputs = Object.fromEntries(
      op.parameters.map((p) => [p.name, this.tryValues()[p.name] ?? p.sample ?? null]),
    );
    try {
      this.tryResult.set(await firstValueFrom(this.integration.test(op.code, inputs)));
    } catch (error) {
      this.errors.set(problemMessages(error));
    } finally {
      this.trying.set(false);
    }
  }

  /** Opens the question bank's own editor for a new question named after this input. */
  protected async newQuestion(): Promise<void> {
    const i = this.input();
    const code = this.source().question || i.code.toUpperCase().replace(/-/g, '_');
    const saved = await this.openQuestion(null, {
      code,
      nameAr: i.nameAr,
      nameEn: i.nameEn,
      type: QUESTION_TYPES[i.type] ?? 'TEXT',
      multiple: i.type === 'list',
    });
    if (saved) {
      this.refs.loadQuestions();
      this.setSource({ question: code });
    }
  }

  protected async editQuestion(): Promise<void> {
    const row = this.refs.question(this.source().question);
    if (row && (await this.openQuestion(row))) this.refs.loadQuestions();
  }

  private async openQuestion(
    row: ConfigEditorData['row'],
    defaults?: Record<string, unknown>,
  ): Promise<boolean> {
    const type = SERVICE_SECTIONS.flatMap((s) => s.types).find(
      (t) => t.entityType === 'svc.question',
    );
    if (!type) return false;
    const saved = await firstValueFrom(
      this.dialog
        .open<ConfigEditor, ConfigEditorData, boolean>(ConfigEditor, {
          data: { type, row, defaults },
          width: '48rem',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    return saved === true;
  }

  protected remove(): void {
    this.ref.close({ input: null, removed: true });
  }

  protected done(): void {
    this.ref.close({ input: this.input() });
  }
}

import { DatePipe, JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppsControlApi,
  ConfigRow,
  DecisionResult,
  DecisionsApi,
  EvaluationView,
  LocalizePipe,
  StructuredColumn,
  StructuredInput,
  StructuredShape,
  TraceStep,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { DraftsService } from '../apps-control/drafts.service';
import { PrivilegesService } from '../apps-control/privileges.service';
import { SubmitButton } from '../apps-control/submit-button';

const t = (ar: string, en: string) => ({ ar, en });
/** Named steps: a code and a JSONLogic rule that may read input, vars and earlier steps. */
const STEPS: StructuredColumn[] = [
  { key: 'code', label: t('الاسم', 'Name'), type: 'text', ltr: true, required: true },
  { key: 'rule', label: t('القاعدة (JSONLogic)', 'Rule (JSONLogic)'), type: 'value', required: true },
];

/** The built-in RULES engine's configuration: steps, per-item results and the overall output. */
const RULES_SHAPE: StructuredShape = {
  kind: 'object',
  properties: [
    { key: 'variables', label: t('الخطوات', 'Steps'), type: 'table', columns: STEPS },
    {
      key: 'items',
      label: t('نتيجة لكل عنصر (اختياري)', 'A result per element (optional)'),
      type: 'object',
      columns: [
        { key: 'source', label: t('العناصر (قاعدة)', 'Elements (rule)'), type: 'value' },
        { key: 'as', label: t('اسم العنصر', 'Element name'), type: 'text', ltr: true },
        { key: 'variables', label: t('خطوات العنصر', 'Element steps'), type: 'table', columns: STEPS },
        { key: 'output', label: t('نتيجة العنصر', 'Element result'), type: 'map' },
      ],
    },
    { key: 'output', label: t('النتيجة', 'Result'), type: 'map' },
  ],
};

/** A first version of the built-in RULES engine: variables, per-item results and the overall output. */
const STARTER = {
  variables: [{ code: 'score', rule: { '+': [{ var: 'input.a' }, { var: 'input.b' }] } }],
  items: {
    source: { var: 'input.sectors' },
    as: 'sector',
    variables: [{ code: 'grade', rule: { if: [{ '>=': [{ var: 'vars.score' }, 80] }, 1, { '>=': [{ var: 'vars.score' }, 50] }, 3, 5] } }],
    output: { sector: { var: 'sector' }, grade: { var: 'vars.grade' } },
  },
  output: { score: { var: 'vars.score' }, eligible: { '>=': [{ var: 'vars.score' }, 50] } },
};

const SAMPLE_INPUT = { a: 40, b: 35, sectors: ['BUILDINGS', 'ROADS'] };

/** The test console: input as JSON, evaluated by a chosen version (recorded as a TRY evaluation). */
@Component({
  selector: 'dc-decision-try-dialog',
  imports: [FormsModule, JsonPipe, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatTableModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title dir="ltr">{{ data.ruleSet }}/{{ data.version }}</h2>
    <mat-dialog-content class="dc-try">
      <div>
        <label class="dc-field-label" for="dc-try-input">{{ 'decisions.input' | dcTranslate }}</label>
        <mat-form-field subscriptSizing="dynamic" class="dc-try__input">
          <textarea id="dc-try-input" matInput dir="ltr" rows="16" spellcheck="false" [(ngModel)]="input" data-testid="try-input"></textarea>
        </mat-form-field>
        <button matButton="filled" type="button" (click)="run()" data-testid="try-run">
          <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon>
          {{ 'decisions.run' | dcTranslate }}
        </button>
      </div>
      <div>
        @if (errors().length) {
          <ul class="dc-errors" role="alert">
            @for (e of errors(); track $index) {
              <li dir="ltr">{{ e }}</li>
            }
          </ul>
        }
        @if (result(); as r) {
          <h3>{{ 'decisions.output' | dcTranslate }}</h3>
          <pre dir="ltr" class="dc-json" data-testid="try-output">{{ r.output | json }}</pre>
          <h3>{{ 'decisions.trace' | dcTranslate }}</h3>
          <table mat-table [dataSource]="r.trace" class="dc-table" dir="ltr">
            <ng-container matColumnDef="step">
              <th mat-header-cell *matHeaderCellDef>{{ 'decisions.step' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let t" class="dc-code">{{ t.step }}</td>
            </ng-container>
            <ng-container matColumnDef="value">
              <th mat-header-cell *matHeaderCellDef>{{ 'decisions.value' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let t" class="dc-code">{{ show(t) }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="traceColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: traceColumns"></tr>
          </table>
        }
      </div>
    </mat-dialog-content>
    <mat-dialog-actions
      ><button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button></mat-dialog-actions
    >
  `,
  styles: `
    .dc-try {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(0, 3fr);
      gap: 1.5rem;
    }
    @media (max-width: 900px) {
      .dc-try {
        grid-template-columns: 1fr;
      }
    }
    .dc-try__input {
      width: 100%;
    }
    .dc-try textarea,
    .dc-code,
    .dc-json {
      text-align: left;
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-json {
      text-align: left;
      margin: 0;
      padding: 0.75rem;
      background: var(--dga-gray-50, #f9fafb);
      border-radius: 8px;
      overflow: auto;
      max-height: 16rem;
    }
    h3 {
      font-size: 16px;
    }
  `,
})
export class DecisionTryDialog {
  protected readonly data = inject<{ ruleSet: string; version: number; input: unknown }>(MAT_DIALOG_DATA);
  private readonly api = inject(DecisionsApi);
  protected readonly traceColumns = ['step', 'value'];
  protected input = JSON.stringify(this.data.input ?? SAMPLE_INPUT, null, 2);
  protected readonly result = signal<DecisionResult | null>(null);
  protected readonly errors = signal<string[]>([]);

  protected show(step: TraceStep): string {
    return traceText(step);
  }

  protected async run(): Promise<void> {
    this.errors.set([]);
    let input: unknown;
    try {
      input = JSON.parse(this.input);
    } catch {
      this.errors.set(['Input is not valid JSON.']);
      return;
    }
    try {
      this.result.set(await firstValueFrom(this.api.try(this.data.ruleSet, input, this.data.version)));
    } catch (error) {
      this.result.set(null);
      this.errors.set(problemMessages(error));
    }
  }
}

/** A recorded evaluation: input, output and trace. */
@Component({
  selector: 'dc-evaluation-dialog',
  imports: [DatePipe, JsonPipe, MatButtonModule, MatDialogModule, MatTableModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title dir="ltr">{{ data.ruleSetCode }}/{{ data.version }} · {{ data.subjectNumber ?? data.subjectType }}</h2>
    <mat-dialog-content class="dc-eval">
      <p class="dc-muted" dir="ltr">{{ data.at | date: 'yyyy-MM-dd HH:mm:ss' }} · {{ data.by }} · {{ data.durationMs }} ms</p>
      <div class="dc-eval__io">
        <div>
          <h3>{{ 'decisions.input' | dcTranslate }}</h3>
          <pre dir="ltr" class="dc-json">{{ data.input | json }}</pre>
        </div>
        <div>
          <h3>{{ 'decisions.output' | dcTranslate }}</h3>
          <pre dir="ltr" class="dc-json">{{ data.output | json }}</pre>
        </div>
      </div>
      <h3>{{ 'decisions.trace' | dcTranslate }}</h3>
      <table mat-table [dataSource]="data.trace" class="dc-table" dir="ltr">
        <ng-container matColumnDef="step">
          <th mat-header-cell *matHeaderCellDef>{{ 'decisions.step' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let t" class="dc-code">{{ t.step }}</td>
        </ng-container>
        <ng-container matColumnDef="value">
          <th mat-header-cell *matHeaderCellDef>{{ 'decisions.value' | dcTranslate }}</th>
          <td mat-cell *matCellDef="let t" class="dc-code">{{ show(t) }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="traceColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: traceColumns"></tr>
      </table>
    </mat-dialog-content>
    <mat-dialog-actions
      ><button matButton mat-dialog-close>{{ 'common.close' | dcTranslate }}</button></mat-dialog-actions
    >
  `,
  styles: `
    .dc-eval__io {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 1rem;
    }
    .dc-code,
    .dc-json {
      text-align: left;
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-json {
      text-align: left;
      margin: 0;
      padding: 0.75rem;
      background: var(--dga-gray-50, #f9fafb);
      border-radius: 8px;
      overflow: auto;
      max-height: 16rem;
    }
    h3 {
      font-size: 16px;
    }
  `,
})
export class EvaluationDialog {
  protected readonly data = inject<EvaluationView>(MAT_DIALOG_DATA);
  protected readonly traceColumns = ['step', 'value'];

  protected show(step: TraceStep): string {
    return traceText(step);
  }
}

/** A trace step's value as compact JSON. */
export function traceText(step: TraceStep): string {
  const value = 'value' in step ? step.value : Object.fromEntries(Object.entries(step).filter(([k]) => k !== 'step'));
  return JSON.stringify(value);
}

/**
 * Rule-set versions. A new version starts from the latest one's configuration, is checked by its engine when added to a
 * change set and never changes once published; the console tries any version on a sample input.
 */
@Component({
  selector: 'dc-rule-set-versions',
  imports: [
    SubmitButton,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
    StructuredInput,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="dc-page__header">
      <h1>{{ 'decisions.versionsTitle' | dcTranslate }}</h1>
    </header>
    <p class="dc-muted">{{ 'decisions.versionsHint' | dcTranslate }}</p>
    <div class="dc-toolbar">
      <mat-form-field subscriptSizing="dynamic" class="dc-rulesets__pick">
        <mat-label>{{ 'decisions.ruleSet' | dcTranslate }}</mat-label>
        <mat-select [ngModel]="ruleSet()" (ngModelChange)="choose($event)" data-testid="rule-set">
          @for (r of ruleSets.value() ?? []; track r.code) {
            <mat-option [value]="r.code">{{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }} ({{ r.code }})</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    @if (ruleSet(); as code) {
      <mat-tab-group mat-stretch-tabs="false" animationDuration="0ms">
        <mat-tab [label]="'decisions.versions' | dcTranslate">
          <table mat-table [dataSource]="rows()" class="dc-table">
            <ng-container matColumnDef="code">
              <th mat-header-cell *matHeaderCellDef>{{ 'versions.version' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr" class="dc-code">{{ row.code }}</td>
            </ng-container>
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>{{ 'config.name' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row">{{ { ar: row.nameAr, en: row.nameEn } | dcLocalize }}</td>
            </ng-container>
            <ng-container matColumnDef="published">
              <th mat-header-cell *matHeaderCellDef>{{ 'versions.published' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let row" dir="ltr">{{ row['publishedAt'] | date: 'yyyy-MM-dd HH:mm' }}</td>
            </ng-container>
            <ng-container matColumnDef="state">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let row">
                @if (row['isRetired']) {
                  <span class="dc-badge">{{ 'versions.retired' | dcTranslate }}</span>
                } @else if (row.code === current()) {
                  <span class="dc-badge dc-badge--success">{{ 'decisions.current' | dcTranslate }}</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let row" class="dc-table__actions">
                <button matIconButton (click)="tryVersion(row)" [matTooltip]="'decisions.try' | dcTranslate" [attr.data-testid]="'try-' + row.code">
                  <mat-icon fontSet="material-symbols-outlined">science</mat-icon>
                </button>
                <button matIconButton (click)="edit(row)" [matTooltip]="'decisions.startFrom' | dcTranslate">
                  <mat-icon fontSet="material-symbols-outlined">content_copy</mat-icon>
                </button>
                @if (canEdit() && !row['isRetired']) {
                  <button matIconButton (click)="retire(row)" [matTooltip]="'versions.retire' | dcTranslate">
                    <mat-icon fontSet="material-symbols-outlined">archive</mat-icon>
                  </button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
          @if (rows().length === 0) {
            <p class="dc-empty">{{ 'decisions.noVersions' | dcTranslate }}</p>
          }

          @if (canCreate()) {
            <section class="dc-card dc-authoring">
              <h2>
                {{ 'versions.new' | dcTranslate }} <span dir="ltr" class="dc-code">{{ code }}/{{ nextNumber() }}</span>
              </h2>
              <span class="dc-field-label" id="dc-dec-config">{{ 'decisions.configuration' | dcTranslate }}</span>
              <dc-structured-input
                [shape]="configShape"
                [ngModel]="parsed()"
                (ngModelChange)="text.set(stringify($event))"
                testId="rule-set-config"
                aria-labelledby="dc-dec-config"
              />
              @if (!parsed()) {
                <p class="dc-errors" role="alert">{{ 'decisions.invalidJson' | dcTranslate }}</p>
              }
              @if (errors().length) {
                <ul class="dc-errors" role="alert">
                  @for (e of errors(); track $index) {
                    <li dir="ltr">{{ e }}</li>
                  }
                </ul>
              }
              <div class="dc-authoring__actions">
                <dc-submit-button [entityType]="'dec.rule-set-version'" [disabled]="!parsed()" (submitted)="addVersion(true)" />
                <button matButton="filled" [disabled]="!parsed()" (click)="addVersion()" data-testid="add-rule-set-version">
                  <mat-icon fontSet="material-symbols-outlined">add</mat-icon>
                  {{ drafts.saveLabel('dec.rule-set-version') | dcTranslate }}
                </button>
              </div>
            </section>
          }
        </mat-tab>
        <mat-tab [label]="'decisions.evaluations' | dcTranslate">
          <table mat-table [dataSource]="evaluations.value() ?? []" class="dc-table">
            <ng-container matColumnDef="at">
              <th mat-header-cell *matHeaderCellDef>{{ 'decisions.at' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let e" dir="ltr">{{ e.at | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
            </ng-container>
            <ng-container matColumnDef="version">
              <th mat-header-cell *matHeaderCellDef>{{ 'versions.version' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let e">{{ e.version }}</td>
            </ng-container>
            <ng-container matColumnDef="subject">
              <th mat-header-cell *matHeaderCellDef>{{ 'decisions.subject' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let e" dir="ltr">
                {{ e.subjectType === 'TRY' ? ('decisions.tryRun' | dcTranslate) : (e.subjectNumber ?? e.subjectId) }}
              </td>
            </ng-container>
            <ng-container matColumnDef="by">
              <th mat-header-cell *matHeaderCellDef>{{ 'decisions.by' | dcTranslate }}</th>
              <td mat-cell *matCellDef="let e" dir="ltr">{{ e.by }}</td>
            </ng-container>
            <ng-container matColumnDef="duration">
              <th mat-header-cell *matHeaderCellDef>ms</th>
              <td mat-cell *matCellDef="let e">{{ e.durationMs }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let e" class="dc-table__actions">
                <button matIconButton (click)="showEvaluation(e)" [attr.aria-label]="'decisions.trace' | dcTranslate">
                  <mat-icon fontSet="material-symbols-outlined">visibility</mat-icon>
                </button>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="evaluationColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: evaluationColumns"></tr>
          </table>
          @if ((evaluations.value() ?? []).length === 0) {
            <p class="dc-empty">{{ 'decisions.noEvaluations' | dcTranslate }}</p>
          }
        </mat-tab>
      </mat-tab-group>
    }
  `,
  styles: `
    .dc-rulesets__pick {
      width: min(36rem, 100%);
    }
    .dc-authoring {
      display: grid;
      gap: 0.25rem;
      margin-top: 1.5rem;
    }
    .dc-authoring h2 {
      margin-top: 0;
    }
    .dc-authoring mat-form-field {
      width: 100%;
    }
    .dc-json,
    .dc-code {
      font-family: ui-monospace, monospace;
      font-size: 13px;
    }
    .dc-authoring__actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 0.5rem;
    }
  `,
})
export class RuleSetVersions {
  private readonly api = inject(AppsControlApi);
  private readonly decisions = inject(DecisionsApi);
  private readonly dialog = inject(MatDialog);
  protected readonly drafts = inject(DraftsService);
  private readonly privileges = inject(PrivilegesService);
  protected readonly columns = ['code', 'name', 'published', 'state', 'actions'];
  protected readonly evaluationColumns = ['at', 'version', 'subject', 'by', 'duration', 'actions'];
  protected readonly ruleSets = rxResource({ stream: () => this.api.list('dec.rule-set') });
  private readonly versions = rxResource({ stream: () => this.api.list('dec.rule-set-version') });
  protected readonly ruleSet = signal<string | null>(null);
  protected readonly evaluations = rxResource({
    params: () => this.ruleSet(),
    stream: ({ params }) => this.decisions.evaluations({ ruleSet: params, take: 100 }),
  });
  protected readonly text = signal('');
  protected readonly configShape = RULES_SHAPE;
  protected readonly errors = signal<string[]>([]);
  protected readonly canCreate = computed(() => this.privileges.can('decisions.config', 'CREATE'));
  protected readonly canEdit = computed(() => this.privileges.can('decisions.config', 'EDIT'));

  protected readonly rows = computed(() => {
    const prefix = `${this.ruleSet()}/`;
    return (this.versions.value() ?? []).filter((r) => r.code.startsWith(prefix)).sort((a, b) => number(b.code) - number(a.code));
  });

  /** The version requests are decided by: the highest one not retired. */
  protected readonly current = computed(() => this.rows().find((r) => !r['isRetired'] && r['publishedAt'])?.code ?? null);
  protected readonly nextNumber = computed(() => 1 + Math.max(0, ...this.rows().map((r) => number(r.code))));
  protected readonly parsed = computed<unknown>(() => {
    try {
      return JSON.parse(this.text()) as unknown;
    } catch {
      return null;
    }
  });

  /** The configuration as saved: items only when it has elements. */
  protected stringify(value: unknown): string {
    const config = { ...((value as Record<string, unknown>) ?? {}) };
    const items = config['items'] as Record<string, unknown> | undefined;
    if (items && !items['source']) delete config['items'];
    return JSON.stringify(config, null, 2);
  }

  protected choose(code: string): void {
    this.ruleSet.set(code);
    this.errors.set([]);
    const latest = this.rows()[0];
    this.text.set(JSON.stringify(latest?.['configuration'] ?? STARTER, null, 2));
  }

  protected edit(row: ConfigRow): void {
    this.text.set(JSON.stringify(row['configuration'] ?? STARTER, null, 2));
  }

  protected tryVersion(row: ConfigRow): void {
    this.dialog.open(DecisionTryDialog, { data: { ruleSet: this.ruleSet()!, version: number(row.code), input: null }, width: '80rem', maxWidth: '96vw' });
  }

  protected showEvaluation(evaluation: EvaluationView): void {
    this.dialog.open(EvaluationDialog, { data: evaluation, width: '72rem', maxWidth: '96vw' });
  }

  protected async addVersion(submit = false): Promise<void> {
    const configuration = this.parsed();
    if (!configuration) return;
    this.errors.set([]);
    const n = this.nextNumber();
    try {
      await this.drafts.addChange('dec.rule-set-version', `${this.ruleSet()}/${n}`, 'CREATE', {
        nameAr: `الإصدار ${n}`,
        nameEn: `Version ${n}`,
        configuration,
      }, { submit });
      this.versions.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  protected async retire(row: ConfigRow): Promise<void> {
    this.errors.set([]);
    try {
      await this.drafts.addChange('dec.rule-set-version', row.code, 'UPDATE', { ...row, isRetired: true });
      this.versions.reload();
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }
}

function number(code: string): number {
  return Number(code.slice(code.lastIndexOf('/') + 1));
}

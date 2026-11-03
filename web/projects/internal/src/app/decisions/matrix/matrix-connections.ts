import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { I18nService, LocalizePipe, TranslatePipe } from '@dynamic-cube/shared';
import {
  Matrix,
  MatrixInput,
  SOURCE_ICONS,
  bankQuestionCode,
  bankWeightsChanged,
  cellInputs,
} from './matrix-model';
import { MatrixReferences } from './matrix-references';

export type LinkState = 'ok' | 'warn' | 'error';

/** One input's link to where its value comes from, as checked against the services and workflows. */
export interface InputLink {
  input: MatrixInput;
  state: LinkState;
  /** Translation key of the finding, and what it is about. */
  message: string;
  detail: string;
  usedIn: number;
}

/** Checks where an input's value comes from against what is configured (questions, task fields, integration steps…). */
export function checkInput(
  i: MatrixInput,
  refs: MatrixReferences,
  m?: Matrix,
): Omit<InputLink, 'input' | 'usedIn'> {
  const s = i.source;
  // Points or grades given to answers the bank question does not have (any more).
  const bank = refs.bankQuestion?.(bankQuestionCode(i));
  if (m && bank?.answers?.length) {
    const known = new Set(bank.answers.map((a) => a.value.toLowerCase()));
    const stale = [
      ...new Set(
        m.cells
          .flatMap((c) => c.groups.flatMap((g) => g.criteria))
          .filter((c) => c.input === i.code && c.scoring === 'options')
          .flatMap((c) => c.options.map((o) => o.value))
          .filter((v) => !known.has(v.toLowerCase())),
      ),
    ];
    if (stale.length)
      return { state: 'warn', message: 'mx.link.staleAnswers', detail: stale.join(', ') };
    const changed = m.cells
      .concat((m.shared ?? []).map((x) => x.cell))
      .flatMap((c) => c.groups.flatMap((g) => g.criteria))
      .filter((c) => c.input === i.code && bankWeightsChanged(c, bank.answers));
    if (changed.length)
      return {
        state: 'warn',
        message: 'mx.link.bankWeightsChanged',
        detail: changed.map((c) => c.code).join(', '),
      };
  }
  switch (s.kind) {
    case 'applicant': {
      const code = s.question ?? i.code;
      if (!refs.question(code))
        return { state: 'error', message: 'mx.link.questionMissing', detail: code };
      const services = refs.questionServices().get(code) ?? [];
      return services.length
        ? { state: 'ok', message: 'mx.link.askedIn', detail: services.join(', ') }
        : { state: 'warn', message: 'mx.link.notAsked', detail: code };
    }
    case 'reviewer': {
      const field = s.field ?? i.code;
      const tasks = refs
        .tasks()
        .filter((t) => !s.step || (t.step === s.step && (!s.path || t.workflow === s.path)));
      if (s.step && !tasks.length)
        return { state: 'error', message: 'mx.link.taskMissing', detail: s.step };
      const holders = tasks.filter((t) => t.fields.some((f) => f.code === field));
      return holders.length
        ? {
            state: 'ok',
            message: 'mx.link.filledOn',
            detail: holders.map((t) => `${t.workflow} › ${t.step}`).join(', '),
          }
        : {
            state: 'error',
            message: 'mx.link.fieldMissing',
            detail: `${s.step ?? '*'} › ${field}`,
          };
    }
    case 'integration': {
      if (!s.operation) return { state: 'error', message: 'mx.link.noOperation', detail: '' };
      if (!refs.operation(s.operation))
        return { state: 'error', message: 'mx.link.operationMissing', detail: s.operation };
      const steps = refs
        .integrationSteps()
        .filter(
          (c) =>
            c.operation.toLowerCase() === s.operation!.toLowerCase() &&
            (!s.step || c.result === s.step),
        );
      const forms = s.question
        ? (refs.questionServices().get(s.question) ?? [])
        : (refs.formOperations().get(s.operation) ?? []);
      const callers = [
        ...steps.map((c) => `${c.workflow} › ${c.step}`),
        ...forms.map((f) => `${f} (form)`),
      ];
      return callers.length
        ? { state: 'ok', message: 'mx.link.calledBy', detail: callers.join(', ') }
        : { state: 'error', message: 'mx.link.notCalled', detail: s.operation };
    }
    case 'party':
      return refs.partyFacts().some((f) => f.code === s.path)
        ? { state: 'ok', message: 'mx.link.partyFact', detail: s.path ?? '' }
        : { state: 'warn', message: 'mx.link.partyUnknown', detail: s.path ?? '—' };
    case 'derived':
    case 'projects':
      return { state: 'ok', message: 'mx.link.computed', detail: '' };
    default:
      return { state: 'warn', message: 'mx.link.given', detail: s.path ?? i.code };
  }
}

/**
 * Where the matrix meets the rest of the system: the workflow decision steps that evaluate it (they should pass the
 * request as it is), the input that lists the sectors applied for, and every input's link to its question, task field,
 * integration step or party fact, with what is missing and a button to fix it.
 */
@Component({
  selector: 'dc-matrix-connections',
  imports: [
    FormsModule,
    LocalizePipe,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    RouterLink,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="mx-cn__block">
      <h3>{{ 'mx.link.usedBy' | dcTranslate }}</h3>
      @for (d of steps(); track d.workflow + '/' + d.step) {
        <p
          class="mx-cn__line"
          [class]="'mx-cn__line mx-cn__line--' + (d.mapsInput ? 'warn' : 'ok')"
          data-testid="mx-link-step"
        >
          <mat-icon fontSet="material-symbols-outlined">{{
            d.mapsInput ? 'warning' : 'check_circle'
          }}</mat-icon>
          <span>
            <strong>{{ d.workflowName | dcLocalize }} › {{ d.stepName | dcLocalize }}</strong>
            — {{ (d.mapsInput ? 'mx.link.mapsInput' : 'mx.link.passesRequest') | dcTranslate }}
          </span>
          <a
            matButton
            routerLink="/workflows/designer"
            [queryParams]="{ workflowCode: d.workflow }"
            target="_blank"
            >{{ 'mx.source.openDesigner' | dcTranslate }}</a
          >
        </p>
      } @empty {
        <p class="mx-cn__line mx-cn__line--warn" data-testid="mx-link-no-step">
          <mat-icon fontSet="material-symbols-outlined">info</mat-icon>
          <span>{{ (ruleSet() ? 'mx.link.noStep' : 'mx.link.pickRuleSet') | dcTranslate }}</span>
        </p>
      }
      <p class="dc-muted">{{ 'mx.link.howTo' | dcTranslate }}</p>
    </section>

    <section class="mx-cn__block">
      <h3>{{ 'mx.grid.rowsInput' | dcTranslate }}</h3>
      @if (matrix().rowsInput; as code) {
        <p class="mx-cn__line mx-cn__line--ok">
          <mat-icon fontSet="material-symbols-outlined">check_circle</mat-icon>
          <span dir="auto">{{ code }} → {{ answers() }}</span>
        </p>
      } @else {
        <p class="mx-cn__line mx-cn__line--warn">
          <mat-icon fontSet="material-symbols-outlined">warning</mat-icon>
          <span>{{ 'mx.link.noRowsInput' | dcTranslate }}</span>
        </p>
      }
    </section>

    <section class="mx-cn__block">
      <div class="mx-cn__head">
        <h3>{{ 'mx.link.inputs' | dcTranslate }}</h3>
        <span class="mx-cn__count mx-cn__count--ok">{{ counts().ok }} ✓</span>
        <span class="mx-cn__count mx-cn__count--warn">{{ counts().warn }} !</span>
        <span class="mx-cn__count mx-cn__count--error" data-testid="mx-link-errors"
          >{{ counts().error }} ✗</span
        >
        <mat-checkbox [ngModel]="problemsOnly()" (ngModelChange)="problemsOnly.set($event)">{{
          'mx.link.problemsOnly' | dcTranslate
        }}</mat-checkbox>
      </div>
      <table class="dc-table mx-cn__table">
        <tbody>
          @for (l of shown(); track l.input.code) {
            <tr [attr.data-testid]="'mx-link-' + l.input.code">
              <td class="mx-cn__state" [class]="'mx-cn__state mx-cn__state--' + l.state">
                <mat-icon fontSet="material-symbols-outlined">{{
                  l.state === 'ok' ? 'check_circle' : l.state === 'warn' ? 'warning' : 'error'
                }}</mat-icon>
              </td>
              <td>
                <mat-icon fontSet="material-symbols-outlined" class="mx-cn__src">{{
                  icons[l.input.source.kind]
                }}</mat-icon>
                {{ { ar: l.input.nameAr, en: l.input.nameEn } | dcLocalize }}
                <span class="dc-muted" dir="ltr">{{ l.input.code }}</span>
              </td>
              <td>
                {{ l.message | dcTranslate }}
                <span dir="ltr" class="mx-cn__detail">{{ l.detail }}</span>
              </td>
              <td class="dc-muted">{{ l.usedIn }} {{ 'mx.link.cells' | dcTranslate }}</td>
              <td>
                @if (editable() && l.state !== 'ok') {
                  <button
                    matButton
                    type="button"
                    (click)="fix.emit(l.input)"
                    [attr.data-testid]="'mx-link-fix-' + l.input.code"
                  >
                    <mat-icon fontSet="material-symbols-outlined">build</mat-icon>
                    {{ 'mx.link.fix' | dcTranslate }}
                  </button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </section>
  `,
  styles: `
    .mx-cn__block {
      margin-bottom: 1.25rem;
    }
    .mx-cn__block h3 {
      margin: 0 0 0.5rem;
      font-size: 16px;
    }
    .mx-cn__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 0.5rem;
    }
    .mx-cn__head h3 {
      margin: 0;
    }
    .mx-cn__line {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0.25rem 0;
      padding: 0.4rem 0.75rem;
      border-radius: 8px;
    }
    .mx-cn__line span {
      flex: 1;
    }
    .mx-cn__line--ok {
      background: #ecfdf3;
      color: #05603a;
    }
    .mx-cn__line--warn {
      background: #fffaeb;
      color: #93370d;
    }
    .mx-cn__count {
      font-size: 13px;
      padding: 1px 8px;
      border-radius: 10px;
    }
    .mx-cn__count--ok,
    .mx-cn__state--ok {
      color: #067647;
    }
    .mx-cn__count--warn,
    .mx-cn__state--warn {
      color: #b54708;
    }
    .mx-cn__count--error,
    .mx-cn__state--error {
      color: #d92d20;
    }
    .mx-cn__count--ok {
      background: #ecfdf3;
    }
    .mx-cn__count--warn {
      background: #fffaeb;
    }
    .mx-cn__count--error {
      background: #fef3f2;
    }
    .mx-cn__table td {
      vertical-align: middle;
      font-size: 13px;
    }
    .mx-cn__state {
      width: 2rem;
    }
    .mx-cn__src {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: middle;
      color: var(--dga-text-3, #6c737f);
    }
    .mx-cn__detail {
      font-family: ui-monospace, monospace;
      font-size: 12px;
    }
  `,
})
export class MatrixConnections {
  private readonly i18n = inject(I18nService);
  readonly matrix = input.required<Matrix>();
  readonly references = input.required<MatrixReferences>();
  readonly ruleSet = input<string | null>(null);
  readonly editable = input(true);
  readonly fix = output<MatrixInput>();
  protected readonly icons = SOURCE_ICONS;
  protected readonly problemsOnly = signal(false);

  protected readonly steps = computed(() => {
    const code = this.ruleSet();
    return code
      ? this.references()
          .decisionSteps()
          .filter((d) => d.ruleSet === code)
      : [];
  });

  protected readonly answers = computed(() =>
    this.matrix()
      .rows.map(
        (r) =>
          `${r.answer || r.code}=${this.i18n.language() === 'ar' ? r.nameAr || r.code : r.nameEn || r.code}`,
      )
      .join(' · '),
  );

  protected readonly links = computed<InputLink[]>(() => {
    const m = this.matrix();
    const refs = this.references();
    const usage = new Map<string, number>();
    for (const cell of m.cells)
      for (const i of cellInputs(m, cell)) usage.set(i.code, (usage.get(i.code) ?? 0) + 1);
    const order: Record<LinkState, number> = { error: 0, warn: 1, ok: 2 };
    return m.inputs
      .map((i) => ({ input: i, usedIn: usage.get(i.code) ?? 0, ...checkInput(i, refs, m) }))
      .sort((a, b) => order[a.state] - order[b.state]);
  });

  protected readonly counts = computed(() => {
    const c = { ok: 0, warn: 0, error: 0 };
    for (const l of this.links()) c[l.state]++;
    return c;
  });

  protected readonly shown = computed(() =>
    this.problemsOnly() ? this.links().filter((l) => l.state !== 'ok') : this.links(),
  );
}

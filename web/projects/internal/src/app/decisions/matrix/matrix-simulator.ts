import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  DecisionsApi,
  LocalizePipe,
  TraceStep,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';
import { AnswersOf, Matrix, MatrixInput, SOURCE_ICONS, cellInputs, varsOf } from './matrix-model';

/** A criterion, group, track and sector as the engine reports them. */
export interface SimCriterion {
  code: string;
  nameAr: string;
  nameEn: string;
  input: string;
  value: unknown;
  applicable: boolean;
  maxPoints?: number | null;
  points?: number | null;
  grade?: number | null;
  pending?: boolean;
  bandLabel?: string | null;
  note?: string | null;
}
export interface SimGroup {
  code: string;
  nameAr: string;
  nameEn: string;
  points: number;
  maxPoints: number | null;
  mandatory: boolean;
  minimumMet: boolean;
  criteria: SimCriterion[];
}
export interface SimColumn {
  code: string;
  nameAr: string;
  nameEn: string;
  kind: string;
  includeInFinal: boolean;
  score: number | null;
  maxScore: number | null;
  grade: number | null;
  pending: boolean;
  minimumMet: boolean;
  sameAs: string | null;
  note: string | null;
  details: unknown;
  groups: SimGroup[];
}
export interface SimRow {
  code: string;
  nameAr: string;
  nameEn: string;
  eligible: boolean;
  size: string | null;
  gates: {
    code: string;
    passed: boolean;
    blocking: boolean;
    messageAr: string;
    messageEn: string;
  }[];
  columns: SimColumn[];
  provisionalGrade: number | null;
  baseGrade: number | null;
  finalGrade: number | null;
  expectedGrade: number | null;
  adjustments: { code: string; nameAr: string; nameEn: string; applied: boolean; demote: number }[];
  limiting: string[];
  /** Inputs still waiting for a value (a reviewer's, usually). */
  pending: string[];
}

/** The inputs a person types in the simulator: everything not worked out by the matrix itself. */
export function simulatorInputs(m: Matrix, rows?: string[]): MatrixInput[] {
  const typed = m.inputs.filter((i) => i.source.kind !== 'derived' && i.source.kind !== 'projects');
  if (!rows) return typed;
  // What the chosen sectors read: their cells' inputs, what those are worked out from, the size and the projects.
  const wanted = new Set<string>();
  const add = (code: string) => {
    if (wanted.has(code)) return;
    wanted.add(code);
    const input = m.inputs.find((i) => i.code === code);
    for (const v of varsOf(input?.source.rule)) add(v);
    for (const v of varsOf(input?.source.where)) add(v);
  };
  for (const cell of m.cells.filter((c) =>
    rows.some((r) => r.toLowerCase() === c.row.toLowerCase()),
  )) {
    for (const i of cellInputs(m, cell)) add(i.code);
  }
  for (const g of m.gates.filter((x) => !x.rows.length || x.rows.some((r) => rows.includes(r))))
    for (const v of varsOf(g.rule)) add(v);
  for (const a of m.adjustments.filter(
    (x) => !x.rows.length || x.rows.some((r) => rows.includes(r)),
  ))
    for (const v of varsOf(a.rule)) add(v);
  if (m.sizes?.input) add(m.sizes.input);
  if (m.projects?.input && m.inputs.some((i) => wanted.has(i.code) && i.source.kind === 'projects'))
    add(m.projects.input);
  return typed.filter((i) => wanted.has(i.code));
}

/**
 * Tries the matrix being designed on sample values (nothing saved): values typed per input, the projects as rows, the
 * sectors to grade and the day to grade on. Each sector shows its grades from provisional to final, the tracks that
 * hold it back and every criterion's points.
 */
@Component({
  selector: 'dc-matrix-simulator',
  imports: [
    FormsModule,
    JsonPipe,
    LocalizePipe,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-sim">
      <section class="mx-sim__form">
        <div class="mx-sim__bar">
          <mat-form-field subscriptSizing="dynamic" class="mx-sim__rows">
            <mat-label>{{ 'mx.sim.rows' | dcTranslate }}</mat-label>
            <mat-select
              multiple
              [ngModel]="rows()"
              (ngModelChange)="rows.set($event)"
              data-testid="mx-sim-rows"
            >
              @for (r of matrix().rows; track r.code) {
                <mat-option [value]="r.code">{{
                  { ar: r.nameAr, en: r.nameEn } | dcLocalize
                }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-checkbox
            [ngModel]="showAll()"
            (ngModelChange)="showAll.set($event)"
            data-testid="mx-sim-all"
            >{{ 'mx.sim.showAll' | dcTranslate }} ({{ inputs().length }} /
            {{ allInputs().length }})</mat-checkbox
          >
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>{{ 'mx.sim.asOf' | dcTranslate }}</mat-label>
            <input
              matInput
              type="date"
              dir="ltr"
              [ngModel]="asOf()"
              (ngModelChange)="asOf.set($event)"
            />
          </mat-form-field>
        </div>
        <div class="mx-sim__inputs">
          @for (i of inputs(); track i.code) {
            <div class="mx-sim__input">
              <mat-icon
                fontSet="material-symbols-outlined"
                class="mx-sim__src"
                [matTooltip]="'mx.source.' + i.source.kind | dcTranslate"
                >{{ icons[i.source.kind] }}</mat-icon
              >
              @switch (i.type) {
                @case ('boolean') {
                  <mat-checkbox
                    [ngModel]="values()[i.code] === true"
                    (ngModelChange)="set(i.code, $event)"
                    >{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }}</mat-checkbox
                  >
                }
                @case ('option') {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label>{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }}</mat-label>
                    <mat-select
                      [ngModel]="values()[i.code] ?? null"
                      (ngModelChange)="set(i.code, $event)"
                      [attr.data-testid]="'mx-sim-' + i.code"
                    >
                      <mat-option [value]="null">—</mat-option>
                      @for (o of options(i); track o.code) {
                        <mat-option [value]="o.code">{{
                          { ar: o.labelAr, en: o.labelEn } | dcLocalize
                        }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>
                }
                @case ('list') {
                  @if (i.code === projectsInput()) {
                    <mat-form-field subscriptSizing="dynamic" class="mx-sim__wide">
                      <mat-label
                        >{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }} (JSON)</mat-label
                      >
                      <textarea
                        matInput
                        dir="ltr"
                        rows="5"
                        spellcheck="false"
                        [ngModel]="projectsText()"
                        (ngModelChange)="setProjects($event)"
                        data-testid="mx-sim-projects"
                      ></textarea>
                    </mat-form-field>
                  } @else {
                    <mat-form-field subscriptSizing="dynamic">
                      <mat-label>{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize }}</mat-label>
                      <mat-select
                        multiple
                        [ngModel]="values()[i.code] ?? []"
                        (ngModelChange)="set(i.code, $event)"
                      >
                        @for (o of options(i); track o.code) {
                          <mat-option [value]="o.code">{{
                            { ar: o.labelAr, en: o.labelEn } | dcLocalize
                          }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  }
                }
                @default {
                  <mat-form-field subscriptSizing="dynamic">
                    <mat-label
                      >{{ { ar: i.nameAr, en: i.nameEn } | dcLocalize
                      }}{{ i.unit ? ' (' + i.unit + ')' : '' }}</mat-label
                    >
                    <input
                      matInput
                      [type]="i.type === 'number' ? 'number' : i.type === 'date' ? 'date' : 'text'"
                      dir="ltr"
                      [ngModel]="values()[i.code] ?? null"
                      (ngModelChange)="
                        set(i.code, i.type === 'number' ? num($event) : $event || null)
                      "
                      [attr.data-testid]="'mx-sim-' + i.code"
                    />
                  </mat-form-field>
                }
              }
            </div>
          }
        </div>
        <mat-expansion-panel class="mx-sim__json">
          <mat-expansion-panel-header>{{
            'mx.sim.asJson' | dcTranslate
          }}</mat-expansion-panel-header>
          <textarea
            class="mx-sim__text"
            dir="ltr"
            rows="8"
            spellcheck="false"
            [ngModel]="jsonText()"
            (ngModelChange)="setJson($event)"
            data-testid="mx-sim-json"
          ></textarea>
        </mat-expansion-panel>
        <div class="mx-sim__bar">
          <button
            matButton="filled"
            type="button"
            (click)="run()"
            [disabled]="running()"
            data-testid="mx-sim-run"
          >
            <mat-icon fontSet="material-symbols-outlined">play_arrow</mat-icon>
            {{ 'mx.sim.run' | dcTranslate }}
          </button>
          <button matButton type="button" (click)="clear()">
            {{ 'mx.sim.clear' | dcTranslate }}
          </button>
        </div>
      </section>

      <section class="mx-sim__result" aria-live="polite">
        @if (errors().length) {
          <ul class="dc-errors" role="alert">
            @for (e of errors(); track $index) {
              <li dir="ltr">{{ e }}</li>
            }
          </ul>
        }
        @if (size(); as s) {
          <p class="dc-muted">
            {{ 'mx.sim.size' | dcTranslate }}: <strong>{{ s }}</strong>
          </p>
        }
        @for (r of result(); track r.code) {
          <article class="mx-res" [attr.data-testid]="'mx-sim-row-' + r.code">
            <header class="mx-res__head">
              <h3>{{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }}</h3>
              @if (!r.eligible) {
                <span class="dc-badge dc-badge--danger">{{
                  'mx.sim.notEligible' | dcTranslate
                }}</span>
              } @else if (r.pending.length) {
                <span class="dc-badge" [matTooltip]="r.pending.join(', ')"
                  >{{ 'mx.sim.pending' | dcTranslate }} ({{ r.pending.length }})</span
                >
              }
            </header>
            <ol class="mx-ladder" [attr.aria-label]="'mx.sim.grades' | dcTranslate">
              @for (step of ladder(r); track step.key) {
                <li
                  class="mx-ladder__step"
                  [class.mx-ladder__step--final]="step.key === 'finalGrade'"
                >
                  <span class="mx-ladder__label">{{ 'mx.sim.' + step.key | dcTranslate }}</span>
                  <span
                    class="mx-ladder__value"
                    [attr.data-testid]="'mx-sim-' + r.code + '-' + step.key"
                    >{{ grade(step.value) }}</span
                  >
                </li>
              }
            </ol>
            @for (g of r.gates; track g.code) {
              @if (!g.passed) {
                <p class="mx-res__gate" [class.mx-res__gate--blocking]="g.blocking">
                  <mat-icon fontSet="material-symbols-outlined">{{
                    g.blocking ? 'block' : 'warning'
                  }}</mat-icon>
                  {{ { ar: g.messageAr, en: g.messageEn } | dcLocalize }}
                </p>
              }
            }
            @for (a of r.adjustments; track a.code) {
              @if (a.applied) {
                <p class="dc-muted">
                  {{ 'mx.sim.adjusted' | dcTranslate }}:
                  {{ { ar: a.nameAr, en: a.nameEn } | dcLocalize }} (−{{ a.demote }})
                </p>
              }
            }
            <mat-accordion multi displayMode="flat">
              @for (c of r.columns; track c.code) {
                <mat-expansion-panel [class.mx-res__limiting]="r.limiting.includes(c.code)">
                  <mat-expansion-panel-header>
                    <mat-panel-title>
                      {{ { ar: c.nameAr, en: c.nameEn } | dcLocalize }}
                      @if (r.limiting.includes(c.code)) {
                        <mat-icon
                          fontSet="material-symbols-outlined"
                          class="mx-res__flag"
                          [matTooltip]="'mx.sim.limiting' | dcTranslate"
                          >south</mat-icon
                        >
                      }
                    </mat-panel-title>
                    <mat-panel-description>
                      @if (c.pending) {
                        {{ 'mx.sim.pending' | dcTranslate }}
                      } @else if (c.kind === 'empty') {
                        —
                      } @else {
                        {{ 'mx.sim.grade' | dcTranslate }} {{ grade(c.grade) }}
                        @if (c.score !== null && c.score !== undefined) {
                          · {{ c.score }}{{ c.maxScore ? ' / ' + c.maxScore : '' }}
                        }
                        @if (!c.minimumMet) {
                          · {{ 'mx.sim.minimumNotMet' | dcTranslate }}
                        }
                      }
                    </mat-panel-description>
                  </mat-expansion-panel-header>
                  @for (g of c.groups; track g.code) {
                    <h4>
                      {{ { ar: g.nameAr, en: g.nameEn } | dcLocalize }}
                      <span class="dc-muted"
                        >{{ g.points }}{{ g.maxPoints ? ' / ' + g.maxPoints : '' }}</span
                      >
                      @if (g.mandatory && !g.minimumMet) {
                        <span class="dc-badge dc-badge--danger">{{
                          'mx.sim.minimumNotMet' | dcTranslate
                        }}</span>
                      }
                    </h4>
                    <table class="mx-res__criteria">
                      @for (k of g.criteria; track k.code) {
                        <tr [class.mx-res__na]="!k.applicable">
                          <td>{{ { ar: k.nameAr, en: k.nameEn } | dcLocalize }}</td>
                          <td dir="ltr" class="mx-code">{{ show(k.value) }}</td>
                          <td>{{ k.bandLabel ?? '' }}</td>
                          <td class="mx-res__pts">
                            @if (k.pending) {
                              {{ 'mx.sim.pending' | dcTranslate }}
                            } @else if (!k.applicable) {
                              {{ 'mx.sim.notApplicable' | dcTranslate }}
                            } @else if (k.grade !== undefined && k.grade !== null) {
                              {{ 'mx.sim.grade' | dcTranslate }} {{ k.grade }}
                            } @else {
                              {{ k.points ?? 0 }}{{ k.maxPoints ? ' / ' + k.maxPoints : '' }}
                            }
                          </td>
                        </tr>
                      }
                    </table>
                  }
                  @if (c.details) {
                    <pre class="dc-json" dir="ltr">{{ c.details | json }}</pre>
                  }
                  @if (c.note) {
                    <p class="dc-muted">{{ c.note }}</p>
                  }
                </mat-expansion-panel>
              }
            </mat-accordion>
          </article>
        } @empty {
          @if (!errors().length) {
            <p class="dc-empty">{{ 'mx.sim.empty' | dcTranslate }}</p>
          }
        }
        @if (trace().length) {
          <mat-expansion-panel>
            <mat-expansion-panel-header>{{
              'decisions.trace' | dcTranslate
            }}</mat-expansion-panel-header>
            <pre class="dc-json" dir="ltr">{{ trace() | json }}</pre>
          </mat-expansion-panel>
        }
      </section>
    </div>
  `,
  styles: `
    .mx-sim {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(0, 3fr);
      gap: 1.5rem;
      align-items: start;
    }
    @media (max-width: 1100px) {
      .mx-sim {
        grid-template-columns: 1fr;
      }
    }
    .mx-sim__bar {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
      margin-block: 0.5rem;
    }
    .mx-sim__rows {
      flex: 1 1 16rem;
    }
    .mx-sim__result {
      position: sticky;
      top: 0.5rem;
      max-height: calc(100vh - 6rem);
      overflow: auto;
    }
    @media (max-width: 1100px) {
      .mx-sim__result {
        position: static;
        max-height: none;
      }
    }
    .mx-sim__inputs {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
      gap: 0.5rem 0.75rem;
    }
    .mx-sim__input {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .mx-sim__input mat-form-field {
      flex: 1;
    }
    .mx-sim__wide {
      grid-column: 1 / -1;
    }
    .mx-sim__input:has(.mx-sim__wide) {
      grid-column: 1 / -1;
    }
    .mx-sim__src {
      flex: none;
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--dga-text-3, #6c737f);
    }
    .mx-sim__json {
      margin-top: 0.75rem;
    }
    .mx-sim__text {
      width: 100%;
      font-family: ui-monospace, monospace;
      font-size: 12px;
      border: 1px solid var(--dga-gray-300, #d2d6db);
      border-radius: 6px;
      padding: 6px;
    }
    .mx-res {
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      border-radius: 10px;
      padding: 0.75rem 1rem;
      margin-bottom: 1rem;
      background: #fff;
    }
    .mx-res__head {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .mx-res__head h3 {
      margin: 0;
      font-size: 17px;
    }
    .mx-ladder {
      display: flex;
      flex-wrap: wrap;
      gap: 0;
      list-style: none;
      padding: 0;
      margin: 0.75rem 0;
    }
    .mx-ladder__step {
      display: grid;
      justify-items: center;
      padding: 6px 14px;
      border: 1px solid var(--dga-gray-200, #e5e7eb);
      margin-inline-end: -1px;
      min-width: 6rem;
    }
    .mx-ladder__step:first-child {
      border-start-start-radius: 8px;
      border-end-start-radius: 8px;
    }
    .mx-ladder__step:last-child {
      border-start-end-radius: 8px;
      border-end-end-radius: 8px;
    }
    .mx-ladder__step--final {
      background: color-mix(in srgb, var(--mat-sys-primary, #1b8354) 12%, transparent);
      border-color: var(--mat-sys-primary, #1b8354);
      z-index: 1;
    }
    .mx-ladder__label {
      font-size: 11px;
      color: var(--dga-text-3, #6c737f);
    }
    .mx-ladder__value {
      font-size: 20px;
      font-weight: 700;
    }
    .mx-res__gate {
      display: flex;
      align-items: center;
      gap: 6px;
      color: var(--dga-warning-700, #b54708);
      margin: 0.25rem 0;
    }
    .mx-res__gate--blocking {
      color: var(--dga-error-600, #d92d20);
    }
    .mx-res__limiting {
      border-inline-start: 3px solid var(--dga-warning-500, #f79009);
    }
    .mx-res__flag {
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--dga-warning-600, #dc6803);
      margin-inline-start: 4px;
    }
    h4 {
      margin: 0.5rem 0 0.25rem;
      font-size: 14px;
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .mx-res__criteria {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    .mx-res__criteria td {
      padding: 3px 4px;
      border-top: 1px solid var(--dga-gray-100, #f3f4f6);
    }
    .mx-res__pts {
      text-align: end;
      white-space: nowrap;
      font-weight: 600;
    }
    .mx-res__na {
      color: var(--dga-text-3, #6c737f);
    }
    .mx-code {
      font-family: ui-monospace, monospace;
    }
    .dc-json {
      font-family: ui-monospace, monospace;
      font-size: 12px;
      padding: 0.5rem;
      background: var(--dga-gray-50, #f9fafb);
      border-radius: 6px;
      overflow: auto;
      max-height: 18rem;
    }
  `,
})
export class MatrixSimulator {
  private readonly api = inject(DecisionsApi);
  readonly matrix = input.required<Matrix>();
  /** The bank question answers of an input, when the page knows the question bank. */
  readonly answersOf = input<AnswersOf | null>(null);
  protected readonly icons = SOURCE_ICONS;

  protected readonly values = signal<Record<string, unknown>>({});
  protected readonly rows = signal<string[]>([]);
  protected readonly asOf = signal<string>(new Date().toISOString().slice(0, 10));
  protected readonly running = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly result = signal<SimRow[]>([]);
  protected readonly size = signal<string | null>(null);
  protected readonly trace = signal<TraceStep[]>([]);
  protected readonly jsonText = computed(() => JSON.stringify(this.values(), null, 2));

  protected readonly showAll = signal(false);
  protected readonly allInputs = computed(() => simulatorInputs(this.matrix()));
  /** The inputs of the chosen sectors (the active ones when none is chosen), unless all are asked for. */
  protected readonly inputs = computed(() =>
    this.showAll()
      ? this.allInputs()
      : simulatorInputs(
          this.matrix(),
          this.rows().length
            ? this.rows()
            : this.matrix()
                .rows.filter((r) => r.active)
                .map((r) => r.code),
        ),
  );
  protected readonly projectsInput = computed(() => this.matrix().projects?.input ?? null);
  protected readonly projectsText = computed(() =>
    JSON.stringify(this.values()[this.projectsInput() ?? ''] ?? [], null, 2),
  );

  /** An input's answers: its bank question's, else its answer list's. */
  protected options(i: MatrixInput): { code: string; labelAr: string; labelEn: string }[] {
    const bank = this.answersOf()?.(i);
    if (bank?.length)
      return bank.map((a) => ({ code: a.value, labelAr: a.labelAr, labelEn: a.labelEn }));
    return this.matrix().optionSets.find((s) => s.code === i.optionSet)?.items ?? [];
  }

  protected num(value: unknown): number | null {
    return value === '' || value === null || value === undefined ? null : Number(value);
  }

  protected set(code: string, value: unknown): void {
    this.values.update((v) => {
      const next = { ...v };
      if (value === null || value === undefined || (Array.isArray(value) && !value.length))
        delete next[code];
      else next[code] = value;
      return next;
    });
  }

  protected setProjects(text: string): void {
    try {
      const list = JSON.parse(text) as unknown;
      if (Array.isArray(list)) this.set(this.projectsInput()!, list);
    } catch {
      // Kept as typed until it is valid JSON.
    }
  }

  protected setJson(text: string): void {
    try {
      const parsed = JSON.parse(text) as unknown;
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        const p = parsed as Record<string, unknown>;
        // A whole engine input ({values, rows, asOf}) or the values alone.
        if (p['values'] && typeof p['values'] === 'object') {
          this.values.set(p['values'] as Record<string, unknown>);
          if (Array.isArray(p['rows'])) this.rows.set(p['rows'] as string[]);
          if (typeof p['asOf'] === 'string') this.asOf.set(p['asOf'].slice(0, 10));
        } else {
          this.values.set(p);
        }
      }
    } catch {
      // Kept as typed until it is valid JSON.
    }
  }

  protected clear(): void {
    this.values.set({});
    this.result.set([]);
    this.errors.set([]);
    this.trace.set([]);
    this.size.set(null);
  }

  protected ladder(r: SimRow) {
    return [
      { key: 'provisionalGrade', value: r.provisionalGrade },
      { key: 'baseGrade', value: r.baseGrade },
      { key: 'finalGrade', value: r.finalGrade },
      { key: 'expectedGrade', value: r.expectedGrade },
    ];
  }

  protected grade(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    const g = this.matrix().grades.find((x) => x.rank === value);
    return g?.code && g.code !== `G${value}` ? `${value} (${g.code})` : String(value);
  }

  protected show(value: unknown): string {
    if (value === null || value === undefined) return '—';
    return typeof value === 'object' ? JSON.stringify(value).slice(0, 60) : String(value);
  }

  async run(): Promise<void> {
    this.errors.set([]);
    this.running.set(true);
    const input: Record<string, unknown> = { values: this.values(), asOf: this.asOf() };
    if (this.rows().length) input['rows'] = this.rows();
    try {
      const r = await firstValueFrom(this.api.simulateMatrix(this.matrix(), input));
      this.errors.set(
        Object.entries(r.errors ?? {}).map(([where, message]) => `${where}: ${message}`),
      );
      this.result.set(
        ((r.output?.['rows'] as SimRow[] | undefined) ?? []).map((x) => ({
          ...x,
          limiting: x.limiting ?? [],
          gates: x.gates ?? [],
          adjustments: x.adjustments ?? [],
          pending: x.pending ?? [],
        })),
      );
      this.size.set((r.output?.['size'] as string | null | undefined) ?? null);
      this.trace.set(r.trace ?? []);
    } catch (error) {
      this.result.set([]);
      this.errors.set(problemMessages(error));
    } finally {
      this.running.set(false);
    }
  }
}

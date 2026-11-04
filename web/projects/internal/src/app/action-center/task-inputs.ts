import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  I18nService,
  LocalizePipe,
  RecalculatedDecision,
  TaskField,
  TranslatePipe,
  WorkflowApi,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

/** Result keys that are bookkeeping, not results. */
const HIDDEN = new Set(['evaluationId', 'version']);

/** One line of a decision worked out again: a result before and with the employee's inputs. */
interface ResultLine {
  key: string;
  before: string;
  after: string;
  changed: boolean;
}

/**
 * What the employee fills in on a task (a score, a reviewed figure, a finding), kept with the request for later rules,
 * decisions and documents. When the task works decisions out again, their results are shown live as the fields change.
 */
@Component({
  selector: 'dc-task-inputs',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    LocalizePipe,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-card dc-task-inputs" data-testid="task-inputs">
      <header class="dc-task-inputs__head">
        <h2>{{ 'taskInputs.title' | dcTranslate }}</h2>
        @if (editable()) {
          <button
            matButton="outlined"
            type="button"
            (click)="save()"
            [disabled]="saving()"
            data-testid="task-inputs-save"
          >
            <mat-icon fontSet="material-symbols-outlined">save</mat-icon>
            {{ 'taskInputs.save' | dcTranslate }}
          </button>
        }
      </header>
      <div class="dc-task-inputs__grid">
        @for (f of fields(); track f.code) {
          <div
            class="dc-task-inputs__field"
            [class.dc-task-inputs__field--wide]="f.type === 'LONG_TEXT'"
            [attr.data-testid]="'task-input-' + f.code"
          >
            @switch (f.type) {
              @case ('CHECKBOX') {
                <mat-checkbox
                  [ngModel]="values()[f.code] === true"
                  (ngModelChange)="set(f.code, $event)"
                  [disabled]="!editable()"
                >
                  {{ label(f) }}{{ f.required ? ' *' : '' }}
                </mat-checkbox>
                @if (hint(f); as h) {
                  <small class="dc-muted">{{ h }}</small>
                }
              }
              @case ('SELECT') {
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ label(f) }}</mat-label>
                  <mat-select
                    [ngModel]="values()[f.code] ?? null"
                    (ngModelChange)="set(f.code, $event)"
                    [disabled]="!editable()"
                    [required]="!!f.required"
                  >
                    @if (!f.required) {
                      <mat-option [value]="null">—</mat-option>
                    }
                    @for (o of f.options ?? []; track o.value) {
                      <mat-option [value]="o.value">{{
                        { ar: o.labelAr, en: o.labelEn } | dcLocalize
                      }}</mat-option>
                    }
                  </mat-select>
                  @if (hint(f); as h) {
                    <mat-hint>{{ h }}</mat-hint>
                  }
                </mat-form-field>
              }
              @case ('LONG_TEXT') {
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ label(f) }}</mat-label>
                  <textarea
                    matInput
                    rows="3"
                    [ngModel]="values()[f.code] ?? ''"
                    (ngModelChange)="set(f.code, $event)"
                    [disabled]="!editable()"
                    [required]="!!f.required"
                  ></textarea>
                  @if (hint(f); as h) {
                    <mat-hint>{{ h }}</mat-hint>
                  }
                </mat-form-field>
              }
              @default {
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ label(f) }}</mat-label>
                  <input
                    matInput
                    [type]="f.type === 'NUMBER' ? 'number' : f.type === 'DATE' ? 'date' : 'text'"
                    [attr.min]="f.min ?? null"
                    [attr.max]="f.max ?? null"
                    [ngModel]="values()[f.code] ?? ''"
                    (ngModelChange)="set(f.code, $event)"
                    [disabled]="!editable()"
                    [required]="!!f.required"
                    [attr.dir]="f.type === 'TEXT' ? 'auto' : 'ltr'"
                  />
                  @if (hint(f); as h) {
                    <mat-hint>{{ h }}</mat-hint>
                  }
                </mat-form-field>
              }
            }
          </div>
        }
      </div>

      @if (recalculates()) {
        <div class="dc-task-inputs__results" data-testid="task-recalculated" aria-live="polite">
          <h3>
            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">calculate</mat-icon>
            {{ 'taskInputs.results' | dcTranslate }}
          </h3>
          <p class="dc-muted">{{ 'taskInputs.resultsHint' | dcTranslate }}</p>
          @for (r of results(); track r.step) {
            <div class="dc-task-inputs__decision" [attr.data-testid]="'recalculated-' + r.step">
              <strong>{{ { ar: r.nameAr, en: r.nameEn } | dcLocalize }}</strong>
              @if (r.problem) {
                <p class="dc-errors">{{ r.problem }}</p>
              } @else {
                <table class="dc-table">
                  <thead>
                    <tr>
                      <th scope="col">{{ 'taskInputs.result' | dcTranslate }}</th>
                      <th scope="col">{{ 'taskInputs.before' | dcTranslate }}</th>
                      <th scope="col">{{ 'taskInputs.after' | dcTranslate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (line of lines(r); track line.key) {
                      <tr [class.dc-task-inputs__changed]="line.changed">
                        <td class="mono">{{ line.key }}</td>
                        <td dir="auto">{{ line.before }}</td>
                        <td dir="auto">
                          <strong>{{ line.after }}</strong>
                          @if (line.changed) {
                            <mat-icon fontSet="material-symbols-outlined" aria-hidden="true"
                              >change_circle</mat-icon
                            >
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            </div>
          } @empty {
            <p class="dc-muted">{{ 'taskInputs.working' | dcTranslate }}</p>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .dc-task-inputs {
      margin-bottom: 1rem;
    }
    .dc-task-inputs__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 8px;
    }
    .dc-task-inputs__head h2 {
      margin: 0;
      font-size: 16px;
    }
    .dc-task-inputs__grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 12px 16px;
    }
    .dc-task-inputs__field {
      display: grid;
      gap: 4px;
    }
    .dc-task-inputs__field mat-form-field {
      width: 100%;
    }
    .dc-task-inputs__field--wide {
      grid-column: 1 / -1;
    }
    .dc-task-inputs__results {
      margin-top: 16px;
      padding-top: 12px;
      border-top: 1px solid var(--dga-gray-200);
    }
    .dc-task-inputs__results h3 {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 4px;
      font-size: 15px;
    }
    .dc-task-inputs__decision {
      margin-top: 8px;
      overflow-x: auto;
    }
    .dc-task-inputs__decision table {
      width: 100%;
      margin-top: 4px;
    }
    .dc-task-inputs__changed td {
      background: var(--dynamic-cube-gold-50);
    }
    .dc-task-inputs__changed mat-icon {
      width: 16px;
      height: 16px;
      font-size: 16px;
      vertical-align: middle;
      color: var(--dynamic-cube-gold-700);
    }
  `,
})
export class TaskInputsCard {
  readonly taskId = input.required<string>();
  readonly fields = input.required<TaskField[]>();
  readonly initial = input<Record<string, unknown>>({});
  readonly editable = input(false);
  readonly recalculates = input(false);
  /** After a save: the task as it is now. */
  readonly saved = output<void>();

  private readonly api = inject(WorkflowApi);
  private readonly i18n = inject(I18nService);
  protected readonly values = signal<Record<string, unknown>>({});
  protected readonly results = signal<RecalculatedDecision[]>([]);
  protected readonly saving = signal(false);
  private timer: ReturnType<typeof setTimeout> | undefined;
  private asked = 0;

  /** Fields needed to move on that are still empty (a required tick box must be ticked). */
  readonly missing = computed(() =>
    this.fields()
      .filter((f) => f.required)
      .filter((f) => {
        const value = this.values()[f.code];
        return f.type === 'CHECKBOX'
          ? value !== true
          : value === null || value === undefined || value === '';
      })
      .map((f) => this.label(f)),
  );

  constructor() {
    effect(() => {
      const initial = this.initial();
      this.taskId();
      untracked(() => {
        this.values.set({ ...initial });
        this.refresh(0);
      });
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  /** What the fields hold, for the action. */
  value(): Record<string, unknown> {
    const value: Record<string, unknown> = {};
    for (const f of this.fields()) {
      const v = this.values()[f.code];
      if (v === null || v === undefined || v === '') continue;
      value[f.code] = f.type === 'NUMBER' && typeof v === 'string' ? Number(v) : v;
    }
    return value;
  }

  protected set(code: string, value: unknown): void {
    this.values.update((values) => ({ ...values, [code]: value }));
    this.refresh(500);
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.saveInputs(this.taskId(), this.value()));
      this.saved.emit();
    } finally {
      this.saving.set(false);
    }
  }

  protected label(field: TaskField): string {
    // Required fields get their mark from the form field (and the tick box below).
    return this.i18n.language() === 'ar' ? field.labelAr : field.labelEn;
  }

  protected hint(field: TaskField): string | null {
    return (this.i18n.language() === 'ar' ? field.hintAr : field.hintEn) || null;
  }

  protected lines(decision: RecalculatedDecision): ResultLine[] {
    const keys = new Set([
      ...Object.keys(decision.before ?? {}),
      ...Object.keys(decision.after ?? {}),
    ]);
    const top = [...keys]
      .filter((key) => !HIDDEN.has(key))
      // Lists and objects would only show as a count; the result's items are listed one by one below.
      .filter((key) => !isNested(decision.before?.[key]) && !isNested(decision.after?.[key]))
      .map((key) => ({
        key,
        before: shown(decision.before?.[key]),
        after: shown(decision.after?.[key]),
      }));
    return [...top, ...this.itemLines(decision)]
      .filter((line) => line.before !== '' || line.after !== '')
      .map((line) => ({ ...line, changed: line.before !== line.after }));
  }

  /**
   * One line per value of each element of the result's items (e.g. a scoring matrix's sectors with their final,
   * expected and track grades), named after the element.
   */
  private itemLines(decision: RecalculatedDecision): Omit<ResultLine, 'changed'>[] {
    const before = asItems(decision.before?.['items']);
    const after = asItems(decision.after?.['items']);
    const lines: Omit<ResultLine, 'changed'>[] = [];
    const count = Math.max(before.length, after.length);
    for (let i = 0; i < count; i++) {
      const element = after[i] ?? before[i];
      const name = itemName(element, this.i18n.language());
      const fields = new Set([...Object.keys(before[i] ?? {}), ...Object.keys(after[i] ?? {})]);
      for (const field of [...fields].filter((f) => !ITEM_NAMES.has(f))) {
        const was = before[i]?.[field];
        const now = after[i]?.[field];
        if (isNested(was) || isNested(now)) continue;
        lines.push({ key: `${name} · ${field}`, before: shown(was), after: shown(now) });
      }
    }
    return lines;
  }

  /** Works the decisions out again after a pause in typing; a late answer to an older question is dropped. */
  private refresh(delay: number): void {
    if (!this.recalculates()) return;
    clearTimeout(this.timer);
    this.timer = setTimeout(async () => {
      const asked = ++this.asked;
      try {
        const results = await firstValueFrom(this.api.recalculate(this.taskId(), this.value()));
        if (asked === this.asked) this.results.set(results);
      } catch {
        // A value that does not pass yet (e.g. out of range) leaves the last results.
      }
    }, delay);
  }
}

/** The fields that name an element of a result's items (shown in the line's name, not as lines). */
const ITEM_NAMES = new Set([
  'code',
  'nameAr',
  'nameEn',
  'name',
  'sector',
  'sectorNameAr',
  'sectorNameEn',
]);

function isNested(value: unknown): boolean {
  return value !== null && typeof value === 'object';
}

function asItems(value: unknown): Record<string, unknown>[] {
  return Array.isArray(value)
    ? value.filter((v): v is Record<string, unknown> => isNested(v) && !Array.isArray(v))
    : [];
}

function itemName(element: Record<string, unknown> | undefined, language: string): string {
  const pick = (...keys: string[]) =>
    keys.map((k) => element?.[k]).find((v) => typeof v === 'string' && v);
  return String(
    (language === 'ar'
      ? pick('nameAr', 'sectorNameAr', 'name')
      : pick('nameEn', 'sectorNameEn', 'name')) ??
      pick('code', 'sector') ??
      '?',
  );
}

/** A result value as text; lists and objects are summarised, not shown. */
function shown(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (Array.isArray(value)) return `[${value.length}]`;
  if (typeof value === 'object') return '{…}';
  return String(value);
}

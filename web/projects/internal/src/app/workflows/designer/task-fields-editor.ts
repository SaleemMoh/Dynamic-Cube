import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import {
  I18nService,
  JsonLogicRule,
  RuleEditor,
  RuleVariable,
  TaskField,
  TaskFieldOption,
  TaskFieldType,
  TranslatePipe,
  SearchableSelectModule,
} from '@dynamic-cube/shared';

/** A decision step the task can work out again. */
export interface DecisionChoice {
  code: string;
  label: string;
}

/**
 * What an employee fills in on a human task (kept as `inputs.<task>.<field>` for later rules, decisions and documents)
 * and the decision steps worked out again with those inputs.
 */
@Component({
  selector: 'dc-task-fields-editor',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    SearchableSelectModule,
    RuleEditor,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="dc-panel__hint">{{ 'designer.taskFieldsHint' | dcTranslate }}</p>
    <mat-accordion class="dc-task-fields" multi>
      @for (f of fields(); track $index; let i = $index) {
        <mat-expansion-panel [attr.data-testid]="'task-field-panel-' + i">
          <mat-expansion-panel-header>
            <mat-panel-title>
              <span class="mono">{{ f.code || '—' }}</span>
            </mat-panel-title>
            <mat-panel-description>
              {{ label(f) }} · {{ 'designer.taskFieldType.' + f.type | dcTranslate }}
            </mat-panel-description>
          </mat-expansion-panel-header>
          <div class="dc-task-fields__body">
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'designer.field.code' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="f.code"
                (ngModelChange)="patch(i, { code: $event.trim().toUpperCase() })"
                [ngModelOptions]="{ updateOn: 'blur' }"
                data-testid="task-field-code"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'designer.taskFieldType' | dcTranslate }}</mat-label>
              <mat-select
                [ngModel]="f.type"
                (ngModelChange)="type(i, $event)"
                data-testid="task-field-type"
              >
                @for (t of types; track t) {
                  <mat-option [value]="t">{{
                    'designer.taskFieldType.' + t | dcTranslate
                  }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'designer.field.nameAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="f.labelAr"
                (ngModelChange)="patch(i, { labelAr: $event })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'designer.field.nameEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="f.labelEn"
                (ngModelChange)="patch(i, { labelEn: $event })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'designer.taskFieldHintAr' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="rtl"
                [ngModel]="f.hintAr ?? ''"
                (ngModelChange)="patch(i, { hintAr: $event.trim() || null })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-form-field subscriptSizing="dynamic">
              <mat-label>{{ 'designer.taskFieldHintEn' | dcTranslate }}</mat-label>
              <input
                matInput
                dir="ltr"
                [ngModel]="f.hintEn ?? ''"
                (ngModelChange)="patch(i, { hintEn: $event.trim() || null })"
                [ngModelOptions]="{ updateOn: 'blur' }"
              />
            </mat-form-field>
            <mat-checkbox [ngModel]="!!f.required" (ngModelChange)="patch(i, { required: $event })">
              {{ 'designer.taskFieldRequired' | dcTranslate }}
            </mat-checkbox>
            @if (f.type === 'NUMBER') {
              <div class="dc-task-fields__pair">
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'designer.taskFieldMin' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    type="number"
                    dir="ltr"
                    [ngModel]="f.min ?? null"
                    (ngModelChange)="patch(i, { min: number($event) })"
                  />
                </mat-form-field>
                <mat-form-field subscriptSizing="dynamic">
                  <mat-label>{{ 'designer.taskFieldMax' | dcTranslate }}</mat-label>
                  <input
                    matInput
                    type="number"
                    dir="ltr"
                    [ngModel]="f.max ?? null"
                    (ngModelChange)="patch(i, { max: number($event) })"
                  />
                </mat-form-field>
              </div>
            }
            @if (f.type === 'SELECT') {
              <div class="dc-task-fields__options">
                <strong>{{ 'designer.taskFieldOptions' | dcTranslate }}</strong>
                @for (o of f.options ?? []; track $index; let j = $index) {
                  <div class="dc-task-fields__option">
                    <input
                      class="dc-task-fields__cell mono"
                      dir="ltr"
                      [attr.aria-label]="'designer.taskFieldOptionValue' | dcTranslate"
                      [placeholder]="'designer.taskFieldOptionValue' | dcTranslate"
                      [ngModel]="o.value"
                      (ngModelChange)="option(i, j, { value: $event.trim() })"
                      [ngModelOptions]="{ updateOn: 'blur' }"
                    />
                    <input
                      class="dc-task-fields__cell"
                      dir="rtl"
                      [attr.aria-label]="'designer.field.nameAr' | dcTranslate"
                      [placeholder]="'designer.field.nameAr' | dcTranslate"
                      [ngModel]="o.labelAr"
                      (ngModelChange)="option(i, j, { labelAr: $event })"
                      [ngModelOptions]="{ updateOn: 'blur' }"
                    />
                    <input
                      class="dc-task-fields__cell"
                      dir="ltr"
                      [attr.aria-label]="'designer.field.nameEn' | dcTranslate"
                      [placeholder]="'designer.field.nameEn' | dcTranslate"
                      [ngModel]="o.labelEn"
                      (ngModelChange)="option(i, j, { labelEn: $event })"
                      [ngModelOptions]="{ updateOn: 'blur' }"
                    />
                    <button
                      matIconButton
                      type="button"
                      (click)="removeOption(i, j)"
                      [attr.aria-label]="'designer.taskFieldRemoveOption' | dcTranslate"
                    >
                      <mat-icon>close</mat-icon>
                    </button>
                  </div>
                }
                <button matButton type="button" (click)="addOption(i)">
                  <mat-icon>add</mat-icon>
                  {{ 'designer.taskFieldAddOption' | dcTranslate }}
                </button>
              </div>
            }
            <dc-rule-editor
              [label]="'designer.taskFieldInitial' | dcTranslate"
              mode="value"
              [rule]="$any(f.initial ?? null)"
              [variables]="variables()"
              testId="task-field-initial"
              (ruleChange)="patch(i, { initial: $event })"
            />
            <button matButton type="button" class="dc-task-fields__remove" (click)="removeField(i)">
              <mat-icon>delete</mat-icon>
              {{ 'designer.taskFieldRemove' | dcTranslate }}
            </button>
          </div>
        </mat-expansion-panel>
      }
    </mat-accordion>
    <button matButton type="button" (click)="addField()" data-testid="add-task-field">
      <mat-icon>add</mat-icon>
      {{ 'designer.addTaskField' | dcTranslate }}
    </button>
    @if (decisions().length) {
      <mat-form-field subscriptSizing="dynamic" class="dc-task-fields__recalculate">
        <mat-label>{{ 'designer.field.recalculate' | dcTranslate }}</mat-label>
        <mat-select
          multiple
          [ngModel]="recalculate()"
          (ngModelChange)="recalculateChange.emit($event)"
          data-testid="task-recalculate"
        >
          @for (d of decisions(); track d.code) {
            <mat-option [value]="d.code">{{ d.label }}</mat-option>
          }
        </mat-select>
        <mat-hint>{{ 'designer.recalculateHint' | dcTranslate }}</mat-hint>
      </mat-form-field>
    }
  `,
  styles: `
    .dc-task-fields {
      display: block;
      margin-bottom: 8px;
    }
    .dc-task-fields mat-panel-title {
      flex: none;
    }
    .dc-task-fields mat-panel-description {
      display: block;
      min-width: 0;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-task-fields__body {
      display: grid;
      gap: 10px;
    }
    .dc-task-fields__body mat-form-field {
      width: 100%;
    }
    .dc-task-fields__pair {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }
    .dc-task-fields__options {
      display: grid;
      gap: 6px;
    }
    .dc-task-fields__option {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr auto;
      gap: 4px;
      align-items: center;
    }
    .dc-task-fields__cell {
      min-width: 0;
      padding: 6px 8px;
      border: 1px solid var(--dga-gray-300);
      border-radius: 6px;
      font: inherit;
    }
    .dc-task-fields__remove {
      justify-self: start;
      color: var(--mat-sys-error);
    }
    .dc-task-fields__recalculate {
      width: 100%;
      margin-top: 8px;
    }
  `,
})
export class TaskFieldsEditor {
  readonly fields = input.required<TaskField[]>();
  readonly decisions = input<DecisionChoice[]>([]);
  readonly recalculate = input<string[]>([]);
  readonly variables = input<RuleVariable[]>([]);
  readonly fieldsChange = output<TaskField[]>();
  readonly recalculateChange = output<string[]>();

  private readonly i18n = inject(I18nService);
  protected readonly types: TaskFieldType[] = [
    'TEXT',
    'LONG_TEXT',
    'NUMBER',
    'DATE',
    'CHECKBOX',
    'SELECT',
  ];

  protected label(field: TaskField): string {
    return (this.i18n.language() === 'ar' ? field.labelAr : field.labelEn) || '—';
  }

  protected number(value: unknown): number | null {
    return value === null || value === undefined || value === '' ? null : Number(value);
  }

  protected addField(): void {
    const used = new Set(this.fields().map((f) => f.code));
    let n = this.fields().length + 1;
    while (used.has(`FIELD_${n}`)) n++;
    this.fieldsChange.emit([
      ...this.fields(),
      { code: `FIELD_${n}`, labelAr: '', labelEn: '', type: 'TEXT', required: false },
    ]);
  }

  protected removeField(index: number): void {
    this.fieldsChange.emit(this.fields().filter((_, i) => i !== index));
  }

  protected patch(
    index: number,
    change: Partial<TaskField> & { initial?: JsonLogicRule | null },
  ): void {
    this.fieldsChange.emit(
      this.fields().map((f, i) => (i === index ? clean({ ...f, ...change }) : f)),
    );
  }

  /** A new type drops what only the old one had (choices, a least and a most). */
  protected type(index: number, type: TaskFieldType): void {
    const field = this.fields()[index];
    this.fieldsChange.emit(
      this.fields().map((f, i) =>
        i === index
          ? clean({
              ...field,
              type,
              options: type === 'SELECT' ? (field.options ?? []) : undefined,
              min: type === 'NUMBER' ? field.min : undefined,
              max: type === 'NUMBER' ? field.max : undefined,
            })
          : f,
      ),
    );
  }

  protected addOption(index: number): void {
    const options = this.fields()[index].options ?? [];
    this.patch(index, {
      options: [...options, { value: `OPTION_${options.length + 1}`, labelAr: '', labelEn: '' }],
    });
  }

  protected option(index: number, at: number, change: Partial<TaskFieldOption>): void {
    const options = (this.fields()[index].options ?? []).map((o, j) =>
      j === at ? { ...o, ...change } : o,
    );
    this.patch(index, { options });
  }

  protected removeOption(index: number, at: number): void {
    this.patch(index, { options: (this.fields()[index].options ?? []).filter((_, j) => j !== at) });
  }
}

/** Leaves out empty settings, so the saved definition stays small. */
function clean(field: TaskField): TaskField {
  const copy: Record<string, unknown> = { ...field };
  for (const key of Object.keys(copy)) {
    if (copy[key] === undefined || copy[key] === null) delete copy[key];
  }
  return copy as unknown as TaskField;
}

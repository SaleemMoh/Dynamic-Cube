import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import {
  FormOption,
  StructuredColumn,
  StructuredInput,
  StructuredShape,
  TranslatePipe,
  SearchableSelectModule,
} from '@dynamic-cube/shared';
import { Matrix, inputFields, projectFields } from './matrix-model';

const t = (ar: string, en: string) => ({ ar, en });
const NAMES: StructuredColumn[] = [
  { key: 'nameAr', label: t('الاسم بالعربية', 'Arabic name'), type: 'text' },
  { key: 'nameEn', label: t('الاسم بالإنجليزية', 'English name'), type: 'text', ltr: true },
];
const CODE: StructuredColumn = {
  key: 'code',
  label: t('الرمز', 'Code'),
  type: 'text',
  ltr: true,
  required: true,
};
const TYPES: FormOption[] = ['number', 'text', 'option', 'boolean', 'date', 'list'].map((v) => ({
  value: v,
  label: t(v, v),
}));

/**
 * The matrix's shared settings, each in its own table: grades (1 best), point scales (points → grade, from inclusive,
 * to exclusive), sizes (categories of one input), answer lists, gates (must hold for a sector to be graded),
 * adjustments (demote the grade when a condition holds), how projects count, and what happens below the worst grade.
 */
@Component({
  selector: 'dc-matrix-settings',
  imports: [
    FormsModule,
    MatExpansionModule,
    MatFormFieldModule,
    SearchableSelectModule,
    StructuredInput,
    TranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-accordion multi>
      @if (part() === 'grades') {
        <mat-expansion-panel expanded>
          <mat-expansion-panel-header>{{
            'mx.settings.grades' | dcTranslate
          }}</mat-expansion-panel-header>
          <p class="dc-muted">{{ 'mx.settings.gradesHint' | dcTranslate }}</p>
          <dc-structured-input
            [shape]="gradesShape"
            [ngModel]="matrix().grades"
            (ngModelChange)="set({ grades: $event ?? [] })"
            testId="mx-grades"
          />
          <mat-form-field subscriptSizing="dynamic" class="mx-set__below">
            <mat-label>{{ 'mx.settings.belowWorst' | dcTranslate }}</mat-label>
            <mat-select
              [ngModel]="matrix().belowWorst ?? 'stay'"
              (ngModelChange)="set({ belowWorst: $event })"
            >
              <mat-option value="stay">{{
                'mx.settings.belowWorst.stay' | dcTranslate
              }}</mat-option>
              <mat-option value="notGraded">{{
                'mx.settings.belowWorst.notGraded' | dcTranslate
              }}</mat-option>
            </mat-select>
          </mat-form-field>
        </mat-expansion-panel>
        <mat-expansion-panel>
          <mat-expansion-panel-header
            >{{ 'mx.settings.scales' | dcTranslate }} ({{
              matrix().scales.length
            }})</mat-expansion-panel-header
          >
          <p class="dc-muted">{{ 'mx.settings.scalesHint' | dcTranslate }}</p>
          <dc-structured-input
            [shape]="scalesShape"
            [ngModel]="matrix().scales"
            (ngModelChange)="set({ scales: $event ?? [] })"
            testId="mx-scales"
          />
        </mat-expansion-panel>
        <mat-expansion-panel>
          <mat-expansion-panel-header>{{
            'mx.settings.sizes' | dcTranslate
          }}</mat-expansion-panel-header>
          <p class="dc-muted">{{ 'mx.settings.sizesHint' | dcTranslate }}</p>
          <dc-structured-input
            [shape]="sizesShape()"
            [ngModel]="matrix().sizes ?? { input: '', categories: [] }"
            (ngModelChange)="setSizes($event)"
            testId="mx-sizes"
          />
        </mat-expansion-panel>
        <mat-expansion-panel>
          <mat-expansion-panel-header
            >{{ 'mx.settings.optionSets' | dcTranslate }} ({{
              matrix().optionSets.length
            }})</mat-expansion-panel-header
          >
          <dc-structured-input
            [shape]="optionSetsShape"
            [ngModel]="matrix().optionSets"
            (ngModelChange)="set({ optionSets: $event ?? [] })"
            testId="mx-option-sets"
          />
        </mat-expansion-panel>
      } @else {
        <mat-expansion-panel expanded>
          <mat-expansion-panel-header
            >{{ 'mx.settings.gates' | dcTranslate }} ({{
              matrix().gates.length
            }})</mat-expansion-panel-header
          >
          <p class="dc-muted">{{ 'mx.settings.gatesHint' | dcTranslate }}</p>
          <dc-structured-input
            [shape]="gatesShape()"
            [ngModel]="matrix().gates"
            (ngModelChange)="set({ gates: $event ?? [] })"
            testId="mx-gates"
          />
        </mat-expansion-panel>
        <mat-expansion-panel>
          <mat-expansion-panel-header
            >{{ 'mx.settings.adjustments' | dcTranslate }} ({{
              matrix().adjustments.length
            }})</mat-expansion-panel-header
          >
          <p class="dc-muted">{{ 'mx.settings.adjustmentsHint' | dcTranslate }}</p>
          <dc-structured-input
            [shape]="adjustmentsShape()"
            [ngModel]="matrix().adjustments"
            (ngModelChange)="set({ adjustments: $event ?? [] })"
            testId="mx-adjustments"
          />
        </mat-expansion-panel>
        <mat-expansion-panel>
          <mat-expansion-panel-header>{{
            'mx.settings.projects' | dcTranslate
          }}</mat-expansion-panel-header>
          <p class="dc-muted">{{ 'mx.settings.projectsHint' | dcTranslate }}</p>
          <dc-structured-input
            [shape]="projectsShape()"
            [ngModel]="matrix().projects ?? emptyProjects"
            (ngModelChange)="setProjects($event)"
            testId="mx-projects"
          />
        </mat-expansion-panel>
      }
    </mat-accordion>
  `,
  styles: `
    .mx-set__below {
      margin-top: 0.75rem;
      width: min(24rem, 100%);
    }
  `,
})
export class MatrixSettings {
  readonly matrix = input.required<Matrix>();
  /** Which settings to show: grades, scales, sizes and answer lists; or the gates, adjustments and projects. */
  readonly part = input<'grades' | 'rules'>('grades');
  readonly matrixChange = output<Matrix>();
  protected readonly emptyProjects = { input: '', valueField: '', fields: [], rules: [] };

  protected readonly gradesShape: StructuredShape = {
    kind: 'table',
    columns: [
      { key: 'rank', label: t('الترتيب', 'Rank'), type: 'number', required: true },
      CODE,
      ...NAMES,
    ],
  };

  protected readonly scalesShape: StructuredShape = {
    kind: 'table',
    columns: [
      CODE,
      ...NAMES,
      {
        key: 'steps',
        label: t('الدرجات حسب النقاط', 'Grades by points'),
        type: 'table',
        columns: [
          { key: 'min', label: t('من (يشمل)', 'From (inclusive)'), type: 'number' },
          { key: 'max', label: t('إلى (لا يشمل)', 'To (exclusive)'), type: 'number' },
          { key: 'grade', label: t('الدرجة', 'Grade'), type: 'number', required: true },
        ],
      },
    ],
  };

  protected readonly optionSetsShape: StructuredShape = {
    kind: 'table',
    columns: [
      CODE,
      ...NAMES,
      {
        key: 'items',
        label: t('الإجابات', 'Answers'),
        type: 'table',
        columns: [
          CODE,
          { key: 'labelAr', label: t('العنوان بالعربية', 'Arabic label'), type: 'text' },
          {
            key: 'labelEn',
            label: t('العنوان بالإنجليزية', 'English label'),
            type: 'text',
            ltr: true,
          },
          { key: 'ordinal', label: t('الترتيب', 'Order'), type: 'number' },
        ],
      },
    ],
  };

  private readonly inputOptions = computed<FormOption[]>(() =>
    this.matrix().inputs.map((i) => ({
      value: i.code,
      label: { ar: i.nameAr || i.code, en: i.nameEn || i.code },
    })),
  );

  protected readonly sizesShape = computed<StructuredShape>(() => ({
    kind: 'object',
    properties: [
      {
        key: 'input',
        label: t('المدخل الذي يحدد الحجم', 'Input that decides the size'),
        type: 'select',
        options: this.inputOptions(),
      },
      {
        key: 'categories',
        label: t('الأحجام', 'Sizes'),
        type: 'table',
        columns: [
          CODE,
          ...NAMES,
          { key: 'min', label: t('من', 'From'), type: 'number' },
          { key: 'max', label: t('إلى', 'To'), type: 'number' },
        ],
      },
    ],
  }));

  /** Gates and adjustments read the inputs (they are checked before the tracks are worked out). */
  private readonly ruleFields = computed(() => inputFields(this.matrix()));

  protected readonly gatesShape = computed<StructuredShape>(() => ({
    kind: 'table',
    columns: [
      CODE,
      {
        key: 'rows',
        label: t('رموز القطاعات (الكل إن تُركت)', 'Sector codes (all when empty)'),
        type: 'tags',
        ltr: true,
      },
      {
        key: 'rule',
        label: t('الشرط الذي يجب أن يتحقق', 'Must hold'),
        type: 'logic',
        logic: { mode: 'condition', fields: this.ruleFields() },
      },
      { key: 'messageAr', label: t('الرسالة بالعربية', 'Arabic message'), type: 'text' },
      {
        key: 'messageEn',
        label: t('الرسالة بالإنجليزية', 'English message'),
        type: 'text',
        ltr: true,
      },
      {
        key: 'blocking',
        label: t('يمنع التصنيف', 'Blocks grading'),
        type: 'checkbox',
        defaultValue: true,
      },
    ],
  }));

  protected readonly adjustmentsShape = computed<StructuredShape>(() => ({
    kind: 'table',
    columns: [
      CODE,
      ...NAMES,
      {
        key: 'rows',
        label: t('رموز القطاعات (الكل إن تُركت)', 'Sector codes (all when empty)'),
        type: 'tags',
        ltr: true,
      },
      {
        key: 'rule',
        label: t('متى', 'When'),
        type: 'logic',
        logic: { mode: 'condition', fields: this.ruleFields() },
      },
      { key: 'demote', label: t('تخفيض بدرجات', 'Demote by'), type: 'number', defaultValue: 1 },
    ],
  }));

  protected readonly projectsShape = computed<StructuredShape>(() => {
    const fieldOptions: FormOption[] = projectFields(this.matrix()).map((f) => ({
      value: f.path,
      label: f.label,
    }));
    return {
      kind: 'object',
      properties: [
        {
          key: 'input',
          label: t('مدخل قائمة المشاريع', 'Projects list input'),
          type: 'select',
          options: this.inputOptions(),
        },
        {
          key: 'fields',
          label: t('حقول المشروع', 'Project fields'),
          type: 'table',
          columns: [
            CODE,
            ...NAMES,
            {
              key: 'type',
              label: t('النوع', 'Type'),
              type: 'select',
              options: TYPES,
              defaultValue: 'number',
            },
          ],
        },
        {
          key: 'valueField',
          label: t('حقل القيمة', 'Value field'),
          type: 'select',
          options: fieldOptions,
        },
        {
          key: 'certifiedValueField',
          label: t('حقل القيمة المعتمدة', 'Certified value field'),
          type: 'select',
          options: fieldOptions,
        },
        {
          key: 'completionField',
          label: t('حقل نسبة الإنجاز', 'Completion field'),
          type: 'select',
          options: fieldOptions,
        },
        {
          key: 'dateField',
          label: t('حقل التاريخ', 'Date field'),
          type: 'select',
          options: fieldOptions,
        },
        { key: 'splitThreshold', label: t('حد التجزئة', 'Split threshold'), type: 'number' },
        {
          key: 'rules',
          label: t('شروط احتساب المشروع', 'When a project counts'),
          type: 'table',
          columns: [
            CODE,
            {
              key: 'matches',
              label: t('ينطبق على', 'Applies to'),
              type: 'logic',
              logic: { mode: 'condition', fields: projectFields(this.matrix()) },
            },
            { key: 'minCompletion', label: t('أدنى إنجاز %', 'Min completion %'), type: 'number' },
            { key: 'maxAgeYears', label: t('أقصى عمر (سنوات)', 'Max age (years)'), type: 'number' },
            { key: 'minValue', label: t('أدنى قيمة', 'Min value'), type: 'number' },
            {
              key: 'useCertifiedValue',
              label: t('القيمة المعتمدة', 'Use certified value'),
              type: 'checkbox',
            },
          ],
        },
      ],
    };
  });

  protected set(change: Partial<Matrix>): void {
    this.matrixChange.emit({ ...this.matrix(), ...change });
  }

  protected setSizes(value: { input?: string; categories?: unknown[] } | null): void {
    this.set({
      sizes: value?.input || value?.categories?.length ? (value as Matrix['sizes']) : null,
    });
  }

  protected setProjects(value: { input?: string } | null): void {
    this.set({ projects: value?.input ? (value as Matrix['projects']) : null });
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  AppsControlApi,
  DynamicForm,
  FormField,
  FormFieldType,
  FormOption,
  FormSchema,
  FormValue,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { catchError, forkJoin, map, of } from 'rxjs';
import { fromDate, fromJsonText, toDate, toJsonText } from '../config/form-values';
import { DraftsService } from '../drafts.service';
import { AttributeDefinition, AttributeDataType, ClassRow, DomainRow } from './class-models';

export interface DomainEditorData {
  cls: ClassRow;
  row: DomainRow | null;
}

const ATTRIBUTE_PREFIX = 'attr__';

const FIELD_TYPES: Record<AttributeDataType, FormFieldType> = {
  TEXT: 'text',
  TEXTAREA: 'textarea',
  NUMBER: 'number',
  DATE: 'date',
  BOOL: 'checkbox',
  DOMAIN_REF: 'select',
  LOV_REF: 'select',
  JSON: 'textarea',
};

/** Tabs of the value form: the fields grouped by kind (as the legacy flex values, dates and numbers), parameters apart. */
const KINDS: { code: string; title: { ar: string; en: string }; types: AttributeDataType[] }[] = [
  { code: 'texts', title: { ar: 'النصوص', en: 'Texts' }, types: ['TEXT', 'TEXTAREA', 'JSON'] },
  { code: 'choices', title: { ar: 'الاختيارات', en: 'Choices' }, types: ['DOMAIN_REF', 'LOV_REF', 'BOOL'] },
  { code: 'numbers', title: { ar: 'الأرقام', en: 'Numbers' }, types: ['NUMBER'] },
  { code: 'dates', title: { ar: 'التواريخ', en: 'Dates' }, types: ['DATE'] },
];

/**
 * Edits a lookup value. The form is generated from the class's attribute definitions, so adding an attribute to a
 * class adds a field here without any code change.
 */
@Component({
  selector: 'dc-domain-editor',
  imports: [MatDialogModule, MatProgressBarModule, DynamicForm, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ { ar: data.cls.nameAr, en: data.cls.nameEn } | dcLocalize }} — {{ (data.row ? 'config.edit' : 'classes.newValue') | dcTranslate }}
    </h2>
    <mat-dialog-content>
      @if (errors().length) {
        <ul class="dc-errors" role="alert">
          @for (error of errors(); track $index) {
            <li>{{ error }}</li>
          }
        </ul>
      }
      @if (schema.hasValue()) {
        <dc-dynamic-form
          [schema]="schema.value()"
          [initialValue]="initial"
          [submitLabel]="drafts.saveLabel('apps.domain') | dcTranslate"
          [altSubmitLabel]="drafts.needsApproval('apps.domain') ? ('config.submitForApproval' | dcTranslate) : undefined"
          (altSubmitted)="save($event, true)"
          [cancellable]="true"
          (submitted)="save($event)"
          (cancelled)="ref.close()"
        />
      } @else {
        <mat-progress-bar mode="indeterminate" />
      }
    </mat-dialog-content>
  `,
})
export class DomainEditor {
  protected readonly data = inject<DomainEditorData>(MAT_DIALOG_DATA);
  protected readonly ref = inject(MatDialogRef<DomainEditor, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);

  protected readonly errors = signal<string[]>([]);
  protected readonly initial = this.initialValue();

  protected readonly schema = rxResource({
    stream: () => {
      const cls = this.data.cls;
      const parentClass = cls.parentClassCode ?? (cls.isHierarchical ? cls.code : null);
      const refClasses = [...new Set(cls.attributes.filter((a) => a.dataType === 'DOMAIN_REF' && a.refClassCode).map((a) => a.refClassCode!))];
      const refLovs = [...new Set(cls.attributes.filter((a) => a.dataType === 'LOV_REF' && a.refLovCode).map((a) => a.refLovCode!))];
      const toOptions = (code: string) =>
        this.api.classDomains(code).pipe(map((rows) => rows.map((r): FormOption => ({ value: r.code, label: { ar: r.nameAr, en: r.nameEn } }))));
      const lovOptions = (code: string) =>
        this.api.lovOptions(code).pipe(
          map((page) => page.items.map((i): FormOption => ({ value: i.value, label: { ar: i.labelAr, en: i.labelEn } }))),
          catchError(() => of<FormOption[]>([])),
        );
      // "All companies" is a choice of its own (`*`); an empty choice is not valid (ADR-0036).
      const companies = cls.valuesPerCompany
        ? this.api.list('apps.company').pipe(
            map((rows) => [
              { value: '*', label: { ar: 'كل الشركات', en: 'All companies' } } as FormOption,
              ...rows.map((r): FormOption => ({ value: r.code, label: { ar: r.nameAr, en: r.nameEn } })),
            ]),
            catchError(() => of<FormOption[]>([])),
          )
        : of<FormOption[]>([]);
      return forkJoin({
        companies,
        parents: parentClass ? toOptions(parentClass) : of<FormOption[]>([]),
        refs: refClasses.length ? forkJoin(refClasses.map(toOptions)) : of<FormOption[][]>([]),
        lovs: refLovs.length ? forkJoin(refLovs.map(lovOptions)) : of<FormOption[][]>([]),
      }).pipe(
        map(({ companies, parents, refs, lovs }) =>
          this.buildSchema(
            parentClass,
            parents,
            new Map(refClasses.map((c, i) => [c, refs[i]])),
            new Map(refLovs.map((c, i) => [c, lovs[i]])),
            companies,
          ),
        ),
      );
    },
  });

  protected async save(value: FormValue, submit = false): Promise<void> {
    this.errors.set([]);
    const attributes: Record<string, unknown> = {};
    try {
      for (const attribute of this.data.cls.attributes) {
        const raw = value[ATTRIBUTE_PREFIX + attribute.code];
        const converted = this.convert(attribute, raw);
        if (converted !== null && converted !== undefined && converted !== '') {
          attributes[attribute.code] = converted;
        }
      }
    } catch (error) {
      this.errors.set([(error as Error).message]);
      return;
    }

    const code = this.data.row ? this.data.row.code.slice(this.data.cls.code.length + 1) : String(value['code'] ?? '').trim();
    const payload = {
      nameAr: value['nameAr'],
      nameEn: value['nameEn'],
      isActive: value['isActive'] ?? true,
      parentCode: value['parentCode'] ?? null,
      detailsAr: value['detailsAr'] || null,
      detailsEn: value['detailsEn'] || null,
      displayOrder: value['displayOrder'] ?? 0,
      isDefault: value['isDefault'] ?? false,
      effectiveFrom: fromDate(value['effectiveFrom']),
      effectiveTo: fromDate(value['effectiveTo']),
      companies: this.data.cls.valuesPerCompany ? onlyAll((value['companies'] as string[] | null) ?? []) : [],
      attributes,
      externalCodes: (this.data.row?.['externalCodes'] as unknown[] | undefined) ?? [],
    };
    try {
      await this.drafts.addChange('apps.domain', `${this.data.cls.code}/${code}`, this.data.row ? 'UPDATE' : 'CREATE', payload, { submit });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private convert(attribute: AttributeDefinition, raw: unknown): unknown {
    if (attribute.isMultiple) {
      const codes = Array.isArray(raw) ? raw.filter((c) => typeof c === 'string' && c.length > 0) : [];
      return codes.length ? codes : null;
    }
    switch (attribute.dataType) {
      case 'DATE':
        return fromDate(raw);
      case 'BOOL':
        return raw === true;
      case 'JSON':
        try {
          return fromJsonText(raw);
        } catch {
          throw new Error(`${this.i18n.localize({ ar: attribute.nameAr, en: attribute.nameEn })}: ${this.i18n.translate('form.invalidJson')}`);
        }
      case 'NUMBER':
        return raw === null || raw === '' ? null : Number(raw);
      default:
        return typeof raw === 'string' ? raw.trim() || null : raw;
    }
  }

  private initialValue(): FormValue {
    const row = this.data.row;
    const value: FormValue = {
      code: row ? row.code.slice(this.data.cls.code.length + 1) : null,
      nameAr: row?.nameAr ?? null,
      nameEn: row?.nameEn ?? null,
      isActive: row?.isActive ?? true,
      parentCode: row?.parentCode ?? null,
      detailsAr: row?.detailsAr ?? null,
      detailsEn: row?.detailsEn ?? null,
      displayOrder: row?.displayOrder ?? 0,
      isDefault: row?.isDefault ?? false,
      effectiveFrom: toDate(row?.effectiveFrom),
      effectiveTo: toDate(row?.effectiveTo),
      companies: row?.companies ?? [],
    };
    for (const attribute of this.data.cls.attributes) {
      const current = row?.attributes?.[attribute.code];
      value[ATTRIBUTE_PREFIX + attribute.code] = attribute.isMultiple
        ? Array.isArray(current)
          ? current
          : []
        : attribute.dataType === 'DATE'
          ? toDate(current)
          : attribute.dataType === 'JSON'
            ? toJsonText(current)
            : attribute.dataType === 'BOOL'
              ? current === true
              : (current ?? null);
    }
    return value;
  }

  private buildSchema(
    parentClass: string | null,
    parents: FormOption[],
    refs: Map<string, FormOption[]>,
    lovs: Map<string, FormOption[]>,
    companies: FormOption[],
  ): FormSchema {
    const editing = this.data.row !== null;
    const attributeField = (a: AttributeDefinition): FormField => ({
      code: ATTRIBUTE_PREFIX + a.code,
      type: FIELD_TYPES[a.dataType],
      label: { ar: a.nameAr, en: a.nameEn },
      required: a.isRequired && a.dataType !== 'BOOL',
      multiple: a.isMultiple || undefined,
      options:
        a.dataType === 'DOMAIN_REF'
          ? (refs.get(a.refClassCode ?? '') ?? [])
          : a.dataType === 'LOV_REF'
            ? (lovs.get(a.refLovCode ?? '') ?? [])
            : undefined,
    });
    const ordered = [...this.data.cls.attributes].sort((x, y) => x.displayOrder - y.displayOrder || x.code.localeCompare(y.code));
    const attributes = ordered.filter((a) => a.attributeGroup !== 'PARAMETER');
    const parameters = ordered.filter((a) => a.attributeGroup === 'PARAMETER').map(attributeField);
    const kinds = KINDS.map((k) => ({ code: k.code, title: k.title, fields: attributes.filter((a) => k.types.includes(a.dataType)).map(attributeField) })).filter(
      (k) => k.fields.length,
    );

    const general: FormField[] = [
      { code: 'code', type: 'text', label: { ar: 'الرمز', en: 'Code' }, required: true, readonly: editing },
      { code: 'nameAr', type: 'text', label: { ar: 'الاسم بالعربية', en: 'Arabic name' }, required: true },
      { code: 'nameEn', type: 'text', label: { ar: 'الاسم بالإنجليزية', en: 'English name' }, required: true },
      ...(parentClass ? [{ code: 'parentCode', type: 'select', label: { ar: 'القيمة الأعلى', en: 'Parent value' }, options: parents } satisfies FormField] : []),
      ...(this.data.cls.valuesPerCompany
        ? [
            {
              code: 'companies',
              type: 'select',
              multiple: true,
              label: { ar: 'الشركات', en: 'Companies' },
              hint: { ar: 'اختر الشركات أو «كل الشركات»', en: 'Choose companies, or "All companies"' },
              required: true,
              options: companies,
            } satisfies FormField,
          ]
        : []),
      { code: 'displayOrder', type: 'number', label: { ar: 'الترتيب', en: 'Display order' } },
      { code: 'effectiveFrom', type: 'date', label: { ar: 'يسري من', en: 'Effective from' } },
      { code: 'effectiveTo', type: 'date', label: { ar: 'يسري حتى', en: 'Effective to' } },
      { code: 'isDefault', type: 'checkbox', label: { ar: 'القيمة الافتراضية', en: 'Default value' } },
      { code: 'isActive', type: 'checkbox', label: { ar: 'فعال', en: 'Active' } },
      { code: 'detailsAr', type: 'textarea', label: { ar: 'التفاصيل بالعربية', en: 'Details (Arabic)' } },
      { code: 'detailsEn', type: 'textarea', label: { ar: 'التفاصيل بالإنجليزية', en: 'Details (English)' } },
    ];
    const sections = [
      { code: 'general', title: { ar: 'البيانات الأساسية', en: 'General' }, fields: general },
      ...kinds,
      ...(parameters.length ? [{ code: 'parameters', title: { ar: 'المعاملات', en: 'Parameters' }, fields: parameters }] : []),
    ];
    // The general data alone stays one form; with fields, each kind gets its tab.
    return { code: 'domain-' + this.data.cls.code, display: sections.length > 1 ? 'tabs' : 'stacked', sections };
  }
}

/** "All companies" alone when it is among the chosen ones. */
function onlyAll(companies: string[]): string[] {
  return companies.includes('*') ? ['*'] : companies;
}

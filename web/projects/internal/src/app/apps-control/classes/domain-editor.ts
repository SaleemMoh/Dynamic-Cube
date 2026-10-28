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
import { forkJoin, map, of } from 'rxjs';
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
  NUMBER: 'number',
  DATE: 'date',
  BOOL: 'checkbox',
  DOMAIN_REF: 'select',
  JSON: 'textarea',
};

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
      const toOptions = (code: string) =>
        this.api.classDomains(code).pipe(map((rows) => rows.map((r): FormOption => ({ value: r.code, label: { ar: r.nameAr, en: r.nameEn } }))));
      return forkJoin({
        parents: parentClass ? toOptions(parentClass) : of<FormOption[]>([]),
        refs: refClasses.length ? forkJoin(refClasses.map(toOptions)) : of<FormOption[][]>([]),
      }).pipe(map(({ parents, refs }) => this.buildSchema(parentClass, parents, new Map(refClasses.map((c, i) => [c, refs[i]])))));
    },
  });

  protected async save(value: FormValue): Promise<void> {
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
      attributes,
      externalCodes: (this.data.row?.['externalCodes'] as unknown[] | undefined) ?? [],
    };
    try {
      await this.drafts.addChange('apps.domain', `${this.data.cls.code}/${code}`, this.data.row ? 'UPDATE' : 'CREATE', payload);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private convert(attribute: AttributeDefinition, raw: unknown): unknown {
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
    };
    for (const attribute of this.data.cls.attributes) {
      const current = row?.attributes?.[attribute.code];
      value[ATTRIBUTE_PREFIX + attribute.code] =
        attribute.dataType === 'DATE' ? toDate(current) : attribute.dataType === 'JSON' ? toJsonText(current) : attribute.dataType === 'BOOL' ? current === true : (current ?? null);
    }
    return value;
  }

  private buildSchema(parentClass: string | null, parents: FormOption[], refs: Map<string, FormOption[]>): FormSchema {
    const editing = this.data.row !== null;
    const attributeField = (a: AttributeDefinition): FormField => ({
      code: ATTRIBUTE_PREFIX + a.code,
      type: FIELD_TYPES[a.dataType],
      label: { ar: a.nameAr, en: a.nameEn },
      required: a.isRequired && a.dataType !== 'BOOL',
      options: a.dataType === 'DOMAIN_REF' ? (refs.get(a.refClassCode ?? '') ?? []) : undefined,
    });
    const ordered = [...this.data.cls.attributes].sort((x, y) => x.displayOrder - y.displayOrder || x.code.localeCompare(y.code));
    const attributes = ordered.filter((a) => a.attributeGroup !== 'PARAMETER').map(attributeField);
    const parameters = ordered.filter((a) => a.attributeGroup === 'PARAMETER').map(attributeField);

    const general: FormField[] = [
      { code: 'code', type: 'text', label: { ar: 'الرمز', en: 'Code' }, required: true, readonly: editing },
      { code: 'nameAr', type: 'text', label: { ar: 'الاسم بالعربية', en: 'Arabic name' }, required: true },
      { code: 'nameEn', type: 'text', label: { ar: 'الاسم بالإنجليزية', en: 'English name' }, required: true },
      ...(parentClass ? [{ code: 'parentCode', type: 'select', label: { ar: 'القيمة الأعلى', en: 'Parent value' }, options: parents } satisfies FormField] : []),
      { code: 'displayOrder', type: 'number', label: { ar: 'الترتيب', en: 'Display order' } },
      { code: 'effectiveFrom', type: 'date', label: { ar: 'يسري من', en: 'Effective from' } },
      { code: 'effectiveTo', type: 'date', label: { ar: 'يسري حتى', en: 'Effective to' } },
      { code: 'isDefault', type: 'checkbox', label: { ar: 'القيمة الافتراضية', en: 'Default value' } },
      { code: 'isActive', type: 'checkbox', label: { ar: 'فعال', en: 'Active' } },
      { code: 'detailsAr', type: 'textarea', label: { ar: 'التفاصيل بالعربية', en: 'Details (Arabic)' } },
      { code: 'detailsEn', type: 'textarea', label: { ar: 'التفاصيل بالإنجليزية', en: 'Details (English)' } },
    ];
    return {
      code: 'domain-' + this.data.cls.code,
      sections: [
        { code: 'general', title: { ar: 'البيانات الأساسية', en: 'General' }, fields: general },
        ...(attributes.length ? [{ code: 'attributes', title: { ar: 'الخصائص', en: 'Attributes' }, fields: attributes }] : []),
        ...(parameters.length ? [{ code: 'parameters', title: { ar: 'المعاملات', en: 'Parameters' }, fields: parameters }] : []),
      ],
    };
  }
}

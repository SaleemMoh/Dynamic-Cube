import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  AppsControlApi,
  ConfigRow,
  DynamicForm,
  FormField,
  FormOption,
  FormSchema,
  FormValue,
  I18nService,
  LocalizePipe,
  TranslatePipe,
  problemMessages,
} from '@dynamic-cube/shared';
import { forkJoin, map, of } from 'rxjs';
import { DraftsService } from '../drafts.service';
import { ConfigField, ConfigType } from './config-types';
import { emptyToNull, fromDate, fromJsonText, payloadOf, toDate, toJsonText, withCurrentValues } from './form-values';

export interface ConfigEditorData {
  type: ConfigType;
  /** Existing row (edit) or null (new). */
  row: ConfigRow | null;
  /** Starting values of a new row (e.g. the menu group a new menu goes into). */
  defaults?: Record<string, unknown>;
}

/** Edits one configuration row with a {@link DynamicForm}; saving adds the change to the user's draft change set. */
@Component({
  selector: 'dc-config-editor',
  imports: [MatDialogModule, MatProgressBarModule, DynamicForm, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>
      {{ data.type.title | dcLocalize }} — {{ (data.row ? 'config.edit' : 'config.new') | dcTranslate }}
      @if (data.row) {
        <span dir="ltr">{{ data.row.code }}</span>
      }
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
          [submitLabel]="drafts.saveLabel(data.type.entityType) | dcTranslate"
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
export class ConfigEditor {
  protected readonly data = inject<ConfigEditorData>(MAT_DIALOG_DATA);
  protected readonly ref = inject(MatDialogRef<ConfigEditor, boolean>);
  private readonly api = inject(AppsControlApi);
  protected readonly drafts = inject(DraftsService);
  private readonly i18n = inject(I18nService);

  protected readonly errors = signal<string[]>([]);
  protected readonly initial = this.initialValue();

  /** Form schema, once option lists for reference fields are loaded. */
  protected readonly schema = rxResource({
    stream: () => {
      const fields = this.data.type.fields;
      const lookups = fields.filter((f) => f.ref || f.refDomains);
      const sources = lookups.map((f) =>
        f.ref
          ? this.api.list(f.ref).pipe(map((rows) => rows.map((r): FormOption => ({ value: r.code, label: { ar: `${r.nameAr} (${r.code})`, en: `${r.nameEn} (${r.code})` } }))))
          : this.api.classDomains(f.refDomains!).pipe(map((rows) => rows.map((r): FormOption => ({ value: r.code, label: { ar: r.nameAr, en: r.nameEn } })))),
      );
      return (sources.length ? forkJoin(sources) : of([] as FormOption[][])).pipe(
        map((lists) => this.buildSchema(new Map(lookups.map((f, i) => [f.code, lists[i]])))),
      );
    },
  });

  private get isSingleton(): boolean {
    return typeof this.data.type.key === 'object';
  }

  protected async save(value: FormValue): Promise<void> {
    this.errors.set([]);
    const type = this.data.type;
    // Start from the current row so fields edited elsewhere (e.g. the entity's logo and footer links) are kept.
    const payload: Record<string, unknown> = {
      ...payloadOf(this.data.row),
      nameAr: value['nameAr'],
      nameEn: value['nameEn'],
      isActive: value['isActive'] ?? true,
    };
    try {
      for (const field of type.fields) {
        if (!(field.code in value)) {
          continue;
        }
        payload[field.code] = this.fromControl(field, value[field.code]);
      }
    } catch (error) {
      this.errors.set([(error as Error).message]);
      return;
    }

    const code = this.entityCode(value);
    try {
      await this.drafts.addChange(type.entityType, code, this.data.row ? 'UPDATE' : 'CREATE', payload);
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  private entityCode(value: FormValue): string {
    const key = this.data.type.key;
    if (typeof key === 'object') {
      return key.singleton;
    }
    if (this.data.row) {
      return this.data.row.code;
    }
    const code = String(value['code'] ?? '').trim();
    const app = value['appCode'] as string | null | undefined;
    return key === 'appScoped' && app ? `${app}/${code}` : code;
  }

  private fromControl(field: ConfigField, raw: unknown): unknown {
    switch (field.type) {
      case 'date':
        return fromDate(raw);
      case 'json':
        try {
          return fromJsonText(raw);
        } catch {
          throw new Error(`${this.i18n.localize(field.label)}: ${this.i18n.translate('form.invalidJson')}`);
        }
      case 'refList': {
        // Keep the other properties of items that stay selected (e.g. org-unit scope of a role).
        const existing = ((this.data.row?.[field.code] as Record<string, unknown>[] | undefined) ?? []).filter((i) => i && typeof i === 'object');
        return ((raw as string[] | null) ?? []).map((code) => existing.find((i) => i[field.itemKey!] === code) ?? { [field.itemKey!]: code });
      }
      default:
        return emptyToNull(raw);
    }
  }

  private initialValue(): FormValue {
    const row = this.data.row;
    const value: FormValue = {
      code: row?.code ?? (this.isSingleton ? (this.data.type.key as { singleton: string }).singleton : null),
      nameAr: row?.nameAr ?? null,
      nameEn: row?.nameEn ?? null,
      isActive: row?.isActive ?? true,
    };
    if (row && this.data.type.key === 'appScoped' && row.code.includes('/')) {
      value['code'] = row.code.slice(row.code.indexOf('/') + 1);
    }
    for (const field of this.data.type.fields) {
      const current = row ? row[field.code] : (this.data.defaults?.[field.code] ?? field.defaultValue);
      switch (field.type) {
        case 'date':
          value[field.code] = toDate(current);
          break;
        case 'json':
          value[field.code] = toJsonText(current);
          break;
        case 'refList':
          value[field.code] = ((current as Record<string, unknown>[] | undefined) ?? []).map((i) => i[field.itemKey!]);
          break;
        default:
          value[field.code] = current ?? (field.multiple ? [] : null);
      }
    }
    return value;
  }

  private buildSchema(options: Map<string, FormOption[]>): FormSchema {
    const type = this.data.type;
    const editing = this.data.row !== null;
    const general: FormField[] = [
      { code: 'code', type: 'text', label: { ar: 'الرمز', en: 'Code' }, required: true, readonly: editing || this.isSingleton },
      { code: 'nameAr', type: type.nameLabels ? 'textarea' : 'text', label: type.nameLabels?.ar ?? { ar: 'الاسم بالعربية', en: 'Arabic name' }, required: true },
      { code: 'nameEn', type: type.nameLabels ? 'textarea' : 'text', label: type.nameLabels?.en ?? { ar: 'الاسم بالإنجليزية', en: 'English name' }, required: true },
      { code: 'isActive', type: 'checkbox', label: { ar: 'فعال', en: 'Active' } },
    ];
    const details: FormField[] = type.fields.map((f) => ({
      code: f.code,
      type: f.type === 'json' ? 'textarea' : f.type === 'refList' ? 'select' : f.type,
      label: f.label,
      hint: f.hint,
      required: f.required,
      requiredWhen: f.requiredWhen,
      multiple: f.multiple,
      visibleWhen: f.visibleWhen,
      // App-scoped keys cannot move to another app on edit.
      readonly: editing && type.key === 'appScoped' && f.code === 'appCode',
      options: withCurrentValues(options.get(f.code) ?? f.options, this.data.row?.[f.code]),
    }));
    return {
      code: type.entityType,
      sections: [
        { code: 'general', title: { ar: 'البيانات الأساسية', en: 'General' }, fields: general },
        ...(details.length ? [{ code: 'details', title: { ar: 'التفاصيل', en: 'Details' }, fields: details }] : []),
      ],
    };
  }
}

import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  AppsControlApi,
  BusinessAppContext,
  ConfigRow,
  DynamicForm,
  FormField,
  FormOption,
  FormSchema,
  FormValue,
  LocalizePipe,
  StructuredColumn,
  TranslatePipe,
  lookupColumns,
  problemMessages,
  withColumnOptions,
} from '@dynamic-cube/shared';
import { catchError, forkJoin, map, of } from 'rxjs';
import { DraftsService } from '../drafts.service';
import { ConfigField, ConfigType, scopeOf } from './config-types';
import { emptyToNull, fromDate, payloadOf, toDate, withCurrentValues } from './form-values';

/** The field's options with its explicit "all" option (`*`) first, when it has one. */
function withAll(field: ConfigField, options: FormOption[] | undefined): FormOption[] | undefined {
  if (!field.allOption) return options;
  return [{ value: '*', label: field.allOption }, ...(options ?? []).filter((o) => o.value !== '*')];
}

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
          [altSubmitLabel]="drafts.needsApproval(data.type.entityType) ? ('config.submitForApproval' | dcTranslate) : undefined"
          [cancellable]="true"
          (submitted)="save($event)"
          (altSubmitted)="save($event, true)"
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
  private readonly http = inject(HttpClient);
  protected readonly drafts = inject(DraftsService);
  private readonly appContext = inject(BusinessAppContext);

  protected readonly errors = signal<string[]>([]);
  protected readonly initial = this.initialValue();
  private columnOptions: (column: StructuredColumn) => FormOption[] | undefined = (c) => c.options;

  /** Form schema, once option lists for reference fields are loaded. */
  protected readonly schema = rxResource({
    stream: () => {
      const fields = [...this.data.type.fields, ...this.scopeFields()];
      const lookups = fields.filter((f) => f.ref || f.refDomains || f.optionsUrl);
      const coded = (rows: { code: string; nameAr: string; nameEn: string }[]) =>
        rows.map((r): FormOption => ({ value: r.code, label: { ar: `${r.nameAr} (${r.code})`, en: `${r.nameEn} (${r.code})` } }));
      const sources = lookups.map((f) =>
        f.ref
          ? this.api.list(f.ref).pipe(map(coded))
          : f.optionsUrl
            ? this.http.get<{ code: string; nameAr: string; nameEn: string }[]>(f.optionsUrl).pipe(
                map(coded),
                catchError(() => of([] as FormOption[])),
              )
            : // A class's values; the field's own options stand in when the class has none or cannot be read.
              this.api.classDomains(f.refDomains!).pipe(
                map((rows) => rows.map((r): FormOption => ({ value: r.code, label: { ar: r.nameAr, en: r.nameEn } }))),
                map((options) => (options.length ? options : (f.options ?? []))),
                catchError(() => of(f.options ?? [])),
              ),
      );
      // Columns of nested data whose options are rows of another type or values of a class.
      const columns = fields.flatMap((f) => lookupColumns(f.structure));
      const columnKey = (c: StructuredColumn) => (c.ref ? 'ref:' + c.ref : c.optionsUrl ? 'url:' + c.optionsUrl : 'class:' + c.refDomains);
      const columnKeys = [...new Set(columns.map(columnKey))];
      const columnSources = columnKeys.map((key) =>
        key.startsWith('ref:')
          ? this.api.list(key.slice(4)).pipe(map(coded), catchError(() => of([] as FormOption[])))
          : key.startsWith('url:')
            ? this.http.get<{ code: string; nameAr: string; nameEn: string }[]>(key.slice(4)).pipe(map(coded), catchError(() => of([] as FormOption[])))
            : this.api.classDomains(key.slice(6)).pipe(
              map((rows) => rows.map((r): FormOption => ({ value: r.code, label: { ar: r.nameAr, en: r.nameEn } }))),
              catchError(() => of([] as FormOption[])),
            ),
      );
      const all = [...sources, ...columnSources];
      return (all.length ? forkJoin(all) : of([] as FormOption[][])).pipe(
        map((lists) => {
          const columnOptions = new Map(columnKeys.map((k, i) => [k, lists[sources.length + i]]));
          this.columnOptions = (c) => {
            const loaded = columnOptions.get(columnKey(c));
            return loaded?.length ? loaded : c.options;
          };
          return this.buildSchema(new Map(lookups.map((f, i) => [f.code, lists[i]])));
        }),
      );
    },
  });

  private get isSingleton(): boolean {
    return typeof this.data.type.key === 'object';
  }

  protected async save(value: FormValue, submit = false): Promise<void> {
    this.errors.set([]);
    const payload = this.payloadFrom(value);
    if (!payload) return;
    try {
      await this.drafts.addChange(this.data.type.entityType, this.entityCode(value), this.data.row ? 'UPDATE' : 'CREATE', payload, { submit });
      this.ref.close(true);
    } catch (error) {
      this.errors.set(problemMessages(error));
    }
  }

  /** The row's payload from the form's value; null (with the problem shown) when a value cannot be read. */
  protected payloadFrom(value: FormValue): Record<string, unknown> | null {
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
      return null;
    }

    // The row's level; an empty string tells the server "none" (a missing one would keep the current level).
    const scope = scopeOf(type);
    if (scope !== 'none') {
      payload['entityCode'] = (value['entityCode'] as string | null) || '';
      // Several apps: the first is the row's own, the others share it.
      if (scope === 'full') payload['businessApps'] = (value['businessApps'] as string[] | null) ?? [];
    }
    return payload;
  }

  protected entityCode(value: FormValue): string {
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
        // An emptied list stays a list (the server's payload lists are not nullable).
        return raw === null || raw === undefined || raw === '' ? (Array.isArray(field.defaultValue) ? [] : null) : raw;
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
    // A new row starts in the business app chosen in the header.
    value['entityCode'] = row ? (row['entityCode'] ?? null) : null;
    const current = this.appContext.current();
    const preset = this.data.defaults?.['businessApps'] as string[] | undefined;
    value['businessApps'] = row
      ? ((row['businessApps'] as string[] | undefined) ?? (row['businessApp'] ? [row['businessApp'] as string] : []))
      : (preset ?? (current ? [current] : []));
    for (const field of this.data.type.fields) {
      const current = row ? row[field.code] : (this.data.defaults?.[field.code] ?? field.defaultValue);
      switch (field.type) {
        case 'date':
          value[field.code] = toDate(current);
          break;
        case 'json':
          value[field.code] = current ?? null;
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
      type: f.type === 'json' ? 'structured' : f.type === 'refList' ? (f.picker === 'dual' ? 'dualList' : 'select') : f.type,
      structure: f.type === 'json' ? withColumnOptions(f.structure ?? { kind: 'json' }, this.columnOptions) : undefined,
      label: f.label,
      hint: f.hint,
      required: f.required,
      requiredWhen: f.requiredWhen,
      multiple: f.multiple,
      visibleWhen: f.visibleWhen,
      // App-scoped keys cannot move to another app on edit.
      readonly: editing && ((type.key === 'appScoped' && f.code === 'appCode') || !!f.lockedOnEdit),
      options: withAll(f, withCurrentValues(options.get(f.code) ?? f.options, this.data.row?.[f.code])),
    }));
    const level: FormField[] = this.scopeFields().map((f) => ({
      code: f.code,
      type: 'select',
      multiple: f.multiple,
      label: f.label,
      hint: f.hint,
      options: withCurrentValues(options.get(f.code) ?? [], this.data.row?.[f.code]),
    }));
    return {
      code: type.entityType,
      sections: [
        { code: 'general', title: { ar: 'البيانات الأساسية', en: 'General' }, fields: general },
        ...(details.length ? [{ code: 'details', title: { ar: 'التفاصيل', en: 'Details' }, fields: details }] : []),
        ...(level.length ? [{ code: 'level', title: { ar: 'المستوى', en: 'Level' }, fields: level }] : []),
      ],
    };
  }

  /** The level fields of the type: the entity, and the business app (none for rows of the whole installation). */
  private scopeFields(): ConfigField[] {
    const scope = scopeOf(this.data.type);
    if (scope === 'none') return [];
    const entity: ConfigField = {
      code: 'entityCode',
      type: 'select',
      label: { ar: 'الجهة', en: 'Organization' },
      hint: { ar: 'فارغ: لكل الجهات (مستوى النظام)', en: 'Empty: every organization (system level)' },
      ref: 'apps.entity',
    };
    const app: ConfigField = {
      code: 'businessApps',
      type: 'select',
      multiple: true,
      label: { ar: 'التطبيقات', en: 'Apps' },
      hint: {
        ar: 'فارغ: لكل تطبيقات الجهة. يمكن اختيار أكثر من تطبيق لنفس الجهة؛ يُحدد معه الجهة تلقائيًا.',
        en: 'Empty: every app of the organization. Several apps of one organization may share a row; choosing sets the organization.',
      },
      ref: 'apps.business-app',
    };
    return scope === 'entity' ? [entity] : [entity, app];
  }
}

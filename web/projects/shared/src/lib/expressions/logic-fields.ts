import { Injectable, Signal, inject, signal } from '@angular/core';
import { catchError, map, of } from 'rxjs';
import { AppsControlApi } from '../apps-control/apps-control.api';
import { LocalizedText } from '../i18n/language';
import { CompareOp } from './logic-model';

export type LogicFieldType = 'text' | 'number' | 'date' | 'boolean' | 'list' | 'rows';

/** One choice a field can hold. */
export interface LogicOption {
  value: string;
  label: LocalizedText;
}

/**
 * Something a rule can read, offered in the Logic Builder's field menu: an answer (`data.CLS_SECTORS`), the level, a
 * party fact, a task fact, a table column…. Its values may come from fixed options, a class (Class/Domain) or a list
 * of values, so the builder offers them in a dropdown instead of codes typed by hand.
 */
export interface LogicField {
  path: string;
  label: LocalizedText;
  type?: LogicFieldType;
  options?: LogicOption[];
  /** The field's values are the values of this class (Class/Domain). */
  classCode?: string;
  /** The field's values are the values of this list of values. */
  lovCode?: string;
  /** For `rows`: the columns of each row (paths relative to the row). */
  columns?: LogicField[];
  /** Heading the field is listed under in the menu (Answers, Applicant, Task…). */
  group?: LocalizedText;
}

const BY_TYPE: Record<LogicFieldType, CompareOp[]> = {
  text: ['eq', 'ne', 'in', 'notIn', 'contains', 'startsWith', 'filled', 'empty'],
  number: ['eq', 'ne', 'gt', 'ge', 'lt', 'le', 'between', 'in', 'notIn', 'filled', 'empty'],
  date: ['eq', 'ne', 'gt', 'ge', 'lt', 'le', 'between', 'filled', 'empty'],
  boolean: ['isTrue', 'isFalse', 'eq', 'ne', 'filled', 'empty'],
  list: ['contains', 'filled', 'empty'],
  rows: ['filled', 'empty'],
};

const ALL_OPS: CompareOp[] = [
  'eq',
  'ne',
  'gt',
  'ge',
  'lt',
  'le',
  'between',
  'in',
  'notIn',
  'contains',
  'startsWith',
  'filled',
  'empty',
  'isTrue',
  'isFalse',
];

/** The comparisons that make sense for a field (all of them when its type is not known). */
export function operatorsFor(field: LogicField | undefined, current?: CompareOp): CompareOp[] {
  const ops = field?.type ? BY_TYPE[field.type] : ALL_OPS;
  return current && !ops.includes(current) ? [...ops, current] : ops;
}

/** True when a field's values are offered as choices. */
export function hasChoices(field: LogicField | undefined): boolean {
  return !!field && (!!field.options?.length || !!field.classCode || !!field.lovCode);
}

/**
 * Loads the values of classes and lists of values once per page, for the builder's dropdowns. A source that cannot be
 * read (no access, unknown code) gives no choices, and the builder falls back to a typed value.
 */
@Injectable({ providedIn: 'root' })
export class LogicValues {
  private readonly api = inject(AppsControlApi);
  private readonly cache = new Map<string, Signal<LogicOption[] | null>>();

  /** The values of a field's class or list of values (null while loading), or null when it has neither. */
  source(field: LogicField | undefined): Signal<LogicOption[] | null> | null {
    if (field?.classCode)
      return this.load(`class:${field.classCode}`, () => this.classValues(field.classCode!));
    if (field?.lovCode)
      return this.load(`lov:${field.lovCode}`, () => this.lovValues(field.lovCode!));
    return null;
  }

  private load(
    key: string,
    fetch: () => ReturnType<LogicValues['classValues']>,
  ): Signal<LogicOption[] | null> {
    let cached = this.cache.get(key);
    if (!cached) {
      const value = signal<LogicOption[] | null>(null);
      fetch().subscribe((options) => value.set(options));
      cached = value.asReadonly();
      this.cache.set(key, cached);
    }
    return cached;
  }

  private classValues(code: string) {
    return this.api.classDomains(code).pipe(
      map((items) => items.map((d) => ({ value: d.code, label: { ar: d.nameAr, en: d.nameEn } }))),
      catchError(() => of([] as LogicOption[])),
    );
  }

  private lovValues(code: string) {
    return this.api.lovOptions(code).pipe(
      map((page) =>
        page.items.map((i) => ({ value: i.value, label: { ar: i.labelAr, en: i.labelEn } })),
      ),
      catchError(() => of([] as LogicOption[])),
    );
  }
}

/** A question or column as the Logic Builder needs it. */
export interface AnswerLike {
  label: LocalizedText;
  type?: string;
  multiple?: boolean;
  lov?: string | null;
  options?: LogicOption[];
  columns?: {
    code: string;
    labelAr: string;
    labelEn: string;
    type: string;
    multiple?: boolean;
    lov?: string | null;
    options?: { value: string; labelAr: string; labelEn: string }[];
  }[];
}

/** How the Logic Builder treats an answer type (its comparisons and value input). */
export function answerType(type: string | undefined, multiple = false): LogicFieldType {
  switch (type) {
    case 'NUMBER':
      return 'number';
    case 'DATE':
      return 'date';
    case 'CHECKBOX':
      return 'boolean';
    case 'ROWS':
      return 'rows';
    case 'SELECT':
    case 'FILE':
      return multiple ? 'list' : 'text';
    default:
      return 'text';
  }
}

/** A question's answer as a Logic Builder field: its type, choices (options or list of values) and row columns. */
export function answerField(path: string, q: AnswerLike, group?: LocalizedText): LogicField {
  return {
    path,
    label: q.label,
    type: answerType(q.type, q.multiple),
    options: q.type === 'CHECKBOX' || !q.options?.length ? undefined : q.options,
    lovCode: q.lov ?? undefined,
    columns: q.columns?.length
      ? q.columns.map((c) =>
          answerField(c.code, {
            label: { ar: c.labelAr || c.code, en: c.labelEn || c.code },
            type: c.type,
            multiple: c.multiple,
            lov: c.lov,
            options: c.options?.map((o) => ({
              value: o.value,
              label: { ar: o.labelAr, en: o.labelEn },
            })),
          }),
        )
      : undefined,
    group,
  };
}

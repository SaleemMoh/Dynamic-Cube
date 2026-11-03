import { Injectable, signal } from '@angular/core';
import { I18nService } from '../i18n/i18n.service';
import { Language, localize } from '../i18n/language';
import { LogicField, LogicValues } from './logic-fields';
import { CompareOp, LogicWords } from './logic-model';
import { LOGIC_TRANSLATIONS } from './logic-translations';

const registered = new WeakSet<I18nService>();
const OPS: CompareOp[] = [
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

/** Adds the Logic Builder's texts once per app. */
export function registerLogicTexts(i18n: I18nService): void {
  if (registered.has(i18n)) return;
  registered.add(i18n);
  i18n.register(LOGIC_TRANSLATIONS);
}

/** The words a rule is said with, in the current language. */
export function logicWords(i18n: I18nService): LogicWords {
  const t = (key: string) => i18n.translate(key);
  return {
    and: t('logic.and'),
    or: t('logic.or'),
    not: t('logic.notWord'),
    always: t('logic.always'),
    empty: t('logic.empty'),
    op: Object.fromEntries(OPS.map((op) => [op, t(`logic.op.${op}`)])) as Record<CompareOp, string>,
    quantifier: { some: t('logic.q.some'), all: t('logic.q.all'), none: t('logic.q.none') },
    rowsOf: t('logic.rowsOf'),
    where: t('logic.where'),
    sumOf: t('logic.sumOf'),
    countOf: t('logic.countOf'),
    if: t('logic.if'),
    then: t('logic.then'),
    otherwise: t('logic.otherwise'),
    advanced: t('logic.advancedPiece'),
    yes: t('logic.yes'),
    no: t('logic.no'),
    joined: t('logic.joined'),
  };
}

/** A field's name; for a column of a list (`data.PROJECTS.VALUE`) the column's name; else the path. */
export function logicLabel(fields: LogicField[], path: string, language: Language): string {
  const field = fields.find((f) => f.path === path);
  if (field) return localize(field.label, language);
  const list = fields.find((f) => f.columns?.length && path.startsWith(f.path + '.'));
  const column = list?.columns?.find((c) => c.path === path.slice(list.path.length + 1));
  return column ? localize(column.label, language) : list ? path.slice(list.path.length + 1) : path;
}

/** The name of a field's choice (its options, or its class's or list's values once loaded). */
export function choiceName(
  fields: LogicField[],
  values: LogicValues,
  path: string,
  value: string,
  language: Language,
): string | undefined {
  const field = fields.find((f) => f.path === path);
  if (!field) return undefined;
  const choices = field.options?.length ? field.options : (values.source(field)?.() ?? []);
  const choice = choices.find((c) => c.value === value);
  return choice ? localize(choice.label, language) : undefined;
}

/**
 * Named lists of fields screens can point the Logic Builder at (e.g. `task` for automation rules, `party` for a
 * calculator's prefill), registered by the app that knows them.
 */
@Injectable({ providedIn: 'root' })
export class LogicContexts {
  private readonly providers = signal<Record<string, () => LogicField[]>>({});

  register(name: string, fields: () => LogicField[]): void {
    this.providers.update((all) => ({ ...all, [name]: fields }));
  }

  fields(name: string | undefined): LogicField[] {
    if (!name) return [];
    return this.providers()[name]?.() ?? [];
  }
}

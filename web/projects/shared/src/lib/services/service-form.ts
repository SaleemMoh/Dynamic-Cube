import { JsonLogicRule, jsonLogic } from '../expressions/jsonlogic';
import { FieldWidth, FormDisplay, FormField, FormFieldType, FormSchema, FormSection } from '../forms/form-schema';
import { problemMessages } from '../apps-control/apps-control.models';
import { EligibilityFailure, FieldError, QuestionSnapshot, QuestionUse, ResolvedVersion, StepDefinition, isLocked } from './service.models';

const TYPES: Record<QuestionSnapshot['type'], FormFieldType> = {
  TEXT: 'text',
  TEXTAREA: 'textarea',
  NUMBER: 'number',
  DATE: 'date',
  SELECT: 'select',
  CHECKBOX: 'checkbox',
  FILE: 'file',
};

const both = (a: JsonLogicRule | null | undefined, b: JsonLogicRule | null | undefined): JsonLogicRule | undefined =>
  a && b ? { and: [a, b] } : (a ?? b ?? undefined);

/**
 * The form of one wizard step. A question used in several places is asked where it first shows: later uses are hidden
 * while an earlier one (in this or an earlier step) is visible — the same rule the server applies. Rules are evaluated
 * on all answers, so pass the request's answers as the form's context.
 */
export function stepSchema(version: ResolvedVersion, step: StepDefinition): FormSchema {
  const earlier = new Map<string, JsonLogicRule[]>();
  const sections: FormSection[] = [];
  for (const current of version.definition.steps.filter((s) => s.type === 'FORM')) {
    for (const section of current.sections) {
      const fields: FormField[] = [];
      for (const use of section.questions) {
        const question = version.questions[use.code];
        const before = earlier.get(use.code) ?? [];
        const shownBefore: JsonLogicRule | undefined = before.length ? { or: before } : undefined;
        const visibleWhen = both(use.visibleWhen, shownBefore ? { '!': shownBefore } : undefined);
        if (current.code === step.code && question) {
          fields.push({ ...toField(question, use.required, use.requiredWhen ?? undefined, visibleWhen, use.readonly || isLocked(question)), width: widthOf(use.width) });
        }
        earlier.set(use.code, [...before, both(section.visibleWhen, use.visibleWhen) ?? true]);
      }
      if (current.code === step.code) {
        sections.push({
          code: section.code,
          title: { ar: section.titleAr, en: section.titleEn },
          visibleWhen: section.visibleWhen ?? undefined,
          columns: section.columns ?? undefined,
          fields,
        });
      }
    }
  }
  const display: FormDisplay | undefined = step.display ? (step.display.toLowerCase() as FormDisplay) : undefined;
  return { code: `${version.serviceCode}-${version.number}-${step.code}`, display, sections };
}

function widthOf(width: QuestionUse['width']): FieldWidth | undefined {
  return width ? (width.toLowerCase() as FieldWidth) : undefined;
}

function toField(
  q: QuestionSnapshot,
  required: boolean,
  requiredWhen: JsonLogicRule | undefined,
  visibleWhen: JsonLogicRule | undefined,
  readonly: boolean,
): FormField {
  return {
    code: q.code,
    type: TYPES[q.type],
    label: { ar: q.labelAr, en: q.labelEn },
    hint: q.hintAr || q.hintEn ? { ar: q.hintAr ?? '', en: q.hintEn ?? '' } : undefined,
    required,
    requiredWhen,
    visibleWhen,
    options: q.options.map((o) => ({ value: o.value, label: { ar: o.labelAr, en: o.labelEn } })),
    lov: q.lov ?? undefined,
    lovParentField: q.lovParentQuestion ?? undefined,
    multiple: q.multiple,
    readonly,
    min: q.validation.min ?? undefined,
    max: q.validation.max ?? undefined,
    maxLength: q.validation.maxLength ?? undefined,
    pattern: q.validation.pattern ?? undefined,
    accept: q.validation.extensions?.length ? q.validation.extensions : undefined,
    maxSizeMb: q.validation.maxSizeMb ?? undefined,
    maxFiles: q.validation.maxFiles ?? undefined,
  };
}

/** Sections and questions shown for these answers, each question once (for review screens). */
export function answeredSections(
  version: ResolvedVersion,
  data: Record<string, unknown>,
): { titleAr: string; titleEn: string; questions: QuestionSnapshot[] }[] {
  const shown = new Set<string>();
  const result: { titleAr: string; titleEn: string; questions: QuestionSnapshot[] }[] = [];
  for (const step of version.definition.steps.filter((s) => s.type === 'FORM')) {
    for (const section of step.sections) {
      if (section.visibleWhen && !jsonLogic.evaluateTruthy(section.visibleWhen, data as never)) continue;
      const questions = section.questions
        .filter((use) => !shown.has(use.code) && (!use.visibleWhen || jsonLogic.evaluateTruthy(use.visibleWhen, data as never)))
        .map((use) => version.questions[use.code])
        .filter((q): q is QuestionSnapshot => !!q);
      questions.forEach((q) => shown.add(q.code));
      if (questions.length) result.push({ titleAr: section.titleAr, titleEn: section.titleEn, questions });
    }
  }
  return result;
}

/** An answer as text: option labels for choices, yes/no for checkboxes, file names for files. */
export function displayAnswer(question: QuestionSnapshot, value: unknown, language: 'ar' | 'en', files: { id: string; fileName: string }[] = []): string {
  if (value === null || value === undefined || value === '' || (Array.isArray(value) && value.length === 0)) return '—';
  if (question.type === 'FILE') {
    const ids = Array.isArray(value) ? value : [value];
    return ids.map((id) => files.find((f) => f.id === id)?.fileName ?? String(id)).join('، ');
  }
  const label = (v: unknown) => {
    const option = question.options.find((o) => o.value === v);
    return option ? (language === 'ar' ? option.labelAr : option.labelEn) : String(v);
  };
  if (Array.isArray(value)) return value.map(label).join('، ');
  if (question.type === 'CHECKBOX') return value ? (language === 'ar' ? 'نعم' : 'Yes') : language === 'ar' ? 'لا' : 'No';
  if (question.type === 'SELECT') return label(value);
  return String(value);
}

/** Converts form values for the API: dates become yyyy-MM-dd. */
export function toAnswers(value: Record<string, unknown>): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const [key, v] of Object.entries(value)) {
    result[key] = v instanceof Date ? `${v.getFullYear()}-${String(v.getMonth() + 1).padStart(2, '0')}-${String(v.getDate()).padStart(2, '0')}` : v;
  }
  return result;
}

/** Converts API answers for a form: date questions become Date objects for the date picker. */
export function fromAnswers(version: ResolvedVersion, data: Record<string, unknown>): Record<string, unknown> {
  const result: Record<string, unknown> = { ...data };
  for (const [code, value] of Object.entries(data)) {
    if (version.questions[code]?.type === 'DATE' && typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) {
      const [y, m, d] = value.slice(0, 10).split('-').map(Number);
      result[code] = new Date(y, m - 1, d);
    }
  }
  return result;
}

/** Problems returned by the Services API as bilingual field errors (validation errors, eligibility reasons or others). */
export function requestErrors(error: unknown): FieldError[] {
  const body = (error as { error?: { errors?: unknown; reasons?: EligibilityFailure[] } } | null)?.error;
  if (Array.isArray(body?.errors)) return body.errors as FieldError[];
  if (Array.isArray(body?.reasons)) return body.reasons.map((r) => ({ field: null, code: r.code, messageAr: r.messageAr, messageEn: r.messageEn, step: null }));
  return problemMessages(error).map((m) => ({ field: null, code: 'ERROR', messageAr: m, messageEn: m, step: null }));
}

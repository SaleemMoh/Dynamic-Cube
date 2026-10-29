import { JsonValue, QuestionUse, SectionDefinition, ServiceCardDefinition, ServiceLevelDefinition, StepDefinition, StepType } from '@dynamic-cube/shared';

export type Rule = JsonValue;

/** An eligibility or validation rule with the message shown when it fails. */
export interface RuleDef {
  code: string;
  rule: Rule | null;
  messageAr: string;
  messageEn: string;
  /** Validations: the step whose answers it checks. */
  step?: string | null;
}

export interface BracketRow {
  from: number;
  to: number | null;
  amount: number;
}

export interface FeeDef {
  code: string;
  nameAr: string;
  nameEn: string;
  levels: string[];
  when?: Rule | null;
  fixed?: number | null;
  formula?: Rule | null;
  bracket?: { of: Rule | null; rows: BracketRow[] } | null;
  vatRate: number;
  vatIncluded: boolean;
}

export interface DateBound {
  of: 'ISSUED' | 'EXPIRES';
  days: number;
}

export interface AvailabilityDef {
  statuses: string[];
  from?: DateBound | null;
  to?: DateBound | null;
}

/** A service version's definition as the builder edits it (what the server stores, field for field). */
export interface ServiceDraft {
  levels: ServiceLevelDefinition[];
  steps: StepDefinition[];
  eligibility: RuleDef[];
  validations: RuleDef[];
  fees: FeeDef[];
  availability?: AvailabilityDef | null;
  card: ServiceCardDefinition;
  [key: string]: unknown;
}

export const STEP_TYPES: StepType[] = ['FORM', 'LEVEL', 'FEES', 'REVIEW', 'DECLARATION'];

export const EMPTY_CARD: ServiceCardDefinition = {
  audienceAr: null,
  audienceEn: null,
  steps: [],
  termsAr: null,
  termsEn: null,
  requiredDocuments: [],
  channels: ['PORTAL'],
  faqUrl: null,
  phone: null,
  email: null,
};

export const STARTER: ServiceDraft = {
  levels: [],
  steps: [
    {
      code: 'DETAILS',
      type: 'FORM',
      titleAr: 'البيانات',
      titleEn: 'Details',
      sections: [{ code: 'MAIN', titleAr: 'البيانات الأساسية', titleEn: 'Main data', questions: [] }],
    },
    { code: 'REVIEW', type: 'REVIEW', titleAr: 'المراجعة', titleEn: 'Review', sections: [] },
  ],
  eligibility: [],
  validations: [],
  fees: [],
  card: EMPTY_CARD,
};

/** Fills in the parts an older or hand-written definition may lack, so every editor has something to work on. */
export function normalize(definition: Partial<ServiceDraft> | null | undefined): ServiceDraft {
  const d = structuredClone(definition ?? STARTER) as Partial<ServiceDraft>;
  return {
    ...d,
    levels: d.levels ?? [],
    steps: (d.steps ?? []).map((s) => ({ ...s, sections: (s.sections ?? []).map((x) => ({ ...x, questions: x.questions ?? [] })) })),
    eligibility: d.eligibility ?? [],
    validations: d.validations ?? [],
    fees: (d.fees ?? []).map((f) => ({ ...f, levels: f.levels ?? [], vatRate: f.vatRate ?? 0.15, vatIncluded: f.vatIncluded ?? true })),
    availability: d.availability ?? null,
    card: { ...EMPTY_CARD, ...(d.card ?? {}) },
  };
}

/** Where something is in the form: a step, a section of a step, or a question of a section. */
export interface FormPath {
  step: number;
  section?: number;
  question?: number;
}

/** A code not used yet among `used`: the prefix and the first free number. */
export function freeCode(used: Iterable<string>, prefix: string): string {
  const taken = new Set(used);
  if (!taken.has(prefix)) return prefix;
  for (let i = 2; ; i++) if (!taken.has(`${prefix}_${i}`)) return `${prefix}_${i}`;
}

function clone(def: ServiceDraft): ServiceDraft {
  return structuredClone(def);
}

const STEP_TITLES: Record<StepType, [string, string]> = {
  FORM: ['بيانات', 'Details'],
  LEVEL: ['مستوى الخدمة', 'Service level'],
  FEES: ['الرسوم', 'Fees'],
  REVIEW: ['المراجعة', 'Review'],
  DECLARATION: ['الإقرار', 'Declaration'],
};

/** Adds a step of a type after `after` (at the end by default); a form step comes with an empty section. */
export function addStep(def: ServiceDraft, type: StepType, after?: number): { def: ServiceDraft; index: number } {
  const next = clone(def);
  const [ar, en] = STEP_TITLES[type];
  const code = freeCode(
    next.steps.map((s) => s.code),
    type === 'FORM' ? 'STEP' : type,
  );
  const sectionCode = freeCode(
    next.steps.flatMap((s) => s.sections.map((x) => x.code)),
    'SECTION',
  );
  const step: StepDefinition = {
    code,
    type,
    titleAr: ar,
    titleEn: en,
    sections: type === 'FORM' ? [{ code: sectionCode, titleAr: 'قسم جديد', titleEn: 'New section', questions: [] }] : [],
    ...(type === 'DECLARATION' ? { textAr: 'أقر بصحة البيانات المقدمة.', textEn: 'I confirm the information given is correct.' } : {}),
  };
  const index = after === undefined ? next.steps.length : after + 1;
  next.steps.splice(index, 0, step);
  return { def: next, index };
}

export function addSection(def: ServiceDraft, step: number): { def: ServiceDraft; index: number } {
  const next = clone(def);
  const code = freeCode(
    next.steps.flatMap((s) => s.sections.map((x) => x.code)),
    'SECTION',
  );
  next.steps[step].sections.push({ code, titleAr: 'قسم جديد', titleEn: 'New section', questions: [] });
  return { def: next, index: next.steps[step].sections.length - 1 };
}

/** Puts a bank question in a section (at `at`, or the end); a question already in that section is not added twice. */
export function addQuestion(def: ServiceDraft, step: number, section: number, code: string, at?: number): ServiceDraft {
  const target = def.steps[step]?.sections[section];
  if (!target || target.questions.some((q) => q.code === code)) return def;
  const next = clone(def);
  const questions = next.steps[step].sections[section].questions;
  questions.splice(at ?? questions.length, 0, { code, required: false, readonly: false });
  return next;
}

/** Moves an item within its list (steps, a step's sections, a section's questions). */
export function move<T>(list: T[], from: number, to: number): T[] {
  const copy = [...list];
  const [item] = copy.splice(from, 1);
  copy.splice(Math.max(0, Math.min(copy.length, to)), 0, item);
  return copy;
}

export function moveStep(def: ServiceDraft, from: number, to: number): ServiceDraft {
  return { ...def, steps: move(def.steps, from, to) };
}

export function moveSection(def: ServiceDraft, step: number, from: number, to: number): ServiceDraft {
  const next = clone(def);
  next.steps[step].sections = move(next.steps[step].sections, from, to);
  return next;
}

/** Moves a question within a section or to another section (of any form step). */
export function moveQuestion(
  def: ServiceDraft,
  from: Required<Omit<FormPath, 'question'>> & { question: number },
  to: { step: number; section: number; index: number },
): ServiceDraft {
  const next = clone(def);
  const source = next.steps[from.step].sections[from.section].questions;
  const [use] = source.splice(from.question, 1);
  const target = next.steps[to.step].sections[to.section].questions;
  if (target.some((q) => q.code === use.code)) return def;
  target.splice(Math.max(0, Math.min(target.length, to.index)), 0, use);
  return next;
}

export function remove(def: ServiceDraft, path: FormPath): ServiceDraft {
  const next = clone(def);
  if (path.question !== undefined && path.section !== undefined) next.steps[path.step].sections[path.section].questions.splice(path.question, 1);
  else if (path.section !== undefined) next.steps[path.step].sections.splice(path.section, 1);
  else next.steps.splice(path.step, 1);
  return next;
}

export function updateStep(def: ServiceDraft, step: number, change: Partial<StepDefinition>): ServiceDraft {
  const next = clone(def);
  next.steps[step] = { ...next.steps[step], ...change };
  return next;
}

export function updateSection(def: ServiceDraft, step: number, section: number, change: Partial<SectionDefinition>): ServiceDraft {
  const next = clone(def);
  next.steps[step].sections[section] = { ...next.steps[step].sections[section], ...change };
  return next;
}

export function updateQuestion(def: ServiceDraft, path: Required<FormPath>, change: Partial<QuestionUse>): ServiceDraft {
  const next = clone(def);
  const list = next.steps[path.step].sections[path.section].questions;
  list[path.question] = { ...list[path.question], ...change };
  return next;
}

/** Codes of the questions the form uses, in order, each once. */
export function usedQuestions(def: ServiceDraft): string[] {
  return [...new Set(def.steps.flatMap((s) => s.sections.flatMap((x) => x.questions.map((q) => q.code))))];
}

export interface Problem {
  /** Which tab it belongs to. */
  tab: 'form' | 'rules' | 'card';
  /** Translation key. */
  key: string;
  params?: Record<string, string | number>;
  path?: FormPath;
}

const CODE = /^[A-Z][A-Z0-9_-]{0,50}$/;

/** The checks the server makes that can be made here; the server check covers the rest (rule syntax, references). */
export function check(def: ServiceDraft, bank: ReadonlySet<string>): Problem[] {
  const problems: Problem[] = [];
  const p = (tab: Problem['tab'], key: string, params?: Problem['params'], path?: FormPath) => problems.push({ tab, key, params, path });
  if (!def.steps.length) p('form', 'builder.problem.noSteps');
  if (!def.steps.some((s) => s.type === 'FORM')) p('form', 'builder.problem.noForm');
  for (const type of ['LEVEL', 'FEES', 'REVIEW', 'DECLARATION'] as StepType[]) {
    if (def.steps.filter((s) => s.type === type).length > 1) p('form', 'builder.problem.oneOf', { type });
  }
  const stepCodes = new Set<string>();
  const sectionCodes = new Set<string>();
  def.steps.forEach((step, i) => {
    if (!CODE.test(step.code)) p('form', 'builder.problem.code', { code: step.code }, { step: i });
    if (stepCodes.has(step.code)) p('form', 'builder.problem.duplicate', { code: step.code }, { step: i });
    stepCodes.add(step.code);
    if (!step.titleAr?.trim() || !step.titleEn?.trim()) p('form', 'builder.problem.titles', { code: step.code }, { step: i });
    if (step.type === 'FORM' && !step.sections.length) p('form', 'builder.problem.noSections', { code: step.code }, { step: i });
    if (step.type === 'DECLARATION' && (!step.textAr?.trim() || !step.textEn?.trim())) p('form', 'builder.problem.declaration', {}, { step: i });
    if (step.type === 'LEVEL' && !def.levels.length) p('rules', 'builder.problem.levelStepWithoutLevels');
    step.sections.forEach((section, j) => {
      if (!CODE.test(section.code)) p('form', 'builder.problem.code', { code: section.code }, { step: i, section: j });
      if (sectionCodes.has(section.code)) p('form', 'builder.problem.duplicate', { code: section.code }, { step: i, section: j });
      sectionCodes.add(section.code);
      if (!section.titleAr?.trim() || !section.titleEn?.trim()) p('form', 'builder.problem.titles', { code: section.code }, { step: i, section: j });
      if (!section.questions.length) p('form', 'builder.problem.emptySection', { code: section.code }, { step: i, section: j });
      section.questions.forEach((q, k) => {
        if (!bank.has(q.code)) p('form', 'builder.problem.unknownQuestion', { code: q.code }, { step: i, section: j, question: k });
      });
    });
  });
  const levelCodes = new Set<string>();
  def.levels.forEach((l) => {
    if (!CODE.test(l.code) || levelCodes.has(l.code)) p('rules', 'builder.problem.levelCode', { code: l.code });
    levelCodes.add(l.code);
    if (!l.nameAr?.trim() || !l.nameEn?.trim()) p('rules', 'builder.problem.levelNames', { code: l.code });
  });
  for (const [list, key] of [
    [def.eligibility, 'builder.problem.eligibility'],
    [def.validations, 'builder.problem.validation'],
  ] as const) {
    list.forEach((r) => {
      if (!CODE.test(r.code) || r.rule === null || r.rule === undefined || !r.messageAr?.trim() || !r.messageEn?.trim()) p('rules', key, { code: r.code });
    });
  }
  def.fees.forEach((f) => {
    const kinds = [f.fixed !== null && f.fixed !== undefined, f.formula !== null && f.formula !== undefined, !!f.bracket].filter(Boolean).length;
    if (!CODE.test(f.code) || !f.nameAr?.trim() || !f.nameEn?.trim() || kinds !== 1) p('rules', 'builder.problem.fee', { code: f.code });
    if (f.levels.some((l) => !levelCodes.has(l))) p('rules', 'builder.problem.feeLevel', { code: f.code });
  });
  def.card.steps.forEach((s, i) => {
    if (!s.textAr?.trim() || !s.textEn?.trim()) p('card', 'builder.problem.cardStep', { n: i + 1 });
  });
  def.card.requiredDocuments.forEach((d, i) => {
    if (!d.nameAr?.trim() || !d.nameEn?.trim()) p('card', 'builder.problem.cardDocument', { n: i + 1 });
  });
  return problems;
}

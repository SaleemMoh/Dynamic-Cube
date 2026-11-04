import {
  JsonLogicRule,
  QuestionType,
  QuestionUse,
  SectionDefinition,
  StepDefinition,
  VersionDefinition,
} from '@dynamic-cube/shared';
import {
  InputType,
  Matrix,
  MatrixInput,
  MatrixRow,
  bankQuestionCode,
  cellInputs,
  cellOf,
} from './matrix-model';

/**
 * The application form of the service whose workflow decides with the matrix, as the sector page edits it. A question
 * is shown to a sector's applicants when its "visible when" rule names the sector's value in the question that lists
 * the sectors applied for (`{"in": ["RE", {"var": "CLS_DOMAINS"}]}`, several joined with "or"); no rule shows it to all.
 */
export interface FormLink {
  /** The service and the version the form was read from (saving adds the next one). */
  service: string;
  serviceName: { ar: string; en: string };
  version: number;
  definition: VersionDefinition;
  /** Bank questions added on this page, created when saving (before the service version that asks them). */
  newQuestions: NewQuestion[];
}

/** A question added to the bank from the sector page. */
export interface NewQuestion {
  code: string;
  nameAr: string;
  nameEn: string;
  type: QuestionType;
  options: { value: string; labelAr: string; labelEn: string }[];
}

/** Which sectors a question is shown to: all (no rule), the listed ones, or a rule this page cannot read (custom). */
export type SectorVisibility =
  { kind: 'all' } | { kind: 'sectors'; sectors: string[] } | { kind: 'custom' };

/** Where a question is placed in the form. */
export interface Placement {
  step: number;
  section: number;
  index: number;
  use: QuestionUse;
}

/** The value a sector has in the question listing the sectors applied for. */
export function sectorValue(row: MatrixRow): string {
  return row.answer || row.code;
}

/** The bank question listing the sectors applied for (the question of the matrix's rows input). */
export function sectorsQuestion(m: Matrix): string | null {
  const input = m.inputs.find((i) => i.code === m.rowsInput);
  return input ? bankQuestionCode(input) : null;
}

function sectorClause(rule: JsonLogicRule, question: string): string | null {
  if (!rule || typeof rule !== 'object' || Array.isArray(rule)) return null;
  const args = (rule as Record<string, unknown>)['in'];
  if (!Array.isArray(args) || args.length !== 2 || typeof args[0] !== 'string') return null;
  const ref = args[1] as Record<string, unknown> | null;
  return ref && typeof ref === 'object' && ref['var'] === question ? args[0] : null;
}

export function visibility(
  rule: JsonLogicRule | null | undefined,
  question: string | null,
): SectorVisibility {
  if (rule === null || rule === undefined) return { kind: 'all' };
  if (!question) return { kind: 'custom' };
  const single = sectorClause(rule, question);
  if (single !== null) return { kind: 'sectors', sectors: [single] };
  const any =
    typeof rule === 'object' && !Array.isArray(rule)
      ? (rule as Record<string, unknown>)['or']
      : null;
  if (!Array.isArray(any)) return { kind: 'custom' };
  const sectors = any.map((r) => sectorClause(r as JsonLogicRule, question));
  return sectors.every((s): s is string => s !== null)
    ? { kind: 'sectors', sectors }
    : { kind: 'custom' };
}

/** Whether the question shows for the sector (a custom rule is taken as shown: it depends on other answers). */
export function shownFor(
  rule: JsonLogicRule | null | undefined,
  question: string | null,
  value: string,
): boolean {
  const v = visibility(rule, question);
  return v.kind !== 'sectors' || v.sectors.some((s) => same(s, value));
}

/** The rule showing a question to these sectors only. */
export function sectorsRule(question: string, sectors: string[]): JsonLogicRule {
  return { or: sectors.map((s) => ({ in: [s, { var: question }] })) } as JsonLogicRule;
}

/** Adds the sector to (or takes it off) the sectors a question shows for; "all" becomes every listed sector but this. */
export function withSector(
  rule: JsonLogicRule | null | undefined,
  question: string,
  value: string,
  on: boolean,
  allSectors: string[],
): JsonLogicRule | null | undefined {
  const v = visibility(rule, question);
  if (v.kind === 'custom') return rule;
  const current = v.kind === 'all' ? allSectors : v.sectors;
  const next = on
    ? current.some((s) => same(s, value))
      ? current
      : [...current, value]
    : current.filter((s) => !same(s, value));
  if (next.length >= allSectors.length && allSectors.every((s) => next.some((n) => same(n, s))))
    return null;
  return sectorsRule(question, next);
}

/** Every place a question is used in the form. */
export function placements(def: VersionDefinition, code: string): Placement[] {
  return def.steps.flatMap((st, step) =>
    st.sections.flatMap((sec, section) =>
      sec.questions.flatMap((use, index) =>
        same(use.code, code) ? [{ step, section, index, use }] : [],
      ),
    ),
  );
}

function mapUse(
  def: VersionDefinition,
  at: { step: number; section: number; index: number },
  change: (use: QuestionUse) => QuestionUse | null,
): VersionDefinition {
  return {
    ...def,
    steps: def.steps.map((st, s) =>
      s !== at.step
        ? st
        : {
            ...st,
            sections: st.sections.map((sec, c) => {
              if (c !== at.section) return sec;
              const questions = sec.questions
                .map((u, i) => (i === at.index ? change(u) : u))
                .filter((u): u is QuestionUse => u !== null);
              return { ...sec, questions };
            }),
          },
    ),
  };
}

/** Shows (or stops showing) a question to a sector; taken off its last sector, it leaves the form. */
export function showForSector(
  def: VersionDefinition,
  code: string,
  question: string,
  value: string,
  on: boolean,
  allSectors: string[],
): VersionDefinition {
  let next = def;
  for (const p of placements(def, code).reverse())
    next = mapUse(next, p, (use) => {
      const rule = withSector(use.visibleWhen, question, value, on, allSectors);
      const v = visibility(rule, question);
      return v.kind === 'sectors' && !v.sectors.length
        ? null
        : { ...use, visibleWhen: rule ?? null };
    });
  return next;
}

/** Places a question in a step for one sector (in the step's first section, made when the step has none). */
export function placeQuestion(
  def: VersionDefinition,
  stepCode: string,
  code: string,
  question: string | null,
  value: string,
  required = true,
): VersionDefinition {
  const step = def.steps.findIndex((s) => same(s.code, stepCode));
  if (step < 0) return def;
  const use: QuestionUse = {
    code,
    required,
    readonly: false,
    visibleWhen: question ? sectorsRule(question, [value]) : null,
  };
  const st = def.steps[step];
  const sections: SectionDefinition[] = st.sections.length
    ? st.sections.map((sec, i) => (i === 0 ? { ...sec, questions: [...sec.questions, use] } : sec))
    : [{ code: st.code, titleAr: st.titleAr, titleEn: st.titleEn, questions: [use] }];
  return { ...def, steps: def.steps.map((s, i) => (i === step ? { ...s, sections } : s)) };
}

export function setRequired(
  def: VersionDefinition,
  code: string,
  required: boolean,
): VersionDefinition {
  let next = def;
  for (const p of placements(def, code)) next = mapUse(next, p, (use) => ({ ...use, required }));
  return next;
}

/** Moves a question one place up or down in its section. */
export function moveQuestion(def: VersionDefinition, at: Placement, by: -1 | 1): VersionDefinition {
  const questions = def.steps[at.step].sections[at.section].questions;
  const to = at.index + by;
  if (to < 0 || to >= questions.length) return def;
  const copy = [...questions];
  [copy[at.index], copy[to]] = [copy[to], copy[at.index]];
  return {
    ...def,
    steps: def.steps.map((st, s) =>
      s !== at.step
        ? st
        : {
            ...st,
            sections: st.sections.map((sec, c) =>
              c === at.section ? { ...sec, questions: copy } : sec,
            ),
          },
    ),
  };
}

/** One step of the form as one sector's applicant sees it: the questions shown to the sector, in order. */
export interface SectorStep {
  index: number;
  step: StepDefinition;
  questions: (Placement & { sectorOnly: boolean; custom: boolean })[];
}

export function formForSector(
  def: VersionDefinition,
  question: string | null,
  value: string,
): SectorStep[] {
  return def.steps.map((step, index) => ({
    index,
    step,
    questions: step.sections.flatMap((sec, section) =>
      sec.questions.flatMap((use, i) => {
        const v = visibility(use.visibleWhen, question);
        if (v.kind === 'sectors' && !v.sectors.some((s) => same(s, value))) return [];
        return [
          {
            step: index,
            section,
            index: i,
            use,
            sectorOnly: v.kind === 'sectors',
            custom: v.kind === 'custom',
          },
        ];
      }),
    ),
  }));
}

/** The bank questions the sector's tracks read, with the tracks (and criteria) reading each. */
export function sectorQuestions(
  m: Matrix,
  row: string,
): { code: string; input: MatrixInput; tracks: string[] }[] {
  const found = new Map<string, { code: string; input: MatrixInput; tracks: string[] }>();
  for (const column of m.columns) {
    for (const input of cellInputs(m, cellOf(m, row, column.code))) {
      if (input.source.kind !== 'applicant') continue;
      const code = bankQuestionCode(input)!;
      const entry = found.get(code.toLowerCase()) ?? { code, input, tracks: [] };
      if (!entry.tracks.includes(column.code)) entry.tracks.push(column.code);
      found.set(code.toLowerCase(), entry);
    }
  }
  return [...found.values()];
}

/** The form step a track's questions belong in: the step named like the track, else the one holding most of them. */
export function stepForTrack(
  def: VersionDefinition,
  track: string,
  questions: string[],
): string | null {
  const forms = def.steps.filter((s) => s.type === 'FORM');
  const named = forms.find((s) => same(s.code, track));
  if (named) return named.code;
  let best: { code: string; count: number } | null = null;
  for (const s of forms) {
    const count = s.sections
      .flatMap((sec) => sec.questions)
      .filter((u) => questions.some((q) => same(q, u.code))).length;
    if (count && (!best || count > best.count)) best = { code: s.code, count };
  }
  return best?.code ?? forms[0]?.code ?? null;
}

/** The matrix input type a bank question type gives. */
export function inputTypeOf(type: QuestionType): InputType {
  switch (type) {
    case 'NUMBER':
      return 'number';
    case 'CHECKBOX':
      return 'boolean';
    case 'DATE':
      return 'date';
    case 'SELECT':
      return 'option';
    default:
      return 'text';
  }
}

/** The matrix input reading a bank question the applicant answers. */
export function applicantInput(q: NewQuestion): MatrixInput {
  return {
    code: q.code,
    nameAr: q.nameAr,
    nameEn: q.nameEn,
    type: inputTypeOf(q.type),
    source: { kind: 'applicant', question: q.code },
  };
}

/** The bank question as the question bank stores it. */
export function questionPayload(q: NewQuestion): Record<string, unknown> {
  return {
    nameAr: q.nameAr,
    nameEn: q.nameEn,
    type: q.type,
    ...(q.type === 'SELECT' ? { options: q.options } : {}),
    ...(q.type === 'NUMBER' ? { validation: { min: 0 } } : {}),
  };
}

/** Answers typed one per line as "value | Arabic label | English label" (labels default to the value). */
export function parseOptions(text: string): NewQuestion['options'] {
  return text
    .split('\n')
    .map((line) => line.split('|').map((p) => p.trim()))
    .filter((parts) => parts[0])
    .map(([value, ar, en]) => ({ value, labelAr: ar || value, labelEn: en || ar || value }));
}

function same(a: string | null | undefined, b: string | null | undefined): boolean {
  return (a ?? '').toLowerCase() === (b ?? '').toLowerCase();
}

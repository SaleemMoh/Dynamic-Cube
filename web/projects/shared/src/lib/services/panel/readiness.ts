import { JsonValue, jsonLogic } from '../../expressions/jsonlogic';
import { LocalizedText } from '../../i18n/language';
import { IntegrationStepRun, QuestionSnapshot, ResolvedVersion } from '../service.models';

/**
 * complete: done; missing: required and not done; conditional: required only for some answers (and not given);
 * optional: worth listing (an optional upload or call) but not counted.
 */
export type ReadinessStatus = 'complete' | 'missing' | 'conditional' | 'optional';

export interface ReadinessItem {
  key: string;
  kind: 'question' | 'file' | 'integration' | 'level';
  status: ReadinessStatus;
  label: LocalizedText;
  /** The wizard step that completes it. */
  step: string;
  /** The question, for question and file items. */
  question?: string;
}

export interface ReadinessGroup {
  step: string;
  title: LocalizedText;
  items: ReadinessItem[];
}

export interface Readiness {
  /** The share of the counted requirements (complete and missing) that are complete; 100 when there are none. */
  percent: number;
  complete: number;
  missing: number;
  conditional: number;
  items: ReadinessItem[];
  /** Questions shown for these answers (each once) and how many are answered. */
  asked: number;
  answered: number;
  /** Uploaded files across the answers. */
  files: number;
}

export interface ReadinessInput {
  version: ResolvedVersion;
  data: Record<string, unknown>;
  levelCode: string | null;
  /** The last call of each integration step. */
  integrations?: Record<string, IntegrationStepRun> | null;
}

export const EMPTY_READINESS: Readiness = {
  percent: 100,
  complete: 0,
  missing: 0,
  conditional: 0,
  items: [],
  asked: 0,
  answered: 0,
  files: 0,
};

/** Whether an answer is empty: nothing, blank text, an empty list or an unticked box. */
export function isEmptyAnswer(value: unknown): boolean {
  return (
    value === null ||
    value === undefined ||
    value === false ||
    (typeof value === 'string' && value.trim() === '') ||
    (Array.isArray(value) && value.length === 0)
  );
}

/**
 * The readiness of a request: every requirement of the form (questions and uploads as they show for these answers, required
 * integration calls, the service level), each complete, missing, conditional or optional. It reads the same rules the
 * wizard and the server apply (visibility, required and required-when, a question asked where it first shows), so it is not
 * a second opinion. It measures how complete the request is, not its result.
 */
export function computeReadiness(input: ReadinessInput): Readiness {
  const { version, data } = input;
  const context = data as JsonValue;
  const holds = (rule: unknown) => jsonLogic.evaluateTruthy(rule as JsonValue, context);
  const items: ReadinessItem[] = [];
  const asked = new Set<string>();
  let answered = 0;
  let files = 0;

  for (const step of version.definition.steps) {
    if (step.type === 'FORM') {
      for (const section of step.sections) {
        if (section.visibleWhen && !holds(section.visibleWhen)) continue;
        for (const use of section.questions) {
          const question = version.questions[use.code];
          if (!question || asked.has(use.code)) continue;
          if (use.visibleWhen && !holds(use.visibleWhen)) continue;
          asked.add(use.code);
          const value = data[use.code];
          const empty = isEmptyAnswer(value);
          if (!empty) answered++;
          if (question.type === 'FILE' && Array.isArray(value)) files += value.length;
          const requiredNow = use.required || (!!use.requiredWhen && holds(use.requiredWhen));
          const status: ReadinessStatus | null = requiredNow
            ? empty || violatesPattern(question, value)
              ? 'missing'
              : 'complete'
            : use.requiredWhen
              ? empty
                ? 'conditional'
                : 'complete'
              : question.type === 'FILE'
                ? empty
                  ? 'optional'
                  : 'complete'
                : null;
          if (status) {
            items.push({
              key: `q:${use.code}`,
              kind: question.type === 'FILE' ? 'file' : 'question',
              status,
              label: { ar: question.labelAr, en: question.labelEn },
              step: step.code,
              question: use.code,
            });
          }
        }
      }
    } else if (step.type === 'INTEGRATION' && step.integration) {
      const done = input.integrations?.[step.code]?.success === true;
      items.push({
        key: `i:${step.code}`,
        kind: 'integration',
        status: done ? 'complete' : step.integration.required ? 'missing' : 'optional',
        label: { ar: step.titleAr, en: step.titleEn },
        step: step.code,
      });
    } else if (step.type === 'LEVEL' && version.definition.levels.length) {
      items.push({
        key: `l:${step.code}`,
        kind: 'level',
        status: input.levelCode ? 'complete' : 'missing',
        label: { ar: step.titleAr, en: step.titleEn },
        step: step.code,
      });
    }
  }

  const complete = items.filter((i) => i.status === 'complete').length;
  const missing = items.filter((i) => i.status === 'missing').length;
  const conditional = items.filter((i) => i.status === 'conditional').length;
  const counted = complete + missing;
  return {
    percent: counted === 0 ? 100 : Math.round((complete / counted) * 100),
    complete,
    missing,
    conditional,
    items,
    asked: asked.size,
    answered,
    files,
  };
}

/** The items still open (missing first, then conditional), grouped by the step that completes them, in step order. */
export function openGroups(version: ResolvedVersion, readiness: Readiness): ReadinessGroup[] {
  const order: ReadinessStatus[] = ['missing', 'conditional'];
  const groups: ReadinessGroup[] = [];
  for (const step of version.definition.steps) {
    const items = readiness.items
      .filter((i) => i.step === step.code && order.includes(i.status))
      .sort((a, b) => order.indexOf(a.status) - order.indexOf(b.status));
    if (items.length)
      groups.push({ step: step.code, title: { ar: step.titleAr, en: step.titleEn }, items });
  }
  return groups;
}

function violatesPattern(question: QuestionSnapshot, value: unknown): boolean {
  const pattern = question.validation.pattern;
  if (!pattern || typeof value !== 'string') return false;
  try {
    return !new RegExp(`^(?:${pattern})$`).test(value);
  } catch {
    return false;
  }
}

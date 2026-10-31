import { JsonValue, jsonLogic } from '../../expressions/jsonlogic';
import { LocalizedText } from '../../i18n/language';
import { FieldError, ResolvedVersion } from '../service.models';
import {
  AssistantLineDefinition,
  AssistantLineKind,
  AssistantPanelDefinition,
} from './panel.models';
import { Readiness } from './readiness';

/** The tone of a note: the configured kinds plus the problems found by the server. */
export type NoteTone = Exclude<AssistantLineKind, 'MESSAGE' | 'QUESTION'>;

export interface AssistantNote {
  key: string;
  tone: NoteTone;
  text: LocalizedText;
  why: LocalizedText | null;
}

export interface AssistantQuestion {
  key: string;
  question: LocalizedText;
  answer: LocalizedText;
}

export interface AssistantView {
  /** The greeting of the step. */
  message: LocalizedText | null;
  notes: AssistantNote[];
  questions: AssistantQuestion[];
}

/** What the assistant knows: the answers on screen, the step shown, the readiness and the live result. */
export interface AssistantContext {
  version: ResolvedVersion;
  data: Record<string, unknown>;
  levelCode: string | null;
  step: string | null;
  readiness: Readiness;
  /** The live result's outputs, when the service has one. */
  result: Record<string, unknown> | null;
  service: LocalizedText;
  /** Problems the server reported for the request. */
  errors?: FieldError[];
}

/** The data a line's condition is evaluated on. */
export function assistantData(context: AssistantContext): Record<string, unknown> {
  return {
    data: context.data,
    level: context.levelCode,
    step: context.step,
    readiness: context.readiness.percent,
    missing: context.readiness.missing,
    result: context.result ?? {},
  };
}

/** Lines shown now: on this step (or every step) and whose condition holds; a condition that fails to evaluate hides its line. */
export function visibleLines(
  definition: AssistantPanelDefinition,
  context: AssistantContext,
): AssistantLineDefinition[] {
  const data = assistantData(context) as JsonValue;
  return definition.lines.filter((line) => {
    if (line.step && line.step !== context.step) return false;
    if (line.when === null || line.when === undefined) return true;
    try {
      return jsonLogic.evaluateTruthy(line.when, data);
    } catch {
      return false;
    }
  });
}

/** Replaces {missing}, {readiness} and {service} in a configured text. */
export function fillWords(text: string, context: AssistantContext, language: 'ar' | 'en'): string {
  return text
    .replaceAll('{missing}', String(context.readiness.missing))
    .replaceAll('{readiness}', String(context.readiness.percent))
    .replaceAll(
      '{service}',
      language === 'ar'
        ? context.service.ar || context.service.en
        : context.service.en || context.service.ar,
    );
}

const filled = (
  ar: string | null | undefined,
  en: string | null | undefined,
  context: AssistantContext,
): LocalizedText => ({
  ar: fillWords(ar ?? '', context, 'ar'),
  en: fillWords(en ?? '', context, 'en'),
});

const joined = (labels: LocalizedText[]): LocalizedText => ({
  ar: labels.map((l) => l.ar).join('، '),
  en: labels.map((l) => l.en || l.ar).join(', '),
});

/**
 * What the assistant says on this step: the greeting, the configured notes and, when on, the notes worked out from the
 * form (what the step still lacks, what may become required, its uploads, the problems found), and the questions offered.
 * Texts of the worked-out notes come from the `texts` given (translated by the caller), with {items} replaced.
 */
export function assistantView(
  definition: AssistantPanelDefinition,
  context: AssistantContext,
  texts: AutoNoteTexts,
): AssistantView {
  const lines = visibleLines(definition, context);
  const greeting = lines.find((l) => l.kind === 'MESSAGE');
  const notes: AssistantNote[] = lines
    .map((line, index) => ({ line, index }))
    .filter(({ line }) => line.kind !== 'MESSAGE' && line.kind !== 'QUESTION')
    .map(({ line, index }) => ({
      key: `line:${index}:${line.kind}`,
      tone: line.kind as NoteTone,
      text: filled(line.textAr, line.textEn, context),
      why: line.whyAr || line.whyEn ? filled(line.whyAr, line.whyEn, context) : null,
    }));
  if (definition.autoNotes) notes.push(...autoNotes(context, texts));
  const questions = lines
    .map((line, index) => ({ line, index }))
    .filter(({ line }) => line.kind === 'QUESTION' && (line.questionAr || line.questionEn))
    .map(({ line, index }) => ({
      key: `q:${index}`,
      question: filled(line.questionAr, line.questionEn, context),
      answer: filled(line.textAr, line.textEn, context),
    }));
  return {
    message: greeting ? filled(greeting.textAr, greeting.textEn, context) : null,
    notes,
    questions,
  };
}

/** Texts of the worked-out notes in both languages; {items} is replaced by the names. */
export interface AutoNoteTexts {
  missing: LocalizedText;
  conditional: LocalizedText;
  uploads: LocalizedText;
  problems: LocalizedText;
  ready: LocalizedText;
}

const withItems = (text: LocalizedText, items: LocalizedText): LocalizedText => ({
  ar: text.ar.replace('{items}', items.ar),
  en: text.en.replace('{items}', items.en),
});

/** Notes worked out from the form for the step shown. */
export function autoNotes(context: AssistantContext, texts: AutoNoteTexts): AssistantNote[] {
  const notes: AssistantNote[] = [];
  const here = context.readiness.items.filter((i) => i.step === context.step);
  const missing = here.filter((i) => i.status === 'missing');
  if (missing.length)
    notes.push({
      key: 'auto:missing',
      tone: 'MANDATORY',
      text: withItems(texts.missing, joined(missing.map((i) => i.label))),
      why: null,
    });
  const conditional = here.filter((i) => i.status === 'conditional');
  if (conditional.length)
    notes.push({
      key: 'auto:conditional',
      tone: 'CONDITIONAL',
      text: withItems(texts.conditional, joined(conditional.map((i) => i.label))),
      why: null,
    });
  const uploads = here.filter((i) => i.kind === 'file' && i.status === 'optional');
  if (uploads.length)
    notes.push({
      key: 'auto:uploads',
      tone: 'ADVICE',
      text: withItems(texts.uploads, joined(uploads.map((i) => i.label))),
      why: null,
    });
  const problems = (context.errors ?? []).filter((e) => !e.step || e.step === context.step);
  if (problems.length) {
    notes.push({
      key: 'auto:problems',
      tone: 'MANDATORY',
      text: withItems(
        texts.problems,
        joined(problems.map((e) => ({ ar: e.messageAr, en: e.messageEn }))),
      ),
      why: null,
    });
  }
  if (context.readiness.items.length && context.readiness.missing === 0)
    notes.push({ key: 'auto:ready', tone: 'ADVICE', text: texts.ready, why: null });
  return notes;
}

const ARABIC_MARKS = /[ً-ْٰـ]/g;
const STOP_WORDS = new Set([
  'في',
  'من',
  'على',
  'الى',
  'إلى',
  'عن',
  'ما',
  'ماذا',
  'هل',
  'كيف',
  'هو',
  'هي',
  'ان',
  'أن',
  'إن',
  'او',
  'أو',
  'لا',
  'مع',
  'هذا',
  'هذه',
  'ذلك',
  'التي',
  'الذي',
  'كم',
  'متى',
  'لماذا',
  'اي',
  'أي',
  'عند',
  'لي',
  'به',
  'بها',
  'the',
  'a',
  'an',
  'is',
  'are',
  'was',
  'be',
  'of',
  'to',
  'in',
  'on',
  'for',
  'and',
  'or',
  'what',
  'how',
  'why',
  'when',
  'which',
  'do',
  'does',
  'did',
  'i',
  'my',
  'me',
  'can',
  'it',
  'this',
  'that',
  'with',
  'by',
  'at',
  'as',
  'we',
  'you',
  'your',
]);

/** The words of a text for matching: lower case, Arabic marks and letter forms unified, the article dropped, stop words out. */
export function words(text: string): Set<string> {
  const result = new Set<string>();
  const normalized = text
    .toLowerCase()
    .replace(ARABIC_MARKS, '')
    .replace(/[أإآ]/g, 'ا')
    .replace(/ى/g, 'ي')
    .replace(/ة/g, 'ه');
  for (const raw of normalized.split(/[^\p{L}\p{N}]+/u)) {
    if (raw.length < 2 || STOP_WORDS.has(raw)) continue;
    const word =
      /^(ال|بال|وال|لل)/.test(raw) && raw.length > 4 ? raw.replace(/^(بال|وال|ال|لل)/, '') : raw;
    if (word.length >= 2 && !STOP_WORDS.has(word)) result.add(word);
  }
  return result;
}

/**
 * The offered question that best answers a typed one: each word shared with the question weighs 3, with the answer 1;
 * the best scoring at least 3 (one word of the question, or three of the answer) wins.
 */
export function matchQuestion(
  typed: string,
  questions: AssistantQuestion[],
  language: 'ar' | 'en',
): AssistantQuestion | null {
  const asked = words(typed);
  if (!asked.size) return null;
  let best: AssistantQuestion | null = null;
  let bestScore = 0;
  for (const q of questions) {
    const questionWords = new Set([...words(q.question.ar), ...words(q.question.en)]);
    const answerWords = words(language === 'ar' ? q.answer.ar : q.answer.en);
    let score = 0;
    for (const w of asked) {
      if (questionWords.has(w)) score += 3;
      else if (answerWords.has(w)) score += 1;
    }
    if (score > bestScore) {
      best = q;
      bestScore = score;
    }
  }
  return bestScore >= 3 ? best : null;
}

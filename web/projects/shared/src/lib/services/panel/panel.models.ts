import { JsonLogicRule } from '../../expressions/jsonlogic';

/**
 * The panel beside the applicant's form (ADR-0040): an assistant, the readiness check, the request summary and the live
 * result. Every part is optional and configured on the service version in the Service Builder.
 */
export interface PanelDefinition {
  assistant: AssistantPanelDefinition;
  readiness: ReadinessPanelDefinition;
  summary: SummaryPanelDefinition;
  result: ResultPanelDefinition;
}

export interface AssistantPanelDefinition {
  enabled: boolean;
  /** e.g. «وكيل التصنيف». */
  titleAr?: string | null;
  titleEn?: string | null;
  subtitleAr?: string | null;
  subtitleEn?: string | null;
  /** Add the notes worked out from the form: what the step still lacks, its uploads and the problems found. */
  autoNotes: boolean;
  lines: AssistantLineDefinition[];
}

/** MESSAGE: the step's greeting; MANDATORY, CONDITIONAL, IMPACT and ADVICE: notes; QUESTION: a question with its answer. */
export type AssistantLineKind =
  'MESSAGE' | 'MANDATORY' | 'CONDITIONAL' | 'IMPACT' | 'ADVICE' | 'QUESTION';

export const ASSISTANT_LINE_KINDS: readonly AssistantLineKind[] = [
  'MESSAGE',
  'MANDATORY',
  'CONDITIONAL',
  'IMPACT',
  'ADVICE',
  'QUESTION',
];

/** A line of the assistant. Texts may use {missing}, {readiness} and {service}. */
export interface AssistantLineDefinition {
  /** The step it is shown on; empty: every step. */
  step?: string | null;
  kind: AssistantLineKind;
  textAr: string;
  textEn: string;
  /** A note's rule, shown under «لماذا؟». */
  whyAr?: string | null;
  whyEn?: string | null;
  questionAr?: string | null;
  questionEn?: string | null;
  /** JSONLogic over data, level, step, readiness, missing and result; empty: always. */
  when?: JsonLogicRule | null;
}

export interface ReadinessPanelDefinition {
  enabled: boolean;
}

export interface SummaryPanelDefinition {
  enabled: boolean;
  /** Questions whose answers the summary also shows. */
  questions: string[];
}

export interface ResultPanelDefinition {
  enabled: boolean;
  titleAr?: string | null;
  titleEn?: string | null;
  /** A decision step of the service's workflow: its rule set and input are used. */
  step?: string | null;
  headline?: ResultValueDefinition | null;
  /** The values a result may take, best first, with their names. */
  scale: ResultScaleValue[];
  unknownAr?: string | null;
  unknownEn?: string | null;
  values: ResultValueDefinition[];
  items?: ResultItemsDefinition | null;
}

/** An output of the rule set shown with a label; outputEn names the output holding the English text, when different. */
export interface ResultValueDefinition {
  output: string;
  outputEn?: string | null;
  labelAr: string;
  labelEn: string;
  /** The value is one of the scale's. */
  onScale: boolean;
}

export interface ResultScaleValue {
  value: string;
  labelAr: string;
  labelEn: string;
}

/** A list output (e.g. items, one per sector): a card per element with its title, result on the scale and columns. */
export interface ResultItemsDefinition {
  output: string;
  titleOutput: string;
  titleOutputEn?: string | null;
  resultOutput?: string | null;
  columns: ResultValueDefinition[];
}

/** The decision step evaluated on the answers on screen (not recorded). */
export interface LiveResultView {
  step: string;
  ruleSetCode: string;
  version: number;
  output: Record<string, unknown>;
}

/** A decision step of a service's workflow (for the builder's picker). */
export interface DecisionStepInfo {
  code: string;
  nameAr: string | null;
  nameEn: string | null;
  ruleSet: string | null;
}

/** A panel with every part off, for a version that has none yet. */
export function emptyPanel(): PanelDefinition {
  return {
    assistant: {
      enabled: false,
      titleAr: null,
      titleEn: null,
      subtitleAr: null,
      subtitleEn: null,
      autoNotes: true,
      lines: [],
    },
    readiness: { enabled: false },
    summary: { enabled: false, questions: [] },
    result: {
      enabled: false,
      titleAr: null,
      titleEn: null,
      step: null,
      headline: null,
      scale: [],
      unknownAr: null,
      unknownEn: null,
      values: [],
      items: null,
    },
  };
}

/** Whether any part of the panel is on. */
export function hasPanel(panel: PanelDefinition | null | undefined): panel is PanelDefinition {
  return (
    !!panel &&
    (panel.assistant?.enabled ||
      panel.readiness?.enabled ||
      panel.summary?.enabled ||
      panel.result?.enabled)
  );
}

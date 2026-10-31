import { signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { LocalizedText } from '../../i18n/language';
import { requestErrors } from '../service-form';
import {
  LiveResultView,
  ResultPanelDefinition,
  ResultScaleValue,
  ResultValueDefinition,
} from './panel.models';

/** A value of the live result as shown: its text and, for a value on the scale, its place (0 = best). */
export interface ShownValue {
  label: LocalizedText;
  text: LocalizedText;
  /** Place on the scale, or -1 when the value is not on it (or empty). */
  place: number;
  value: unknown;
}

/** Whether a result value is empty. */
export function isEmptyValue(value: unknown): boolean {
  return (
    value === null ||
    value === undefined ||
    value === '' ||
    (Array.isArray(value) && value.length === 0)
  );
}

/** The place of a value on the scale (compared as text: 1 and "1" are the same), or -1. */
export function scalePlace(scale: ResultScaleValue[], value: unknown): number {
  if (isEmptyValue(value)) return -1;
  const text = String(value).trim();
  const number = Number(text);
  return scale.findIndex(
    (s) =>
      s.value === text ||
      (text !== '' &&
        Number.isFinite(number) &&
        s.value.trim() !== '' &&
        Number(s.value) === number),
  );
}

/** A plain value as text: numbers with up to three decimals, yes/no for true/false, lists joined. */
export function formatValue(value: unknown, language: 'ar' | 'en'): string {
  if (isEmptyValue(value)) return '—';
  if (typeof value === 'number')
    return Number.isInteger(value) ? String(value) : String(Math.round(value * 1000) / 1000);
  if (typeof value === 'boolean')
    return value ? (language === 'ar' ? 'نعم' : 'Yes') : language === 'ar' ? 'لا' : 'No';
  if (Array.isArray(value))
    return value.map((v) => formatValue(v, language)).join(language === 'ar' ? '، ' : ', ');
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

/** One configured value read from an output object (the result, or an element of its items). */
export function shownValue(
  definition: ResultValueDefinition,
  source: Record<string, unknown> | null | undefined,
  panel: ResultPanelDefinition,
): ShownValue {
  const value = source?.[definition.output];
  const english = definition.outputEn ? source?.[definition.outputEn] : undefined;
  const label = { ar: definition.labelAr, en: definition.labelEn };
  const unknown = { ar: panel.unknownAr || '—', en: panel.unknownEn || panel.unknownAr || '—' };
  if (definition.onScale) {
    const place = scalePlace(panel.scale, value);
    // Empty, or off the scale (e.g. a grade 6 meaning "not classified"): the configured name of an empty result.
    if (place < 0)
      return {
        label,
        text:
          isEmptyValue(value) || panel.unknownAr
            ? unknown
            : { ar: formatValue(value, 'ar'), en: formatValue(value, 'en') },
        place,
        value,
      };
    const step = panel.scale[place];
    return {
      label,
      text: { ar: step.labelAr || step.value, en: step.labelEn || step.labelAr || step.value },
      place,
      value,
    };
  }
  return {
    label,
    text: {
      ar: formatValue(value, 'ar'),
      en: formatValue(isEmptyValue(english) ? value : english, 'en'),
    },
    place: -1,
    value,
  };
}

export interface ShownItem {
  title: LocalizedText;
  result: ShownValue | null;
  columns: ShownValue[];
}

/** The elements of the configured list output, each with its title, its result on the scale and its columns. */
export function shownItems(
  panel: ResultPanelDefinition,
  output: Record<string, unknown> | null | undefined,
): ShownItem[] {
  const items = panel.items;
  if (!items || !output) return [];
  const list = output[items.output || 'items'];
  if (!Array.isArray(list)) return [];
  return list
    .filter(
      (element): element is Record<string, unknown> =>
        !!element && typeof element === 'object' && !Array.isArray(element),
    )
    .map((element) => {
      const title = element[items.titleOutput];
      const titleEn = items.titleOutputEn ? element[items.titleOutputEn] : undefined;
      return {
        title: {
          ar: formatValue(title, 'ar'),
          en: formatValue(isEmptyValue(titleEn) ? title : titleEn, 'en'),
        },
        result: items.resultOutput
          ? shownValue(
              {
                output: items.resultOutput,
                labelAr: '',
                labelEn: '',
                onScale: panel.scale.length > 0,
              },
              element,
              panel,
            )
          : null,
        columns: items.columns.map((c) => shownValue(c, element, panel)),
      };
    });
}

/** The headline as text (for the summary), or null when the result panel has none. */
export function headlineText(
  panel: ResultPanelDefinition | null | undefined,
  view: LiveResultView | null,
): ShownValue | null {
  if (!panel?.enabled || !panel.headline || !view) return null;
  return shownValue(panel.headline, view.output, panel);
}

const LADDER = ['#067647', '#4ca30d', '#ca8504', '#dc6803', '#d92d20'];

/** The colour of a place on a scale of `count` values: green for the best to red for the worst. */
export function placeColor(place: number, count: number): string {
  if (place < 0 || count <= 0) return 'var(--dga-gray-400, #9da4ae)';
  if (count === 1) return LADDER[0];
  return LADDER[Math.round((place / (count - 1)) * (LADDER.length - 1))];
}

/**
 * Fetches the live result a moment after the answers change (the latest answers win: an earlier answer arriving late is
 * dropped), and holds the result, whether it is loading and the problem, if any.
 */
export class LiveResultFeed {
  readonly result = signal<LiveResultView | null>(null);
  readonly loading = signal(false);
  readonly error = signal<LocalizedText | null>(null);

  private timer: ReturnType<typeof setTimeout> | undefined;
  private ticket = 0;

  constructor(
    private readonly fetch: (
      data: Record<string, unknown> | null,
      levelCode: string | null,
    ) => Observable<LiveResultView>,
    private readonly delayMs = 450,
  ) {}

  /** The answers changed: fetch after the delay unless they change again first. */
  change(data: Record<string, unknown> | null, levelCode: string | null): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.now(data, levelCode), this.delayMs);
  }

  /** Fetch now. */
  async now(data: Record<string, unknown> | null, levelCode: string | null): Promise<void> {
    clearTimeout(this.timer);
    const ticket = ++this.ticket;
    this.loading.set(true);
    try {
      const result = await firstValueFrom(this.fetch(data, levelCode));
      if (ticket !== this.ticket) return;
      this.result.set(result);
      this.error.set(null);
    } catch (error) {
      if (ticket !== this.ticket) return;
      const first = requestErrors(error)[0];
      this.error.set(
        first
          ? { ar: first.messageAr, en: first.messageEn }
          : { ar: 'تعذر حساب النتيجة', en: 'The result could not be worked out' },
      );
    } finally {
      if (ticket === this.ticket) this.loading.set(false);
    }
  }

  /** Stops waiting and drops any answer still on its way. */
  stop(): void {
    clearTimeout(this.timer);
    this.ticket++;
    this.loading.set(false);
  }
}

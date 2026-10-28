/** Supported languages. Arabic is the primary language. */
export type Language = 'ar' | 'en';

export const LANGUAGES: readonly Language[] = ['ar', 'en'];

/** Bilingual text as stored in name_ar / name_en columns. */
export interface LocalizedText {
  ar: string;
  en: string;
}

/** Returns the text for the language, falling back to the other language when empty. */
export function localize(text: LocalizedText | null | undefined, language: Language): string {
  if (!text) {
    return '';
  }
  const primary = language === 'ar' ? text.ar : text.en;
  const fallback = language === 'ar' ? text.en : text.ar;
  return primary?.trim() ? primary : (fallback ?? '');
}

export const directionOf = (language: Language): 'rtl' | 'ltr' => (language === 'ar' ? 'rtl' : 'ltr');

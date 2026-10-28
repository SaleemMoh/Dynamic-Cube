import { Language } from './language';

export type TranslationDictionary = Record<Language, Record<string, string>>;

/** Texts used by shared components. Apps register their own dictionaries; UI texts become configurable in Apps Control. */
export const SHARED_TRANSLATIONS: TranslationDictionary = {
  ar: {
    'language.switch': 'English',
    'language.switch.aria': 'التبديل إلى الإنجليزية',
    'form.required': 'هذا الحقل مطلوب',
    'form.submit': 'إرسال',
  },
  en: {
    'language.switch': 'العربية',
    'language.switch.aria': 'Switch to Arabic',
    'form.required': 'This field is required',
    'form.submit': 'Submit',
  },
};

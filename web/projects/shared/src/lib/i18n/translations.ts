import { Language } from './language';

export type TranslationDictionary = Record<Language, Record<string, string>>;

/** Texts used by shared components. Apps register their own dictionaries; UI texts become configurable in Apps Control. */
export const SHARED_TRANSLATIONS: TranslationDictionary = {
  ar: {
    'language.switch': 'English',
    'language.switch.aria': 'التبديل إلى الإنجليزية',
    'form.required': 'هذا الحقل مطلوب',
    'form.submit': 'إرسال',
    'form.cancel': 'إلغاء',
    'form.invalidJson': 'صيغة JSON غير صحيحة',
    'auth.login': 'تسجيل الدخول',
    'auth.logout': 'تسجيل الخروج',
    'auth.signingIn': 'جارٍ تسجيل الدخول…',
    'auth.failed': 'تعذر تسجيل الدخول. حاول مرة أخرى.',
    'nav.menu': 'القائمة',
  },
  en: {
    'language.switch': 'العربية',
    'language.switch.aria': 'Switch to Arabic',
    'form.required': 'This field is required',
    'form.submit': 'Submit',
    'form.cancel': 'Cancel',
    'form.invalidJson': 'Invalid JSON',
    'auth.login': 'Sign in',
    'auth.logout': 'Sign out',
    'auth.signingIn': 'Signing in…',
    'auth.failed': 'Sign-in failed. Please try again.',
    'nav.menu': 'Menu',
  },
};

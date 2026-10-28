import { DOCUMENT } from '@angular/common';
import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { directionOf, Language, LocalizedText, localize } from './language';
import { SHARED_TRANSLATIONS, TranslationDictionary } from './translations';

const STORAGE_KEY = 'dc.language';

/**
 * Current language and direction. Keeps <html lang/dir> in sync so PrimeNG and CSS logical properties
 * flip between RTL (Arabic, default) and LTR (English).
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly document = inject(DOCUMENT);
  private readonly dictionaries = signal<TranslationDictionary[]>([SHARED_TRANSLATIONS]);

  readonly language = signal<Language>(this.readStoredLanguage());
  readonly direction = computed(() => directionOf(this.language()));

  constructor() {
    effect(() => {
      const language = this.language();
      const root = this.document.documentElement;
      root.lang = language;
      root.dir = directionOf(language);
      try {
        this.document.defaultView?.localStorage.setItem(STORAGE_KEY, language);
      } catch {
        // Storage can be unavailable (private mode); the language still applies for this session.
      }
    });
  }

  setLanguage(language: Language): void {
    this.language.set(language);
  }

  toggle(): void {
    this.language.update((current) => (current === 'ar' ? 'en' : 'ar'));
  }

  /** Adds an app- or feature-level dictionary. Later registrations override earlier keys. */
  register(dictionary: TranslationDictionary): void {
    this.dictionaries.update((list) => [...list, dictionary]);
  }

  /** Translates a key; returns the key itself when missing so gaps are visible. */
  translate(key: string): string {
    const language = this.language();
    const list = this.dictionaries();
    for (let i = list.length - 1; i >= 0; i--) {
      const value = list[i][language][key];
      if (value !== undefined) {
        return value;
      }
    }
    return key;
  }

  localize(text: LocalizedText | null | undefined): string {
    return localize(text, this.language());
  }

  private readStoredLanguage(): Language {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(STORAGE_KEY);
      return stored === 'en' ? 'en' : 'ar';
    } catch {
      return 'ar';
    }
  }
}

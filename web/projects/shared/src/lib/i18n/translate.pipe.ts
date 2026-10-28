import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocalizedText } from './language';
import { I18nService } from './i18n.service';

/** `{{ 'form.submit' | dcTranslate }}` — re-evaluates when the language signal changes. */
@Pipe({ name: 'dcTranslate', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(key: string): string {
    return this.i18n.translate(key);
  }
}

/** `{{ field.label | dcLocalize }}` for bilingual configuration values. */
@Pipe({ name: 'dcLocalize', pure: false })
export class LocalizePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(text: LocalizedText | null | undefined): string {
    return this.i18n.localize(text);
  }
}

import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { catchError, firstValueFrom, of } from 'rxjs';
import { RUNTIME_CONFIG } from '../config/runtime-config';
import { I18nService } from '../i18n/i18n.service';
import { AppsControlApi } from './apps-control.api';

/** Loads the UI texts configured in Apps Control for this app at start-up; built-in texts remain when none are set. */
export function provideUiTexts(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      const api = inject(AppsControlApi);
      const i18n = inject(I18nService);
      const texts = await firstValueFrom(api.uiTexts(inject(RUNTIME_CONFIG).app).pipe(catchError(() => of([]))));
      i18n.setOverrides({
        ar: Object.fromEntries(texts.map((t) => [t.key, t.textAr])),
        en: Object.fromEntries(texts.map((t) => [t.key, t.textEn])),
      });
    }),
  ]);
}

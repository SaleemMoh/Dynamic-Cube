import { EnvironmentProviders, inject, makeEnvironmentProviders } from '@angular/core';
import { map } from 'rxjs';
import { LOV_SOURCE, LovSource } from '../forms/lov-source';
import { AppsControlApi } from './apps-control.api';

/** Feeds LOV-backed form fields from the Apps Control API. */
export function provideLovSource(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: LOV_SOURCE,
      useFactory: (): LovSource => {
        const api = inject(AppsControlApi);
        return {
          options: (lov, parent) =>
            api.lovValues(lov, { parent }).pipe(map((page) => page.items.map((item) => ({ value: item.value, label: { ar: item.labelAr, en: item.labelEn } })))),
        };
      },
    },
  ]);
}

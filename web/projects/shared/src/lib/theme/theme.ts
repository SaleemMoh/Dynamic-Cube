import { Directionality } from '@angular/cdk/bidi';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideNativeDateAdapter } from '@angular/material/core';
import {
  MAT_FORM_FIELD_DEFAULT_OPTIONS,
  MatFormFieldDefaultOptions,
} from '@angular/material/form-field';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { AppDirectionality } from '../i18n/app-directionality';
import { DcPaginatorIntl } from '../i18n/paginator-intl';

/**
 * UI providers shared by all Dynamic Cube apps. Colors and typography come from the Material 3 theme in
 * `projects/shared/src/styles/dc-theme.scss`; entities rebrand by changing the palette there.
 */
export function provideDynamicCubeUi(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideNativeDateAdapter(),
    { provide: Directionality, useClass: AppDirectionality },
    { provide: MatPaginatorIntl, useClass: DcPaginatorIntl },
    {
      provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
      useValue: {
        appearance: 'outline',
        subscriptSizing: 'dynamic',
      } satisfies MatFormFieldDefaultOptions,
    },
  ]);
}

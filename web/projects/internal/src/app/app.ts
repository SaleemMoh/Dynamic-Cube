import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { I18nService, TranslatePipe } from '@dynamic-cube/shared';
import { InternalShell } from './layout/internal-shell';
import { MODULE_TRANSLATIONS } from './module-translations';
import { PAYMENT_TRANSLATIONS } from './payments/payment-translations';
import { PHASE_1E_TRANSLATIONS } from './phase-1e-translations';
import { APP_TRANSLATIONS } from './translations';

@Component({
  selector: 'dc-root',
  imports: [RouterOutlet, InternalShell, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-internal-shell [title]="'app.title' | dcTranslate">
      <router-outlet />
    </dc-internal-shell>
  `,
})
export class App {
  constructor() {
    inject(I18nService).register(APP_TRANSLATIONS);
    inject(I18nService).register(MODULE_TRANSLATIONS);
    inject(I18nService).register(PHASE_1E_TRANSLATIONS);
    inject(I18nService).register(PAYMENT_TRANSLATIONS);
  }
}

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { I18nService, TranslatePipe } from '@dynamic-cube/shared';
import { InternalShell } from './layout/internal-shell';
import { NOTIFY_RULE_TRANSLATIONS } from './communication/notify-rule-translations';
import { CONTENT_TRANSLATIONS } from './content/content-translations';
import { MODULE_TRANSLATIONS } from './module-translations';
import { PAYMENT_TRANSLATIONS } from './payments/payment-translations';
import { PHASE_1E_TRANSLATIONS } from './phase-1e-translations';
import { BUILDER_TRANSLATIONS } from './services/builder/builder-translations';
import { APP_TRANSLATIONS } from './translations';
import { UX_TRANSLATIONS } from './ux-translations';
import { PACKAGE_TRANSLATIONS } from './apps-control/change-sets/package-translations';
import { DESIGNER_TRANSLATIONS } from './workflows/designer/designer-translations';

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
    inject(I18nService).register(DESIGNER_TRANSLATIONS);
    inject(I18nService).register(BUILDER_TRANSLATIONS);
    inject(I18nService).register(NOTIFY_RULE_TRANSLATIONS);
    inject(I18nService).register(CONTENT_TRANSLATIONS);
    inject(I18nService).register(UX_TRANSLATIONS);
    inject(I18nService).register(PACKAGE_TRANSLATIONS);
  }
}

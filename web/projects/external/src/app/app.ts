import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ActingContextSelector, AppShell, I18nService, TranslatePipe, UserMenu } from '@dynamic-cube/shared';
import { APP_TRANSLATIONS } from './translations';

@Component({
  selector: 'dc-root',
  imports: [RouterOutlet, ActingContextSelector, AppShell, TranslatePipe, UserMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-app-shell [title]="'app.title' | dcTranslate">
      <dc-acting-context-selector dcShellActions />
      <dc-user-menu dcShellActions />
      <router-outlet />
    </dc-app-shell>
  `,
})
export class App {
  constructor() {
    inject(I18nService).register(APP_TRANSLATIONS);
  }
}

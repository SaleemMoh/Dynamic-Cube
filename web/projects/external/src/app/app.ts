import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppShell, I18nService, TranslatePipe } from '@dynamic-cube/shared';
import { APP_TRANSLATIONS } from './translations';

@Component({
  selector: 'dc-root',
  imports: [RouterOutlet, AppShell, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dc-app-shell [title]="'app.title' | dcTranslate">
      <router-outlet />
    </dc-app-shell>
  `,
})
export class App {
  constructor() {
    inject(I18nService).register(APP_TRANSLATIONS);
  }
}

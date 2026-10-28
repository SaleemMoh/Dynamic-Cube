import { BidiModule } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { I18nService } from '../i18n/i18n.service';
import { LanguageSwitcher } from '../i18n/language-switcher';

/**
 * Common page frame: toolbar with app title and language switcher, content projected below.
 * The `dir` directive propagates the current direction to Material components and their overlays.
 */
@Component({
  selector: 'dc-app-shell',
  imports: [BidiModule, MatIconModule, MatToolbarModule, LanguageSwitcher],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-shell" [dir]="i18n.direction()">
      <mat-toolbar class="dc-shell__header">
        <mat-icon fontSet="material-symbols-outlined" aria-hidden="true">deployed_code</mat-icon>
        <span class="dc-shell__title">{{ title() }}</span>
        <span class="dc-shell__spacer"></span>
        <ng-content select="[dcShellActions]" />
        <dc-language-switcher />
      </mat-toolbar>
      <main class="dc-shell__main">
        <ng-content />
      </main>
    </div>
  `,
  styles: `
    .dc-shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
    .dc-shell__header {
      gap: 0.75rem;
      --mat-toolbar-container-background-color: var(--mat-sys-primary);
      --mat-toolbar-container-text-color: var(--mat-sys-on-primary);
    }
    .dc-shell__title {
      font-weight: 600;
      font-size: 1.125rem;
    }
    .dc-shell__spacer {
      flex: 1;
    }
    .dc-shell__main {
      flex: 1;
      padding: 1.5rem;
      max-width: 72rem;
      width: 100%;
      margin-inline: auto;
    }
  `,
})
export class AppShell {
  readonly title = input.required<string>();
  protected readonly i18n = inject(I18nService);
}

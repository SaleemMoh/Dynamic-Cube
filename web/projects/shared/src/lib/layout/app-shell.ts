import { BidiModule } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { BrandingService } from '../apps-control/branding';
import { I18nService } from '../i18n/i18n.service';
import { LanguageSwitcher } from '../i18n/language-switcher';
import { AppFooter } from './app-footer';
import { GovBar } from './gov-bar';

/**
 * Common page frame following the DGA design: government bar, white 72px header (brand, optional navigation,
 * actions, language), page content and the green footer. The `dir` directive propagates the current direction.
 */
@Component({
  selector: 'dc-app-shell',
  imports: [BidiModule, MatIconModule, LanguageSwitcher, GovBar, AppFooter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dc-shell" [dir]="i18n.direction()">
      <dc-gov-bar />
      <header class="dc-shell__header">
        <div class="dc-shell__header-inner" [class.dc-shell__header-inner--fluid]="fluid()">
          <a class="dc-shell__brand" href="/">
            @if (branding().logoUrl; as logo) {
              <img
                class="dc-shell__logo-img"
                [src]="logo"
                [alt]="i18n.localize({ ar: branding().nameAr, en: branding().nameEn })"
              />
            } @else {
              <span class="dc-shell__logo" aria-hidden="true">
                <mat-icon fontSet="material-symbols-outlined">deployed_code</mat-icon>
              </span>
            }
            <span class="dc-shell__title">{{ title() }}</span>
          </a>
          <nav class="dc-shell__nav">
            <ng-content select="[dcShellNav]" />
          </nav>
          <span class="dc-shell__spacer"></span>
          <div class="dc-shell__actions">
            <ng-content select="[dcShellActions]" />
            <dc-language-switcher />
          </div>
        </div>
      </header>
      <main class="dc-shell__main" [class.dc-shell__main--fluid]="fluid()">
        <ng-content />
      </main>
      <dc-app-footer
        [links]="branding().footerLinks"
        [entityName]="{ ar: branding().nameAr, en: branding().nameEn }"
      />
    </div>
  `,
  styles: `
    .dc-shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
    .dc-shell__header {
      background: #ffffff;
      border-bottom: 1px solid var(--dga-border);
    }
    .dc-shell__header-inner {
      display: flex;
      align-items: stretch;
      gap: 16px;
      min-height: 72px;
      max-width: 80rem;
      margin-inline: auto;
      padding-inline: 1.5rem;
    }
    .dc-shell__header-inner--fluid {
      max-width: none;
    }
    .dc-shell__brand {
      display: flex;
      align-items: center;
      gap: 12px;
      color: var(--dga-text);
      text-decoration: none;
    }
    .dc-shell__logo {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: 8px;
      background: var(--dga-green-700);
      color: #ffffff;
    }
    .dc-shell__logo-img {
      height: 40px;
      width: auto;
      max-width: 12rem;
      object-fit: contain;
    }
    .dc-shell__title {
      font-size: 18px;
      font-weight: 700;
      line-height: 28px;
    }
    .dc-shell__nav {
      display: flex;
      align-items: stretch;
      gap: 12px;
      margin-inline-start: 16px;
    }
    .dc-shell__spacer {
      flex: 1;
    }
    .dc-shell__actions {
      display: flex;
      align-items: stretch;
    }
    /* Header actions: full-height text buttons with a grey hover, as in the DGA nav header. */
    .dc-shell__actions ::ng-deep .mat-mdc-button {
      height: 72px;
      min-width: 72px;
      padding: 8px 16px;
      border-radius: 4px;
      --mat-button-text-label-text-color: var(--dga-text);
      --mat-button-text-state-layer-color: var(--dga-text);
    }
    .dc-shell__main {
      flex: 1;
      padding: 1.5rem;
      max-width: 80rem;
      width: 100%;
      margin-inline: auto;
    }
    .dc-shell__main--fluid {
      display: flex;
      padding: 0;
      max-width: none;
    }
    /* Phones: the brand keeps its logo only and the navigation moves to its own scrolling row. */
    @media (max-width: 48rem) {
      .dc-shell__header-inner {
        flex-wrap: wrap;
        gap: 0 8px;
        padding-inline: 1rem;
      }
      .dc-shell__title {
        display: none;
      }
      .dc-shell__nav {
        order: 3;
        width: 100%;
        min-height: 48px;
        margin-inline-start: 0;
        overflow-x: auto;
      }
      .dc-shell__actions ::ng-deep .mat-mdc-button {
        height: 56px;
        min-width: 48px;
        padding: 8px;
      }
      .dc-shell__main:not(.dc-shell__main--fluid) {
        padding: 1rem;
      }
    }
  `,
})
export class AppShell {
  readonly title = input.required<string>();
  /** Full-width content without padding (for layouts with their own side navigation). */
  readonly fluid = input(false);
  protected readonly i18n = inject(I18nService);
  protected readonly branding = inject(BrandingService).branding;
}

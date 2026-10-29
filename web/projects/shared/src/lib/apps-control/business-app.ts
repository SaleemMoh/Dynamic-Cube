import { ChangeDetectionStrategy, Component, Injectable, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { I18nService } from '../i18n/i18n.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { AppsControlApi } from './apps-control.api';
import { ConfigLevel, MyBusinessApp } from './apps-control.models';

const STORAGE_KEY = 'dc.businessApp';

/**
 * The business app the employee works in (e.g. Classification, Permits): lists of services, workflows, requests,
 * tasks, documents, parties and configuration show that app's rows and the shared ones. Null shows every app. Chosen
 * in the header and remembered in the browser.
 */
@Injectable({ providedIn: 'root' })
export class BusinessAppContext {
  private readonly api = inject(AppsControlApi);
  private loading: Promise<void> | null = null;

  readonly apps = signal<MyBusinessApp[]>([]);
  readonly current = signal<string | null>(BusinessAppContext.stored());
  readonly currentApp = computed(() => this.apps().find((a) => a.code === this.current()) ?? null);

  /** Loads the apps once (the selector does this after sign-in); a remembered app the user no longer has is dropped. */
  load(): Promise<void> {
    this.loading ??= firstValueFrom(this.api.myBusinessApps())
      .then((apps) => {
        this.apps.set(apps);
        if (this.current() && !apps.some((a) => a.code === this.current())) this.select(null);
      })
      .catch(() => {
        // Not signed in yet (or the server was unreachable): try again next time.
        this.apps.set([]);
        this.loading = null;
      });
    return this.loading;
  }

  select(code: string | null): void {
    this.current.set(code);
    try {
      if (code) localStorage.setItem(STORAGE_KEY, code);
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage may be unavailable; the choice then lasts for the page.
    }
  }

  /** The app's name, or its code when it is not one of the user's apps. */
  name(code: string): { ar: string; en: string } {
    const app = this.apps().find((a) => a.code === code);
    return app ? { ar: app.nameAr, en: app.nameEn } : { ar: code, en: code };
  }

  private static stored(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }
}

/** The level of a row from its entity and business app. */
export function configLevel(row: { entityCode?: unknown; businessApp?: unknown }): ConfigLevel {
  return row.businessApp ? 'APP' : row.entityCode ? 'ENTITY' : 'SYSTEM';
}

/** Header control: the business app the employee works in, or every app. Hidden when the installation has no business apps. */
@Component({
  selector: 'dc-business-app-selector',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (context.apps().length) {
      <button matButton class="dc-bapp" [matMenuTriggerFor]="menu" data-testid="business-app">
        <mat-icon fontSet="material-symbols-outlined">{{ context.currentApp()?.icon || 'apps' }}</mat-icon>
        @if (context.currentApp(); as app) {
          {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
        } @else {
          {{ 'businessApp.all' | dcTranslate }}
        }
        <mat-icon class="dc-bapp__chevron" fontSet="material-symbols-outlined" iconPositionEnd>expand_more</mat-icon>
      </button>
      <mat-menu #menu="matMenu">
        <button mat-menu-item (click)="context.select(null)" data-testid="business-app-all">
          <mat-icon fontSet="material-symbols-outlined">{{ context.current() ? 'apps' : 'check' }}</mat-icon>
          {{ 'businessApp.all' | dcTranslate }}
        </button>
        @for (app of context.apps(); track app.code) {
          <button mat-menu-item (click)="context.select(app.code)" [attr.data-testid]="'business-app-' + app.code">
            <mat-icon fontSet="material-symbols-outlined">{{ context.current() === app.code ? 'check' : app.icon || 'apps' }}</mat-icon>
            {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
          </button>
        }
      </mat-menu>
    }
  `,
  styles: `
    .dc-bapp {
      color: inherit;
      max-width: 16rem;
    }
  `,
})
export class BusinessAppSelector {
  protected readonly context = inject(BusinessAppContext);
  protected readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);

  constructor() {
    // The apps are the signed-in employee's: load them once signed in.
    effect(() => {
      if (this.auth.isAuthenticated()) void this.context.load();
    });
  }
}

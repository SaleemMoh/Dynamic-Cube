import { HttpInterceptorFn } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  Injectable,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { LocalizePipe, TranslatePipe } from '../i18n/translate.pipe';
import { AppsControlApi, COMPANY_HEADER } from './apps-control.api';
import { WorkspaceCompany } from './apps-control.models';
import { BusinessAppContext } from './business-app';

const STORAGE_KEY = 'dc.company';

/**
 * The company the employee works in (ADR-0036 step 5). Everyone works in one company: it is sent with every API call, and
 * Apps Control gives the menus, screens, actions and list values of that company. Chosen after sign-in when the employee
 * may work in several, changed from the header, and remembered (in the browser, and by the server for the next sign-in).
 */
@Injectable({ providedIn: 'root' })
export class CompanyContext {
  private readonly api = inject(AppsControlApi);
  private readonly apps = inject(BusinessAppContext);
  private loading: Promise<void> | null = null;
  private readonly chosen = signal(true);

  readonly companies = signal<WorkspaceCompany[]>([]);
  /** The company this browser works in; sent as the company header. */
  readonly current = signal<string | null>(CompanyContext.stored());
  readonly loaded = signal(false);
  readonly switching = signal(false);
  readonly currentCompany = computed(
    () => this.companies().find((c) => c.code === this.current()) ?? null,
  );
  /** Several companies and none chosen yet: the employee is asked to choose before working. */
  readonly needsChoice = computed(
    () => this.loaded() && this.companies().length > 1 && !this.chosen(),
  );
  /** Whether the companies belong to more than one organization (then they are grouped by it). */
  readonly severalOrganizations = computed(
    () => new Set(this.companies().map((c) => c.entityCode ?? '')).size > 1,
  );

  /** Loads the companies once signed in; the server checks the company this browser sends and answers with the one in use. */
  load(): Promise<void> {
    this.loading ??= firstValueFrom(this.api.myWorkspaces())
      .then((info) => {
        this.companies.set(info.companies);
        this.chosen.set(info.chosen);
        this.remember(info.chosen ? info.company : null);
        this.loaded.set(true);
      })
      .catch(() => {
        // Not signed in yet (or the server was unreachable): try again next time.
        this.loading = null;
      });
    return this.loading;
  }

  /**
   * Works in a company (and, when given, an app of it): remembered, then the page starts again so the menus, privileges and
   * lists are that company's.
   */
  async choose(company: string, app?: string | null): Promise<void> {
    this.switching.set(true);
    try {
      await firstValueFrom(this.api.chooseWorkspace(company));
      this.remember(company);
      if (app !== undefined) this.apps.select(app);
      window.location.assign('/');
    } catch {
      this.switching.set(false);
    }
  }

  name(company: WorkspaceCompany): { ar: string; en: string } {
    return { ar: company.nameAr, en: company.nameEn };
  }

  private remember(company: string | null): void {
    this.current.set(company);
    try {
      if (company) localStorage.setItem(STORAGE_KEY, company);
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage unavailable: the choice lasts for the page, and the server's last choice applies next time.
    }
  }

  private static stored(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }
}

/** Sends the company the employee works in with API calls (a call that names one itself keeps it). */
export const companyInterceptor: HttpInterceptorFn = (request, next) => {
  const company = inject(CompanyContext).current();
  return company && request.url.startsWith('/api/') && !request.headers.has(COMPANY_HEADER)
    ? next(request.clone({ setHeaders: { [COMPANY_HEADER]: company } }))
    : next(request);
};

/**
 * Header control: the company the employee works in (when they may work in several) and the app (when there are apps).
 * Changing the company starts the page again in it; changing the app filters the lists.
 */
@Component({
  selector: 'dc-workspace-switcher',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (several() || apps.apps().length) {
      <button
        matButton
        class="dc-ws"
        [matMenuTriggerFor]="menu"
        [disabled]="companies.switching()"
        data-testid="workspace"
      >
        <mat-icon fontSet="material-symbols-outlined">{{
          several() ? 'domain' : apps.currentApp()?.icon || 'apps'
        }}</mat-icon>
        <span class="dc-ws__label">
          @if (several() && companies.currentCompany(); as company) {
            <span data-testid="workspace-company">{{ companies.name(company) | dcLocalize }}</span>
          }
          @if (several() && companies.currentCompany() && apps.apps().length) {
            <span class="dc-ws__dot" aria-hidden="true">·</span>
          }
          @if (apps.apps().length) {
            @if (apps.currentApp(); as app) {
              {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
            } @else {
              {{ 'businessApp.all' | dcTranslate }}
            }
          }
        </span>
        <mat-icon class="dc-ws__chevron" fontSet="material-symbols-outlined" iconPositionEnd
          >expand_more</mat-icon
        >
      </button>
      <mat-menu #menu="matMenu" class="dc-ws__menu">
        @if (several()) {
          <div class="dc-ws__heading" mat-menu-item disabled>
            {{ 'workspace.company' | dcTranslate }}
          </div>
          @for (company of companies.companies(); track company.code; let i = $index) {
            @if (
              companies.severalOrganizations() &&
              (i === 0 || companies.companies()[i - 1].entityCode !== company.entityCode)
            ) {
              <div class="dc-ws__org" mat-menu-item disabled>
                {{
                  { ar: company.entityNameAr ?? '', en: company.entityNameEn ?? '' } | dcLocalize
                }}
              </div>
            }
            <button
              mat-menu-item
              (click)="pick(company.code)"
              [attr.data-testid]="'workspace-company-' + company.code"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                companies.current() === company.code ? 'check' : 'domain'
              }}</mat-icon>
              {{ companies.name(company) | dcLocalize }}
            </button>
          }
          @if (apps.apps().length) {
            <div class="dc-ws__rule" role="separator"></div>
          }
        }
        @if (apps.apps().length) {
          <div class="dc-ws__heading" mat-menu-item disabled>
            {{ 'workspace.app' | dcTranslate }}
          </div>
          <button mat-menu-item (click)="apps.select(null)" data-testid="business-app-all">
            <mat-icon fontSet="material-symbols-outlined">{{
              apps.current() ? 'apps' : 'check'
            }}</mat-icon>
            {{ 'businessApp.all' | dcTranslate }}
          </button>
          @for (app of apps.apps(); track app.code) {
            <button
              mat-menu-item
              (click)="apps.select(app.code)"
              [attr.data-testid]="'business-app-' + app.code"
            >
              <mat-icon fontSet="material-symbols-outlined">{{
                apps.current() === app.code ? 'check' : app.icon || 'apps'
              }}</mat-icon>
              {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
            </button>
          }
        }
      </mat-menu>
    }
  `,
  styles: `
    .dc-ws {
      color: inherit;
      max-width: 22rem;
    }
    .dc-ws__label {
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .dc-ws__dot {
      margin-inline: 0.35rem;
      color: var(--dga-gray-500);
    }
    .dc-ws__heading,
    .dc-ws__org {
      min-height: 2rem;
      font-size: 12px;
      font-weight: 600;
    }
    .dc-ws__org {
      padding-inline-start: 1.5rem;
    }
    .dc-ws__rule {
      margin-block: 0.25rem;
      border-top: 1px solid var(--dga-gray-200);
    }
  `,
})
export class WorkspaceSwitcher {
  protected readonly companies = inject(CompanyContext);
  protected readonly apps = inject(BusinessAppContext);
  private readonly auth = inject(AuthService);
  protected readonly several = computed(() => this.companies.companies().length > 1);

  constructor() {
    // The companies and apps are the signed-in employee's: load them once signed in.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        void this.companies.load();
        void this.apps.load();
      }
    });
  }

  protected pick(company: string): void {
    if (company !== this.companies.current()) void this.companies.choose(company);
  }
}

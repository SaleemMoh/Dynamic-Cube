import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  AppsControlApi,
  CompanyContext,
  I18nService,
  LocalizePipe,
  MyBusinessApp,
  TranslatePipe,
  TranslationDictionary,
  WorkspaceCompany,
} from '@dynamic-cube/shared';
import { firstValueFrom } from 'rxjs';

const TEXTS: TranslationDictionary = {
  ar: {
    'workspace.title': 'اختر مكان عملك',
    'workspace.companyHint':
      'تعمل في شركة واحدة في كل مرة؛ تظهر لك قوائمها وصلاحياتك فيها. يمكنك التغيير لاحقاً من أعلى الصفحة.',
    'workspace.appHint': 'اختر التطبيق، أو كل التطبيقات.',
    'workspace.back': 'رجوع',
    'workspace.otherOrganization': 'أخرى',
  },
  en: {
    'workspace.title': 'Choose where you work',
    'workspace.companyHint':
      'You work in one company at a time: its menus and your privileges there. You can change it later from the top of the page.',
    'workspace.appHint': 'Choose the app, or all apps.',
    'workspace.back': 'Back',
    'workspace.otherOrganization': 'Other',
  },
};

interface OrganizationGroup {
  code: string;
  name: { ar: string; en: string } | null;
  companies: WorkspaceCompany[];
}

/**
 * Asked once after sign-in when the employee may work in several companies and has not chosen one (ADR-0036 step 5):
 * companies grouped by organization, then the apps of the chosen company as tiles when it has several.
 */
@Component({
  selector: 'dc-workspace-picker',
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, LocalizePipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="dc-pick"
      role="dialog"
      aria-modal="true"
      aria-labelledby="dc-pick-title"
      data-testid="workspace-picker"
    >
      <section class="dc-pick__card">
        <h1 id="dc-pick-title">{{ 'workspace.title' | dcTranslate }}</h1>
        @if (company(); as chosen) {
          <p class="dc-pick__hint">{{ 'workspace.appHint' | dcTranslate }}</p>
          <p class="dc-pick__chosen">
            <mat-icon fontSet="material-symbols-outlined">domain</mat-icon>
            {{ context.name(chosen) | dcLocalize }}
          </p>
          <div class="dc-pick__tiles">
            <button
              type="button"
              class="dc-pick__tile"
              [disabled]="busy()"
              (click)="finish(null)"
              data-testid="workspace-app-all"
            >
              <mat-icon fontSet="material-symbols-outlined">apps</mat-icon>
              {{ 'businessApp.all' | dcTranslate }}
            </button>
            @for (app of apps(); track app.code) {
              <button
                type="button"
                class="dc-pick__tile"
                [disabled]="busy()"
                (click)="finish(app.code)"
                [attr.data-testid]="'workspace-app-' + app.code"
              >
                <mat-icon fontSet="material-symbols-outlined">{{ app.icon || 'apps' }}</mat-icon>
                {{ { ar: app.nameAr, en: app.nameEn } | dcLocalize }}
              </button>
            }
          </div>
          <button matButton type="button" [disabled]="busy()" (click)="company.set(null)">
            <mat-icon fontSet="material-symbols-outlined">arrow_back</mat-icon>
            {{ 'workspace.back' | dcTranslate }}
          </button>
        } @else {
          <p class="dc-pick__hint">{{ 'workspace.companyHint' | dcTranslate }}</p>
          @for (group of groups(); track group.code) {
            @if (groups().length > 1) {
              <h2>
                {{
                  group.name
                    ? (group.name | dcLocalize)
                    : ('workspace.otherOrganization' | dcTranslate)
                }}
              </h2>
            }
            <div class="dc-pick__tiles">
              @for (c of group.companies; track c.code) {
                <button
                  type="button"
                  class="dc-pick__tile"
                  [disabled]="busy()"
                  (click)="pickCompany(c)"
                  [attr.data-testid]="'workspace-pick-' + c.code"
                >
                  <mat-icon fontSet="material-symbols-outlined">domain</mat-icon>
                  {{ context.name(c) | dcLocalize }}
                </button>
              }
            </div>
          }
        }
        @if (busy()) {
          <mat-spinner diameter="28" />
        }
      </section>
    </div>
  `,
  styles: `
    .dc-pick {
      position: fixed;
      inset: 0;
      z-index: 1000;
      display: grid;
      place-items: center;
      padding: 1rem;
      background: rgb(17 25 39 / 45%);
    }
    .dc-pick__card {
      display: grid;
      gap: 1rem;
      width: min(44rem, 100%);
      max-height: calc(100dvh - 2rem);
      overflow: auto;
      padding: 1.5rem;
      border-radius: 12px;
      background: #fff;
      box-shadow: 0 12px 40px rgb(0 0 0 / 20%);
    }
    h1 {
      margin: 0;
      font-size: 1.25rem;
    }
    h2 {
      margin: 0.25rem 0 0;
      font-size: 0.95rem;
      color: var(--dga-gray-700);
    }
    .dc-pick__hint {
      margin: 0;
      color: var(--dga-gray-600);
    }
    .dc-pick__chosen {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin: 0;
      font-weight: 600;
    }
    .dc-pick__tiles {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(12rem, 1fr));
      gap: 0.75rem;
    }
    .dc-pick__tile {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      min-height: 4rem;
      padding: 0.75rem 1rem;
      border: 1px solid var(--dga-gray-200);
      border-radius: 10px;
      background: #fff;
      color: var(--dga-gray-900);
      font: inherit;
      font-weight: 600;
      text-align: start;
      cursor: pointer;
    }
    .dc-pick__tile:hover:not(:disabled),
    .dc-pick__tile:focus-visible {
      border-color: var(--dynamic-cube-gold, #ca9f45);
      background: var(--dynamic-cube-gold-50, #faf7f0);
    }
    .dc-pick__tile mat-icon {
      color: var(--dynamic-cube-gold-700, #8a6a26);
    }
  `,
})
export class WorkspacePicker {
  protected readonly context = inject(CompanyContext);
  private readonly api = inject(AppsControlApi);
  protected readonly company = signal<WorkspaceCompany | null>(null);
  protected readonly apps = signal<MyBusinessApp[]>([]);
  protected readonly busy = computed(() => this.loadingApps() || this.context.switching());
  private readonly loadingApps = signal(false);

  protected readonly groups = computed<OrganizationGroup[]>(() => {
    const groups: OrganizationGroup[] = [];
    for (const c of this.context.companies()) {
      const code = c.entityCode ?? '';
      let group = groups.find((g) => g.code === code);
      if (!group) {
        group = {
          code,
          name: c.entityCode
            ? { ar: c.entityNameAr ?? c.entityCode, en: c.entityNameEn ?? c.entityCode }
            : null,
          companies: [],
        };
        groups.push(group);
      }
      group.companies.push(c);
    }
    return groups;
  });

  constructor() {
    inject(I18nService).register(TEXTS);
  }

  /** A company with several apps asks for the app next; otherwise the choice is made. */
  protected async pickCompany(company: WorkspaceCompany): Promise<void> {
    this.loadingApps.set(true);
    try {
      const apps = await firstValueFrom(this.api.myBusinessApps(company.code));
      if (apps.length > 1) {
        this.apps.set(apps);
        this.company.set(company);
      } else {
        await this.context.choose(company.code, apps[0]?.code ?? null);
      }
    } catch {
      await this.context.choose(company.code);
    } finally {
      this.loadingApps.set(false);
    }
  }

  protected finish(app: string | null): void {
    const company = this.company();
    if (company) void this.context.choose(company.code, app);
  }
}

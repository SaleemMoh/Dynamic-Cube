import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AppsControlApi, ConfigRow, LocalizePipe, LocalizedText } from '@dynamic-cube/shared';
import { catchError, forkJoin, of } from 'rxjs';

/** What each Dynamic Cube app is for. */
const PURPOSE: Record<string, { icon: string; text: LocalizedText }> = {
  INTERNAL: {
    icon: 'desktop_windows',
    text: {
      ar: 'للموظفين: التحكم بالتطبيقات، ومعالجة الطلبات والمهام، والإعدادات.',
      en: 'For staff: Apps Control, request and task processing, configuration.',
    },
  },
  EXTERNAL: {
    icon: 'public',
    text: {
      ar: 'للمستفيدين: الخدمات والطلبات والوثائق.',
      en: 'For applicants: services, requests and documents.',
    },
  },
  FIELD: {
    icon: 'smartphone',
    text: {
      ar: 'للمفتشين في الموقع: الزيارات، ويعمل دون اتصال.',
      en: 'For inspectors on site: visits, works offline.',
    },
  },
};

/**
 * The Apps screen's two kinds of app (ADR-0036): the Dynamic Cube apps themselves (internal system, portal, field app),
 * and below them the apps Dynamic Cube manages, built on it (shown in those apps) or connected systems with their own
 * screens.
 */
@Component({
  selector: 'dc-apps-intro',
  imports: [MatIconModule, RouterLink, LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dc-apps" data-testid="dc-apps">
      <h3>{{ t.dcApps | dcLocalize }}</h3>
      <p class="dc-muted">{{ t.dcAppsHint | dcLocalize }}</p>
      <div class="dc-apps__cards">
        @for (app of channels(); track app.row.code) {
          <article class="dc-apps__card" [attr.data-testid]="'dc-app-' + app.row.code">
            <span class="dc-apps__icon">
              <mat-icon fontSet="material-symbols-outlined">{{ app.icon }}</mat-icon>
            </span>
            <div class="dc-apps__body">
              <strong>
                {{ { ar: app.row.nameAr, en: app.row.nameEn } | dcLocalize }}
                <span class="mono dc-muted" dir="ltr">{{ app.row.code }}</span>
              </strong>
              <p>{{ app.purpose | dcLocalize }}</p>
              <span class="dc-apps__meta">
                <a [routerLink]="['/apps-control/menus', app.row.code]">{{
                  t.menus | dcLocalize
                }}</a>
                ·
                <a routerLink="/apps-control/branding">{{ t.branding | dcLocalize }}</a>
                · {{ t.used | dcLocalize }} {{ app.used }}
              </span>
            </div>
          </article>
        }
      </div>
      <h3>{{ t.managed | dcLocalize }}</h3>
      <p class="dc-muted">{{ t.managedHint | dcLocalize }}</p>
    </section>
  `,
  styles: `
    .dc-apps h3 {
      margin: 4px 0 2px;
    }
    .dc-apps h3 + p {
      margin: 0 0 10px;
    }
    .dc-apps__cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
      gap: 12px;
      margin-bottom: 20px;
    }
    .dc-apps__card {
      display: flex;
      gap: 12px;
      padding: 14px;
      border: 1px solid var(--dga-gray-200);
      border-radius: var(--shell-radius-lg, 8px);
      background: var(--dga-white, #fff);
    }
    .dc-apps__icon {
      display: grid;
      place-items: center;
      flex: none;
      width: 40px;
      height: 40px;
      border-radius: 10px;
      background: var(--dga-gray-100);
    }
    .dc-apps__body {
      display: grid;
      gap: 4px;
      min-width: 0;
    }
    .dc-apps__body p {
      margin: 0;
      font-size: 13px;
    }
    .dc-apps__meta {
      font-size: 13px;
      color: var(--dga-gray-600);
    }
  `,
})
export class AppsIntro {
  private readonly api = inject(AppsControlApi);
  protected readonly t = {
    dcApps: { ar: 'تطبيقات ديناميك كيوب', en: 'Dynamic Cube apps' },
    dcAppsHint: {
      ar: 'تطبيقات المنتج نفسه. لكل منها قوائمه وهويته، وتظهر فيها خدمات وشاشات التطبيقات المبنية على ديناميك كيوب.',
      en: 'The product’s own apps. Each has its menus and look; apps built on Dynamic Cube show their services and screens in them.',
    },
    managed: { ar: 'تطبيقات تديرها ديناميك كيوب', en: 'Apps managed by Dynamic Cube' },
    managedHint: {
      ar: 'مبني على ديناميك كيوب: خدماته وشاشاته في التطبيقات أعلاه. نظام مرتبط: نظام خارجي بشاشاته، يستخدم ديناميك كيوب لتسجيل الدخول والمستخدمين والأدوار والقوائم.',
      en: 'Built on Dynamic Cube: its services and screens are in the apps above. Connected system: an outside system with its own screens that uses Dynamic Cube for sign-in, users, roles and menus.',
    },
    menus: { ar: 'القوائم', en: 'Menus' },
    branding: { ar: 'الهوية', en: 'Branding' },
    used: { ar: 'تطبيقات مبنية عليه:', en: 'Apps built on it:' },
  } satisfies Record<string, LocalizedText>;

  private readonly data = rxResource({
    stream: () =>
      forkJoin({
        channels: this.api.list('apps.app').pipe(catchError(() => of([] as ConfigRow[]))),
        apps: this.api.list('apps.business-app').pipe(catchError(() => of([] as ConfigRow[]))),
      }),
  });
  protected readonly channels = computed(() => {
    const data = this.data.value();
    if (!data) return [];
    return data.channels
      .filter((c) => PURPOSE[c.code])
      .map((row) => ({
        row,
        icon: PURPOSE[row.code].icon,
        purpose: PURPOSE[row.code].text,
        used: data.apps.filter(
          (a) =>
            a['kind'] === 'DYNAMIC_CUBE' &&
            ((a['channels'] as string[] | undefined) ?? []).includes(row.code),
        ).length,
      }));
  });
}

import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { APPS_CONTROL_API, BrandingService, ContentApi, FooterLink, LocalizedText, PortalCategory, ServicesApi } from '@dynamic-cube/shared';
import { catchError, map, of } from 'rxjs';

export type StatisticMetric = 'SERVICES' | 'REQUESTS' | 'IN_PROGRESS' | 'COMPLETED';

/** A statistics tile: a counted metric or a fixed value. */
export interface StatisticTile {
  metric?: StatisticMetric;
  value?: number;
  icon?: string;
  labelAr: string;
  labelEn: string;
}

/** The public setting `PORTAL_HOME` (Apps Control, app EXTERNAL): hero banner, statistics tiles and help links. */
export interface PortalHomeSetting {
  heroTitleAr?: string;
  heroTitleEn?: string;
  heroTextAr?: string;
  heroTextEn?: string;
  bannerUrl?: string | null;
  statistics?: StatisticTile[];
  help?: { phone?: string | null; email?: string | null; contactUrl?: string | null; accessibilityUrl?: string | null };
}

const DEFAULT_TILES: StatisticTile[] = [
  { metric: 'SERVICES', icon: 'apps', labelAr: 'خدمة إلكترونية', labelEn: 'E-services' },
  { metric: 'REQUESTS', icon: 'send', labelAr: 'طلب مقدم', labelEn: 'Requests submitted' },
  { metric: 'COMPLETED', icon: 'task_alt', labelAr: 'طلب مكتمل', labelEn: 'Requests completed' },
];

/** The portal's public data, loaded once: catalog (categories and services), statistics and the home settings. */
@Injectable({ providedIn: 'root' })
export class PortalStore {
  private readonly api = inject(ServicesApi);
  private readonly http = inject(HttpClient);
  private readonly content = inject(ContentApi);
  private readonly branding = inject(BrandingService).branding;

  readonly catalog = rxResource({ stream: () => this.api.portalCatalog() });
  readonly statistics = rxResource({ stream: () => this.api.portalStatistics().pipe(catchError(() => of(null))) });
  readonly setting = rxResource({
    stream: () =>
      this.http.get<{ value: PortalHomeSetting }>(`${APPS_CONTROL_API}/settings/PORTAL_HOME`, { params: { app: 'EXTERNAL' } }).pipe(
        map((s) => s.value ?? {}),
        // Without the setting the home page uses its built-in wording and tiles.
        catchError(() => of<PortalHomeSetting>({})),
      ),
  });

  readonly pages = rxResource({ stream: () => this.content.pages().pipe(catchError(() => of([]))) });

  /** Footer: the pages marked for it, then the questions. */
  readonly footerLinks = computed<FooterLink[]>(() => [
    ...(this.pages.value() ?? []).filter((p) => p.showInFooter).map((p) => ({ labelAr: p.titleAr, labelEn: p.titleEn, url: `/pages/${p.slug}` })),
    { labelAr: 'الأسئلة الشائعة', labelEn: 'FAQs', url: '/faq' },
  ]);

  readonly categories = computed<PortalCategory[]>(() => this.catalog.value()?.categories ?? []);
  readonly services = computed(() => this.catalog.value()?.services ?? []);
  readonly tiles = computed(() => this.setting.value()?.statistics ?? DEFAULT_TILES);
  /** Phone and email from the home setting's help, else the entity's contact details (branding). */
  readonly help = computed(() => {
    const help = this.setting.value()?.help ?? {};
    const contact = this.branding().contact;
    return { ...help, phone: help.phone || contact?.phone || null, email: help.email || contact?.email || null };
  });
  /** The entity's contact details for side panels: phone, email, location and social accounts. */
  readonly contact = computed(() => {
    const contact = this.branding().contact;
    const help = this.help();
    return {
      phone: help.phone,
      email: help.email,
      address: contact?.addressAr || contact?.addressEn ? { ar: contact.addressAr ?? '', en: contact.addressEn ?? '' } : null,
      mapUrl: contact?.mapUrl ?? null,
      social: contact?.social ?? [],
    };
  });
  /** When the catalog last changed: the "last modified" line above the footer. */
  readonly lastUpdated = computed(() => this.catalog.value()?.lastUpdated ?? null);

  heroTitle(fallback: LocalizedText): LocalizedText {
    const s = this.setting.value();
    return s?.heroTitleAr || s?.heroTitleEn ? { ar: s.heroTitleAr ?? '', en: s.heroTitleEn ?? '' } : fallback;
  }

  heroText(fallback: LocalizedText): LocalizedText {
    const s = this.setting.value();
    return s?.heroTextAr || s?.heroTextEn ? { ar: s.heroTextAr ?? '', en: s.heroTextEn ?? '' } : fallback;
  }

  category(code: string | null | undefined): PortalCategory | undefined {
    return code ? this.categories().find((c) => c.code === code) : undefined;
  }

  tileValue(tile: StatisticTile): number | null {
    if (tile.value !== undefined && tile.value !== null) return tile.value;
    const s = this.statistics.value();
    if (!s) return null;
    switch (tile.metric) {
      case 'SERVICES':
        return s.services;
      case 'REQUESTS':
        return s.requests;
      case 'IN_PROGRESS':
        return s.inProgress;
      case 'COMPLETED':
        return s.completed;
      default:
        return null;
    }
  }
}

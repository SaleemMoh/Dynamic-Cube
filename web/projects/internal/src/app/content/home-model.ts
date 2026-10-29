/** The public setting `PORTAL_HOME` as the portal reads it (see the portal's `PortalHomeSetting`). */
export type Metric = 'SERVICES' | 'REQUESTS' | 'IN_PROGRESS' | 'COMPLETED';

export const METRICS: Metric[] = ['SERVICES', 'REQUESTS', 'IN_PROGRESS', 'COMPLETED'];

export interface Tile {
  metric?: Metric;
  value?: number;
  icon?: string;
  labelAr: string;
  labelEn: string;
}

export interface HomeSetting {
  heroTitleAr: string;
  heroTitleEn: string;
  heroTextAr: string;
  heroTextEn: string;
  bannerUrl: string | null;
  statistics: Tile[];
  help: { phone: string | null; email: string | null; contactUrl: string | null; accessibilityUrl: string | null };
}

export const MAX_TILES = 6;

const URL = /^(https?:\/\/\S+|\/(?!\/)\S*)$/i;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const ICON = /^[a-z0-9_]{1,40}$/;

const text = (value: unknown): string => (typeof value === 'string' ? value : '');
const optional = (value: unknown): string | null => (typeof value === 'string' && value.trim() ? value.trim() : null);

/** Fills what a stored value lacks, so the editor always has every field. */
export function normalizeHome(value: unknown): HomeSetting {
  const v = (value && typeof value === 'object' ? value : {}) as Record<string, unknown>;
  const help = (v['help'] && typeof v['help'] === 'object' ? v['help'] : {}) as Record<string, unknown>;
  const tiles = Array.isArray(v['statistics']) ? (v['statistics'] as Record<string, unknown>[]) : [];
  return {
    heroTitleAr: text(v['heroTitleAr']),
    heroTitleEn: text(v['heroTitleEn']),
    heroTextAr: text(v['heroTextAr']),
    heroTextEn: text(v['heroTextEn']),
    bannerUrl: optional(v['bannerUrl']),
    statistics: tiles.map((t) => {
      const metric = METRICS.includes(t['metric'] as Metric) ? (t['metric'] as Metric) : undefined;
      return {
        ...(metric ? { metric } : { value: typeof t['value'] === 'number' ? t['value'] : 0 }),
        icon: text(t['icon']) || undefined,
        labelAr: text(t['labelAr']),
        labelEn: text(t['labelEn']),
      };
    }),
    help: {
      phone: optional(help['phone']),
      email: optional(help['email']),
      contactUrl: optional(help['contactUrl']),
      accessibilityUrl: optional(help['accessibilityUrl']),
    },
  };
}

/** Translation keys of what keeps the home page from being saved. */
export function homeProblems(home: HomeSetting): string[] {
  const list: string[] = [];
  if (!home.heroTitleAr.trim() || !home.heroTitleEn.trim()) list.push('portalHome.problem.title');
  if (home.bannerUrl && !URL.test(home.bannerUrl)) list.push('portalHome.problem.banner');
  if (home.statistics.length > MAX_TILES) list.push('portalHome.problem.tooMany');
  if (home.statistics.some((t) => !t.labelAr.trim() || !t.labelEn.trim())) list.push('portalHome.problem.tileLabel');
  if (home.statistics.some((t) => !t.metric && (typeof t.value !== 'number' || Number.isNaN(t.value) || t.value < 0)))
    list.push('portalHome.problem.tileValue');
  if (home.statistics.some((t) => t.icon && !ICON.test(t.icon))) list.push('portalHome.problem.icon');
  if (home.help.email && !EMAIL.test(home.help.email)) list.push('portalHome.problem.email');
  if ([home.help.contactUrl, home.help.accessibilityUrl].some((u) => u && !URL.test(u))) list.push('portalHome.problem.link');
  return list;
}

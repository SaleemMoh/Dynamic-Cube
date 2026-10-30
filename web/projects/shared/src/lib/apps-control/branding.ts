import {
  EnvironmentProviders,
  Injectable,
  effect,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
  signal,
  untracked,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { catchError, firstValueFrom, of } from 'rxjs';
import { RUNTIME_CONFIG } from '../config/runtime-config';
import { AppsControlApi } from './apps-control.api';
import { BrandingInfo } from './apps-control.models';
import { BusinessAppContext } from './business-app';

const EMPTY: BrandingInfo = { nameAr: '', nameEn: '', logoUrl: null, footerLinks: [] };
const PORTAL_APP_KEY = 'dc.portal.app';

/**
 * The look the app shows (name, logo, footer links and images, colour, title) from Apps Control: the channel's, and the
 * chosen app's on top of it. Loaded at start-up and again when the user picks another app.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly api = inject(AppsControlApi);
  private readonly root = inject(DOCUMENT).documentElement;
  private readonly channel = inject(RUNTIME_CONFIG, { optional: true })?.app ?? 'INTERNAL';
  readonly branding = signal<BrandingInfo>(EMPTY);
  private loaded: string | null | undefined;

  async load(app: string | null): Promise<void> {
    this.loaded = app;
    const branding = await firstValueFrom(this.api.branding(this.channel, app).pipe(catchError(() => of(EMPTY))));
    if (this.loaded !== app) return;
    this.branding.set(branding);
    // The internal system (and field app) take the internal colour family, the portal its own.
    const kind = this.channel === 'EXTERNAL' ? 'portal' : 'internal';
    const color = branding.themeColor ?? (kind === 'portal' ? branding.portalThemeColor : branding.internalThemeColor);
    clearThemeColor(this.root, kind);
    if (color) applyThemeColor(this.root, color, kind);
  }

  /** Whether an app other than the one shown is chosen now. */
  changed(app: string | null): boolean {
    return this.loaded !== app;
  }
}

/**
 * The app whose look the portal shows: given once as <code>?app=CODE</code> in a link to the portal and remembered for
 * the visit; none means the portal's own look.
 */
export function portalApp(document: Document): string | null {
  try {
    const given = new URLSearchParams(document.location?.search ?? '').get('app');
    if (given !== null) {
      if (given) sessionStorage.setItem(PORTAL_APP_KEY, given);
      else sessionStorage.removeItem(PORTAL_APP_KEY);
      return given || null;
    }
    return sessionStorage.getItem(PORTAL_APP_KEY);
  } catch {
    return null;
  }
}

export function provideBranding(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      // Everything injected before the first await (injection works only synchronously here).
      const service = inject(BrandingService);
      const channel = inject(RUNTIME_CONFIG, { optional: true })?.app;
      const document = inject(DOCUMENT);
      const apps = inject(BusinessAppContext);
      const portal = channel === 'EXTERNAL';
      // Staff pick the app in the header; the portal takes it from its link.
      if (!portal) {
        effect(() => {
          const app = apps.current();
          untracked(() => {
            if (service.changed(app)) void service.load(app);
          });
        });
      }
      await service.load(portal ? portalApp(document) : apps.current());
    }),
  ]);
}

const HEX = /^#[0-9a-f]{6}$/i;

/**
 * The colour variables an app's styles read, derived from one main colour: darker shades for hover and text on white,
 * light tints for selected rows and icon tiles. The internal system uses the `--dynamic-cube-gold*` family, the portal
 * the DGA greens (and Material's primary).
 */
export function themeVariables(color: string, kind: 'internal' | 'portal'): Record<string, string> {
  if (!HEX.test(color)) return {};
  const mix = (amount: number, other: string) => `color-mix(in srgb, ${color} ${amount}%, ${other})`;
  if (kind === 'internal') {
    return {
      '--dynamic-cube-gold': color,
      '--dynamic-cube-gold-600': mix(85, '#000'),
      '--dynamic-cube-gold-700': mix(62, '#000'),
      '--dynamic-cube-gold-100': mix(22, '#fff'),
      '--dynamic-cube-gold-50': mix(8, '#fff'),
    };
  }
  return {
    '--dga-green-400': mix(60, '#fff'),
    '--dga-green-600': mix(88, '#fff'),
    '--dga-green-700': color,
    '--dga-green-800': mix(85, '#000'),
    '--dga-green-950': mix(55, '#000'),
    '--dga-green-50': mix(8, '#fff'),
    '--dga-primary': color,
    '--dga-primary-dark': mix(85, '#000'),
    '--mat-sys-primary': color,
    '--mat-sys-primary-container': mix(8, '#fff'),
    '--mat-sys-on-primary-container': mix(55, '#000'),
  };
}

/** Back to the stylesheet's own colours (before another app's colour is applied, or when none is chosen). */
export function clearThemeColor(element: HTMLElement, kind: 'internal' | 'portal'): void {
  for (const name of Object.keys(themeVariables('#000000', kind))) {
    element.style.removeProperty(name);
  }
}

export function applyThemeColor(element: HTMLElement, color: string, kind: 'internal' | 'portal'): void {
  for (const [name, value] of Object.entries(themeVariables(color, kind))) {
    element.style.setProperty(name, value);
  }
}

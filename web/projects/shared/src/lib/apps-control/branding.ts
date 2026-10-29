import {
  EnvironmentProviders,
  Injectable,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
  signal,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { catchError, firstValueFrom, of } from 'rxjs';
import { RUNTIME_CONFIG } from '../config/runtime-config';
import { AppsControlApi } from './apps-control.api';
import { BrandingInfo } from './apps-control.models';

const EMPTY: BrandingInfo = { nameAr: '', nameEn: '', logoUrl: null, footerLinks: [] };

/** The entity's branding (name, logo, footer links and image, colours) from Apps Control; loaded once at start-up. */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  readonly branding = signal<BrandingInfo>(EMPTY);
}

export function provideBranding(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      // Everything injected before the first await (injection works only synchronously here).
      const api = inject(AppsControlApi);
      const service = inject(BrandingService);
      const app = inject(RUNTIME_CONFIG, { optional: true })?.app;
      const root = inject(DOCUMENT).documentElement;
      const branding = await firstValueFrom(api.branding().pipe(catchError(() => of(EMPTY))));
      service.branding.set(branding);
      // The internal system (and field app) take the internal colour, the portal its own.
      const color = app === 'EXTERNAL' ? branding.portalThemeColor : branding.internalThemeColor;
      if (color) {
        applyThemeColor(root, color, app === 'EXTERNAL' ? 'portal' : 'internal');
      }
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

export function applyThemeColor(element: HTMLElement, color: string, kind: 'internal' | 'portal'): void {
  for (const [name, value] of Object.entries(themeVariables(color, kind))) {
    element.style.setProperty(name, value);
  }
}

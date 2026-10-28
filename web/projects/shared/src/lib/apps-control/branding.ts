import {
  EnvironmentProviders,
  Injectable,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
  signal,
} from '@angular/core';
import { catchError, firstValueFrom, of } from 'rxjs';
import { AppsControlApi } from './apps-control.api';
import { BrandingInfo } from './apps-control.models';

const EMPTY: BrandingInfo = { nameAr: '', nameEn: '', logoUrl: null, footerLinks: [] };

/** The entity's branding (name, logo, footer links) from Apps Control; loaded once at start-up. */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  readonly branding = signal<BrandingInfo>(EMPTY);
}

export function provideBranding(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      const api = inject(AppsControlApi);
      inject(BrandingService).branding.set(
        await firstValueFrom(api.branding().pipe(catchError(() => of(EMPTY)))),
      );
    }),
  ]);
}

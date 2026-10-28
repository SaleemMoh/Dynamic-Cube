import { EnvironmentProviders, inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { RUNTIME_CONFIG, RuntimeConfig } from '../config/runtime-config';
import { AuthService } from './auth.service';

/** Provides the runtime configuration and restores the sign-in session before the first navigation. */
export function provideAuth(config: RuntimeConfig): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: RUNTIME_CONFIG, useValue: config },
    provideAppInitializer(() => inject(AuthService).initialize()),
  ]);
}

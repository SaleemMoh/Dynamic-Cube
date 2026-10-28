import { InjectionToken } from '@angular/core';

/**
 * Deployment settings read at start-up from `/config.json`, so one build runs in every environment.
 * The web container generates the file from environment variables.
 */
export interface RuntimeConfig {
  /** App code known to Apps Control, e.g. `INTERNAL`. */
  app: string;
  auth: AuthConfig;
}

export interface AuthConfig {
  /** OpenID Connect issuer, e.g. `https://sso.example.gov.sa/realms/dynamic-cube-internal`. */
  authority: string;
  clientId: string;
  scope?: string;
  /** When false the app starts without sign-in (public pages); sign-in is offered on demand. */
  required?: boolean;
}

export const RUNTIME_CONFIG = new InjectionToken<RuntimeConfig>('dc.runtimeConfig');

/** Loads `/config.json` before the application bootstraps. */
export async function loadRuntimeConfig(url = '/config.json'): Promise<RuntimeConfig> {
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Cannot load ${url}: ${response.status}`);
  }
  return (await response.json()) as RuntimeConfig;
}

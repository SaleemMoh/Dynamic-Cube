import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { inject } from '@angular/core';
import {
  NOTIFICATIONS_BASE,
  PARTY_NOTIFICATIONS,
  RECORD_PROVIDER,
  RecordsApi,
  RuntimeConfig,
  actingPartyInterceptor,
  authInterceptor,
  partyRecordProvider,
  provideAuth,
  provideBranding,
  provideDynamicCubeUi,
  provideLovSource,
  provideUiTexts,
} from '@dynamic-cube/shared';
import { routes } from './app.routes';

export function createAppConfig(config: RuntimeConfig): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideRouter(routes, withComponentInputBinding()),
      provideHttpClient(withFetch(), withInterceptors([authInterceptor, actingPartyInterceptor])),
      provideAuth(config),
      provideUiTexts(),
      provideBranding(),
      provideLovSource(),
      provideDynamicCubeUi(),
      // The portal shows the acting party's notifications.
      { provide: NOTIFICATIONS_BASE, useValue: PARTY_NOTIFICATIONS },
      // Rows questions can pick from the acting party's record lists.
      { provide: RECORD_PROVIDER, useFactory: () => partyRecordProvider(inject(RecordsApi)) },
    ],
  };
}

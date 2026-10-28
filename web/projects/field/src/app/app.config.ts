import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RuntimeConfig, authInterceptor, provideAuth, provideBranding, provideDynamicCubeUi, provideLovSource, provideUiTexts } from '@dynamic-cube/shared';
import { routes } from './app.routes';

export function createAppConfig(config: RuntimeConfig): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideRouter(routes, withComponentInputBinding()),
      provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
      provideAuth(config),
      provideUiTexts(),
      provideBranding(),
      provideLovSource(),
      provideDynamicCubeUi(),
    ],
  };
}

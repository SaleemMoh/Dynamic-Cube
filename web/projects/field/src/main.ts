import { isDevMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { loadRuntimeConfig } from '@dynamic-cube/shared';
import { createAppConfig } from './app/app.config';
import { App } from './app/app';

// Deployment settings (identity provider, app code) come from /config.json so one build fits every environment.
loadRuntimeConfig()
  .then((config) => bootstrapApplication(App, createAppConfig(config)))
  .catch((err) => console.error(err));

// The service worker keeps the app and its settings on the device, so it opens without a connection.
if (!isDevMode() && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => void navigator.serviceWorker.register('/sw.js').catch((err) => console.error(err)));
}

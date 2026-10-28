import { Routes } from '@angular/router';
import { INTEGRATION_SECTIONS } from '../apps-control/config/module-sections';

export const INTEGRATION_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'operations' },
  { path: 'test-console', loadComponent: () => import('./test-console').then((m) => m.TestConsole) },
  { path: 'call-log', loadComponent: () => import('./call-log').then((m) => m.CallLog) },
  ...INTEGRATION_SECTIONS.map((section) => ({
    path: section.path,
    data: { section: section.path },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  })),
];

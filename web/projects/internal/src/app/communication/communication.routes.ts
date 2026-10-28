import { Routes } from '@angular/router';

export const COMMUNICATION_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./message-log').then((m) => m.MessageLog) },
  {
    path: 'rules',
    data: { section: 'notification-rules' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  {
    path: 'templates',
    data: { section: 'message-templates' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
];

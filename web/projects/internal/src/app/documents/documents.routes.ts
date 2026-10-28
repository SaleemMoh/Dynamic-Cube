import { Routes } from '@angular/router';

export const DOCUMENTS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./document-list').then((m) => m.DocumentList) },
  {
    path: 'types',
    data: { section: 'document-types' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  {
    path: 'templates',
    data: { section: 'document-templates' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  { path: ':id', loadComponent: () => import('./document-page').then((m) => m.DocumentPage) },
];

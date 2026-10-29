import { Routes } from '@angular/router';

export const VISITS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./visit-list').then((m) => m.VisitList) },
  {
    path: 'types',
    data: { section: 'visit-types' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  { path: ':id', loadComponent: () => import('./visit-page').then((m) => m.VisitPage) },
];

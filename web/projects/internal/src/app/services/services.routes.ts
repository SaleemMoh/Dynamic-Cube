import { Routes } from '@angular/router';
import { SERVICE_SECTIONS } from '../apps-control/config/module-sections';

export const SERVICES_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'requests' },
  // Categories are a class of the data dictionary, edited in Classes & domains.
  { path: 'categories', redirectTo: '/apps-control/classes/SVC_CATEGORY' },
  { path: 'requests', loadComponent: () => import('./request-list').then((m) => m.RequestList) },
  { path: 'requests/:id', loadComponent: () => import('./request-detail').then((m) => m.RequestDetail) },
  { path: 'versions', loadComponent: () => import('./version-list').then((m) => m.VersionList) },
  { path: 'builder', loadComponent: () => import('./builder/builder-page').then((m) => m.BuilderPage) },
  ...SERVICE_SECTIONS.map((section) => ({
    path: section.path,
    data: { section: section.path },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  })),
];

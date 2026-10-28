import { Routes } from '@angular/router';

export const WORKFLOWS_ROUTES: Routes = [
  {
    path: '',
    data: { section: 'workflows' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  {
    path: 'versions',
    loadComponent: () => import('./workflow-version-list').then((m) => m.WorkflowVersionList),
  },
];

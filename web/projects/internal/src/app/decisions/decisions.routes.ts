import { Routes } from '@angular/router';

export const DECISIONS_ROUTES: Routes = [
  {
    path: '',
    data: { section: 'rule-sets' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  { path: 'versions', loadComponent: () => import('./rule-set-versions').then((m) => m.RuleSetVersions) },
];

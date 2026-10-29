import { Routes } from '@angular/router';

export const PAYMENTS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./bill-list').then((m) => m.BillList) },
  {
    path: 'providers',
    data: { section: 'payment-providers' },
    loadComponent: () =>
      import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  { path: ':id', loadComponent: () => import('./bill-page').then((m) => m.BillPage) },
];

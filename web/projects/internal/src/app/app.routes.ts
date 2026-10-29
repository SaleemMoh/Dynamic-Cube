import { Routes } from '@angular/router';
import { AuthCallback, AUTH_CALLBACK_PATH, authGuard } from '@dynamic-cube/shared';
import { Workspace } from './workspace/workspace';

export const routes: Routes = [
  { path: AUTH_CALLBACK_PATH, component: AuthCallback },
  {
    path: '',
    component: Workspace,
    canActivate: [authGuard],
    children: [
      { path: '', loadComponent: () => import('./home/home').then((m) => m.Home) },
      {
        path: 'apps-control',
        loadChildren: () =>
          import('./apps-control/apps-control.routes').then((m) => m.APPS_CONTROL_ROUTES),
      },
      {
        path: 'integration',
        loadChildren: () =>
          import('./integration/integration.routes').then((m) => m.INTEGRATION_ROUTES),
      },
      {
        path: 'party',
        loadChildren: () => import('./party/party.routes').then((m) => m.PARTY_ROUTES),
      },
      {
        path: 'services',
        loadChildren: () => import('./services/services.routes').then((m) => m.SERVICES_ROUTES),
      },
      {
        path: 'action-center',
        loadChildren: () =>
          import('./action-center/action-center.routes').then((m) => m.ACTION_CENTER_ROUTES),
      },
      {
        path: 'workflows',
        loadChildren: () => import('./workflows/workflows.routes').then((m) => m.WORKFLOWS_ROUTES),
      },
      {
        path: 'documents',
        loadChildren: () => import('./documents/documents.routes').then((m) => m.DOCUMENTS_ROUTES),
      },
      {
        path: 'payments',
        loadChildren: () => import('./payments/payments.routes').then((m) => m.PAYMENTS_ROUTES),
      },
      {
        path: 'decisions',
        loadChildren: () => import('./decisions/decisions.routes').then((m) => m.DECISIONS_ROUTES),
      },
      {
        path: 'content',
        loadChildren: () => import('./content/content.routes').then((m) => m.CONTENT_ROUTES),
      },
      {
        path: 'communication',
        loadChildren: () =>
          import('./communication/communication.routes').then((m) => m.COMMUNICATION_ROUTES),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

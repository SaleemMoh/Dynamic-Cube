import { Routes } from '@angular/router';
import { AuthCallback, AUTH_CALLBACK_PATH, authGuard } from '@dynamic-cube/shared';

export const routes: Routes = [
  { path: AUTH_CALLBACK_PATH, component: AuthCallback },
  { path: '', loadComponent: () => import('./home/home').then((m) => m.Home) },
  // Public: anyone may browse the services and read their cards; starting one asks for sign-in.
  { path: 'services', loadComponent: () => import('./services/catalog').then((m) => m.Catalog) },
  { path: 'services/:code', loadComponent: () => import('./services/service-page').then((m) => m.ServicePage) },
  { path: 'requests', canActivate: [authGuard], loadComponent: () => import('./requests/my-requests').then((m) => m.MyRequests) },
  { path: 'requests/:id', canActivate: [authGuard], loadComponent: () => import('./requests/request-page').then((m) => m.RequestPage) },
  { path: 'documents', canActivate: [authGuard], loadComponent: () => import('./documents/my-documents').then((m) => m.MyDocuments) },
  { path: 'documents/:id', canActivate: [authGuard], loadComponent: () => import('./documents/my-document-page').then((m) => m.MyDocumentPage) },
  // Public: the QR code on every document opens /verify/CODE.
  { path: 'verify', loadComponent: () => import('./documents/verify-page').then((m) => m.VerifyPage) },
  { path: 'verify/:code', loadComponent: () => import('./documents/verify-page').then((m) => m.VerifyPage) },
  { path: '**', redirectTo: '' },
];

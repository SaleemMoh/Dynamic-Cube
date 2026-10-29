import { CanActivateFn, Routes } from '@angular/router';
import { AuthCallback, AUTH_CALLBACK_PATH, authGuard } from '@dynamic-cube/shared';

/** Signed in, or offline: the visits kept on the device can be worked on, and reports wait for the connection. */
const fieldGuard: CanActivateFn = (route, state) => (navigator.onLine ? authGuard(route, state) : true);

export const routes: Routes = [
  { path: AUTH_CALLBACK_PATH, component: AuthCallback },
  { path: '', loadComponent: () => import('./home/home').then((m) => m.Home), canActivate: [fieldGuard] },
  { path: 'visit/:id', loadComponent: () => import('./visit/visit-page').then((m) => m.FieldVisitPage), canActivate: [fieldGuard] },
  { path: '**', redirectTo: '' },
];

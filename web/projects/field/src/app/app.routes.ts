import { Routes } from '@angular/router';
import { AuthCallback, AUTH_CALLBACK_PATH, authGuard } from '@dynamic-cube/shared';

export const routes: Routes = [
  { path: AUTH_CALLBACK_PATH, component: AuthCallback },
  { path: '', loadComponent: () => import('./home/home').then((m) => m.Home), canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];

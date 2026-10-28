import { Routes } from '@angular/router';

export const ACTION_CENTER_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./inbox').then((m) => m.Inbox) },
  { path: 'tasks/:id', loadComponent: () => import('./task-page').then((m) => m.TaskPage) },
  { path: 'supervision', loadComponent: () => import('./supervision').then((m) => m.Supervision) },
];

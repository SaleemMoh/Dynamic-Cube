import { Routes } from '@angular/router';
import { CONFIG_SECTIONS } from './config/config-types';

export const APPS_CONTROL_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'change-sets' },
  { path: 'change-sets', loadComponent: () => import('./change-sets/change-set-list').then((m) => m.ChangeSetList) },
  { path: 'change-sets/:id', loadComponent: () => import('./change-sets/change-set-detail').then((m) => m.ChangeSetDetail) },
  { path: 'branding', loadComponent: () => import('./branding/branding-page').then((m) => m.BrandingPage) },
  { path: 'classes', loadComponent: () => import('./classes/class-list').then((m) => m.ClassList) },
  { path: 'classes/:code', loadComponent: () => import('./classes/class-detail').then((m) => m.ClassDetail) },
  ...CONFIG_SECTIONS.map((section) => ({
    path: section.path,
    data: { section: section.path },
    loadComponent: () => import('./config/config-section-page').then((m) => m.ConfigSectionPage),
  })),
];

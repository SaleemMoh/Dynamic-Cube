import { Routes } from '@angular/router';
import { CONFIG_SECTIONS } from './config/config-types';

/** Sections with their own two-panel screens; the others use the generic configuration page. */
const OWN_SCREENS = ['roles', 'menus', 'organization'];

export const APPS_CONTROL_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'change-sets' },
  { path: 'change-sets', loadComponent: () => import('./change-sets/change-set-list').then((m) => m.ChangeSetList) },
  { path: 'change-sets/:id', loadComponent: () => import('./change-sets/change-set-detail').then((m) => m.ChangeSetDetail) },
  { path: 'branding', loadComponent: () => import('./branding/branding-page').then((m) => m.BrandingPage) },
  { path: 'classes', loadComponent: () => import('./classes/classes-page').then((m) => m.ClassesPage) },
  { path: 'classes/:code', loadComponent: () => import('./classes/classes-page').then((m) => m.ClassesPage) },
  { path: 'roles', loadComponent: () => import('./roles/roles-page').then((m) => m.RolesPage) },
  { path: 'roles/:code', loadComponent: () => import('./roles/roles-page').then((m) => m.RolesPage) },
  { path: 'organization', loadComponent: () => import('./organization/org-page').then((m) => m.OrgPage) },
  { path: 'menus', loadComponent: () => import('./menus/menus-page').then((m) => m.MenusPage) },
  { path: 'menus/:code', loadComponent: () => import('./menus/menus-page').then((m) => m.MenusPage) },
  { path: 'portal-accounts', loadComponent: () => import('./portal-accounts/portal-accounts-page').then((m) => m.PortalAccountsPage) },
  ...CONFIG_SECTIONS.filter((section) => !OWN_SCREENS.includes(section.path)).map((section) => ({
    path: section.path,
    data: { section: section.path },
    loadComponent: () => import('./config/config-section-page').then((m) => m.ConfigSectionPage),
  })),
];

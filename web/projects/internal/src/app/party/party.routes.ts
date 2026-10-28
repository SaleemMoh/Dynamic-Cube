import { Routes } from '@angular/router';
import { PARTY_SECTIONS } from '../apps-control/config/module-sections';

export const PARTY_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'parties' },
  { path: 'parties', loadComponent: () => import('./party-list').then((m) => m.PartyList) },
  { path: 'parties/:id', loadComponent: () => import('./party-detail').then((m) => m.PartyDetail) },
  ...PARTY_SECTIONS.map((section) => ({
    path: section.path,
    data: { section: section.path },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  })),
];

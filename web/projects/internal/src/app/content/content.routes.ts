import { Routes } from '@angular/router';

export const CONTENT_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  { path: 'home', loadComponent: () => import('./home-editor').then((m) => m.HomeEditor) },
  {
    path: 'faqs',
    data: { section: 'content-faqs' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  {
    path: 'pages',
    data: { section: 'content-pages' },
    loadComponent: () => import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  },
  { path: 'pages/designer', loadComponent: () => import('./page-designer/page-designer-page').then((m) => m.PageDesignerPage) },
  { path: 'feedback', loadComponent: () => import('./feedback-report').then((m) => m.FeedbackReportPage) },
];

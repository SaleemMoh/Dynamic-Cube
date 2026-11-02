import { Routes } from '@angular/router';
import { REPORT_SECTIONS } from '../apps-control/config/module-sections';

export const REPORTS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./report-list').then((m) => m.ReportList),
  },
  {
    path: 'view/:code',
    loadComponent: () => import('./report-viewer').then((m) => m.ReportViewer),
  },
  {
    path: 'dashboards',
    loadComponent: () => import('./dashboard-list').then((m) => m.DashboardList),
  },
  {
    path: 'dashboards/:code',
    loadComponent: () => import('./dashboard-viewer').then((m) => m.DashboardViewer),
  },
  {
    path: 'my-dashboards/:id',
    loadComponent: () => import('./dashboard-viewer').then((m) => m.DashboardViewer),
  },
  {
    path: 'deliveries',
    loadComponent: () => import('./report-deliveries').then((m) => m.ReportDeliveries),
  },
  ...REPORT_SECTIONS.map((section) => ({
    path: section.path.replace(/^reports-/, ''),
    data: { section: section.path },
    loadComponent: () =>
      import('../apps-control/config/config-section-page').then((m) => m.ConfigSectionPage),
  })),
];

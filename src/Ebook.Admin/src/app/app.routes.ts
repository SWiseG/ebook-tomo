import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'niches',
        loadComponent: () => import('./features/niches/niches').then((m) => m.Niches),
      },
      {
        path: 'products',
        loadComponent: () => import('./features/products/products').then((m) => m.Products),
      },
      {
        path: 'products/:id',
        loadComponent: () =>
          import('./features/products/product-detail').then((m) => m.ProductDetail),
      },
      {
        path: 'jobs',
        loadComponent: () => import('./features/jobs/jobs').then((m) => m.Jobs),
      },
      {
        path: 'channels',
        loadComponent: () => import('./features/channels/channels').then((m) => m.Channels),
      },
      {
        path: 'optimizer',
        loadComponent: () => import('./features/optimizer/optimizer').then((m) => m.Optimizer),
      },
      {
        path: 'media',
        loadComponent: () =>
          import('./features/media-telemetry/media-telemetry').then((m) => m.MediaTelemetry),
      },
      {
        path: 'logs',
        loadComponent: () => import('./features/logs/logs').then((m) => m.Logs),
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings').then((m) => m.Settings),
      },
      {
        path: 'tutorial',
        loadComponent: () => import('./features/tutorial/tutorial').then((m) => m.Tutorial),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

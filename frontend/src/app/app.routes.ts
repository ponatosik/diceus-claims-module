import { Routes } from '@angular/router';
import { NotFoundPage } from './core/not-found/not-found-page';

/** §11.4: one lazy-loaded route bundle per feature — the standalone-era form of a feature module. */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  {
    path: 'claims',
    loadChildren: () => import('./features/claims-list/claims-list.routes').then((m) => m.ROUTES),
  },
  {
    path: 'claims/new',
    loadChildren: () => import('./features/fnol-intake/fnol-intake.routes').then((m) => m.ROUTES),
  },
  {
    path: 'claims/:claimId',
    loadChildren: () => import('./features/claim-detail/claim-detail.routes').then((m) => m.ROUTES),
  },
  { path: 'not-found', component: NotFoundPage },
  { path: '**', component: NotFoundPage },
];

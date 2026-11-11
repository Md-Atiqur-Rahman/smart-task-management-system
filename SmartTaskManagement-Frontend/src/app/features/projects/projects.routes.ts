import { Routes } from '@angular/router';

/** Lazy-loaded project pages (any signed-in role; the API scopes what each role sees). */
export const routes: Routes = [
  {
    path: '',
    title: 'Projects · SmartTask',
    loadComponent: () => import('./pages/projects-list.page').then((m) => m.ProjectsListPage),
  },
  {
    path: ':id',
    title: 'Project · SmartTask',
    loadComponent: () => import('./pages/project-detail.page').then((m) => m.ProjectDetailPage),
  },
];

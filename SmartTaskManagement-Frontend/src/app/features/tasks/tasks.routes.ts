import { Routes } from '@angular/router';

/** Lazy-loaded task pages (any signed-in role; the API scopes what each role sees). */
export const routes: Routes = [
  {
    path: '',
    title: 'Tasks · SmartTask',
    loadComponent: () => import('./pages/tasks-list.page').then((m) => m.TasksListPage),
  },
  {
    path: ':id',
    title: 'Task · SmartTask',
    loadComponent: () => import('./pages/task-detail.page').then((m) => m.TaskDetailPage),
  },
];

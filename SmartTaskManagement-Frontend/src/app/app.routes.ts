import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/guards';

/** Every page is lazy-loaded; signed-in pages live inside the shell behind authGuard. */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · SmartTask',
    loadComponent: () => import('./features/auth/pages/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account · SmartTask',
    loadComponent: () => import('./features/auth/pages/register.page').then((m) => m.RegisterPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · SmartTask',
        loadComponent: () =>
          import('./features/dashboard/pages/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'projects',
        loadChildren: () => import('./features/projects/projects.routes').then((m) => m.routes),
      },
      {
        path: 'tasks',
        loadChildren: () => import('./features/tasks/tasks.routes').then((m) => m.routes),
      },
      {
        path: 'users',
        canActivate: [roleGuard('Admin')],
        title: 'Users · SmartTask',
        loadComponent: () => import('./features/users/pages/users.page').then((m) => m.UsersPage),
      },
    ],
  },
  {
    path: '**',
    title: 'Not found · SmartTask',
    loadComponent: () => import('./features/not-found.page').then((m) => m.NotFoundPage),
  },
];

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from '../models/api.models';
import { AuthStore } from './auth.store';

const HOME = '/dashboard';

/** Only same-app paths are accepted as a return URL (no absolute or protocol-relative URLs). */
export function safeReturnUrl(url: string | null | undefined): string {
  if (!url || !url.startsWith('/') || url.startsWith('//') || url.startsWith('/\\')) {
    return HOME;
  }
  return url;
}

/**
 * Signed-in users only. The session is restored from the refresh cookie before
 * the first navigation (app initializer), so a signed-out state here is final.
 * Guards are UX only: the API enforces every rule.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  if (inject(AuthStore).isSignedIn()) {
    return true;
  }
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Login and register pages: a signed-in user goes to the dashboard. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthStore).isSignedIn() ? inject(Router).parseUrl(HOME) : true;

/** Role-restricted pages; a wrong role sees "not found", like the API hides what you may not see. */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return () => {
    const role = inject(AuthStore).role();
    return role !== null && roles.includes(role) ? true : inject(Router).parseUrl('/not-found');
  };
}

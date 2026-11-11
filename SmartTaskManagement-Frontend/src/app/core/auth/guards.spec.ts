import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { AuthResponse, Role } from '../models/api.models';
import { AuthStore } from './auth.store';
import { authGuard, guestGuard, roleGuard, safeReturnUrl } from './guards';

function signIn(role: Role): void {
  const response: AuthResponse = {
    accessToken: 't',
    expiresAt: new Date(Date.now() + 600_000).toISOString(),
    user: { id: 'u1', fullName: 'Test User', email: 't@example.com', role },
  };
  TestBed.inject(AuthStore).setSession(response);
}

function run(guard: typeof authGuard, url = '/tasks/7'): ReturnType<typeof authGuard> {
  const state = { url } as RouterStateSnapshot;
  return TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, state));
}

function asUrl(result: ReturnType<typeof authGuard>): string {
  return (result as UrlTree).toString();
}

describe('safeReturnUrl', () => {
  it.each([
    ['https://evil.example', '/dashboard'],
    ['//evil.example', '/dashboard'],
    ['/\\evil.example', '/dashboard'],
    [null, '/dashboard'],
    ['', '/dashboard'],
    ['/tasks/1', '/tasks/1'],
    ['/projects?page=2', '/projects?page=2'],
  ])('%s -> %s', (input, expected) => {
    expect(safeReturnUrl(input)).toBe(expected);
  });
});

describe('route guards', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  it('authGuard_SignedOut_RedirectsToLoginWithReturnUrl', () => {
    expect(asUrl(run(authGuard))).toBe('/login?returnUrl=%2Ftasks%2F7');
  });

  it('authGuard_SignedIn_Allows', () => {
    signIn('TeamMember');
    expect(run(authGuard)).toBe(true);
  });

  it('guestGuard_SignedIn_RedirectsToDashboard', () => {
    signIn('Admin');
    expect(asUrl(run(guestGuard))).toBe('/dashboard');
  });

  it('roleGuard_WrongRole_GoesToNotFound', () => {
    signIn('TeamMember');
    expect(asUrl(run(roleGuard('Admin')))).toBe('/not-found');
  });

  it('roleGuard_RightRole_Allows', () => {
    signIn('Admin');
    expect(run(roleGuard('Admin'))).toBe(true);
  });
});

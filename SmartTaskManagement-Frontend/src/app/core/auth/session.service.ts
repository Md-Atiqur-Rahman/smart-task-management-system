import { Injectable, inject, signal } from '@angular/core';
import {
  Observable,
  catchError,
  finalize,
  firstValueFrom,
  map,
  of,
  shareReplay,
  switchMap,
  throwError,
  timeout,
  timer,
} from 'rxjs';
import { toApiProblem } from '../http/api-problem';
import { AuthResponse, CurrentUserDto, LoginRequest, RegisterRequest } from '../models/api.models';
import { AuthApi } from './auth-api.service';
import { AuthStore } from './auth.store';

/** Another tab rotated the refresh token less than 30 s ago (ADR 0016): retry once. */
const REFRESH_SUPERSEDED = 'auth.refresh_superseded';
const SUPERSEDED_RETRY_DELAY_MS = 500;
/** Refresh this long before the access token expires, so calls rarely hit 401. */
const PROACTIVE_REFRESH_MS = 60_000;
/**
 * Lower bound for the proactive timer: expiresAt is server time, so a client
 * clock far ahead of the server would otherwise refresh in a tight loop.
 */
const MIN_REFRESH_DELAY_MS = 30_000;
/** After a network or server error the cookie may still be valid: try again later. */
const TRANSIENT_RETRY_MS = 15_000;
const MAX_TIMER_MS = 2_147_483_647;
/** App start waits for the session restore; a hanging API must not leave a blank screen. */
const RESTORE_TIMEOUT_MS = 10_000;

/** Refresh answers that mean the refresh cookie is gone or invalid (end the session). */
function isSessionOver(status: number): boolean {
  return status === 400 || status === 401 || status === 403;
}

/** Session lifecycle: sign in, restore on reload, single-flight refresh, sign out. */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly api = inject(AuthApi);
  private readonly store = inject(AuthStore);

  /** True when a signed-in session ended unexpectedly (shows "Your session has ended"). */
  readonly sessionEnded = signal(false);

  private inFlight: Observable<boolean> | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | undefined;
  /** Bumped by logout: a refresh that started earlier must not sign the user back in. */
  private generation = 0;

  login(body: LoginRequest): Observable<CurrentUserDto> {
    return this.api.login(body).pipe(map((response) => this.start(response)));
  }

  register(body: RegisterRequest): Observable<CurrentUserDto> {
    return this.api.register(body).pipe(map((response) => this.start(response)));
  }

  /** On app start: true when the refresh cookie still holds a session. */
  restore(): Promise<boolean> {
    return firstValueFrom(
      this.attempt(false).pipe(timeout({ first: RESTORE_TIMEOUT_MS, with: () => of(false) })),
    );
  }

  /** One refresh shared by every caller while it runs; false means the session is over. */
  refresh(): Observable<boolean> {
    this.inFlight ??= this.attempt(true).pipe(
      finalize(() => (this.inFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.inFlight;
  }

  /** Revokes this device's refresh token; the local session is cleared even if the call fails. */
  async logout(): Promise<void> {
    this.generation++;
    try {
      await firstValueFrom(this.api.logout());
    } catch {
      // Expected when offline or the API is down; the local session still ends.
    } finally {
      this.end(false);
    }
  }

  private attempt(markEnded: boolean): Observable<boolean> {
    const generation = this.generation;
    return this.api.refresh().pipe(
      catchError((error: unknown) =>
        toApiProblem(error).errorCode === REFRESH_SUPERSEDED
          ? timer(SUPERSEDED_RETRY_DELAY_MS).pipe(switchMap(() => this.api.refresh()))
          : throwError(() => error),
      ),
      map((response) => {
        if (generation !== this.generation) {
          return false; // The user logged out while this refresh was running.
        }
        this.start(response);
        return true;
      }),
      catchError((error: unknown) => {
        if (generation !== this.generation) {
          return of(false);
        }
        if (isSessionOver(toApiProblem(error).status) || !this.store.isSignedIn()) {
          this.end(markEnded);
        } else {
          // Offline, 429 or 5xx: keep the session and try again later.
          this.schedule(TRANSIENT_RETRY_MS);
        }
        return of(false);
      }),
    );
  }

  private start(response: AuthResponse): CurrentUserDto {
    this.store.setSession(response);
    this.sessionEnded.set(false);
    this.scheduleRefresh(response.expiresAt);
    return response.user;
  }

  private end(markEnded: boolean): void {
    const wasSignedIn = this.store.isSignedIn();
    clearTimeout(this.refreshTimer);
    this.store.clear();
    this.sessionEnded.set(markEnded && wasSignedIn);
  }

  private scheduleRefresh(expiresAt: string): void {
    const delay = Date.parse(expiresAt) - Date.now() - PROACTIVE_REFRESH_MS;
    this.schedule(Number.isFinite(delay) ? delay : MIN_REFRESH_DELAY_MS);
  }

  private schedule(delay: number): void {
    clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(
      () => this.refresh().subscribe(),
      Math.min(Math.max(delay, MIN_REFRESH_DELAY_MS), MAX_TIMER_MS),
    );
  }
}

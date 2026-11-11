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
const MAX_TIMER_MS = 2_147_483_647;

/** Session lifecycle: sign in, restore on reload, single-flight refresh, sign out. */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly api = inject(AuthApi);
  private readonly store = inject(AuthStore);

  /** True when a signed-in session ended unexpectedly (shows "Your session has ended"). */
  readonly sessionEnded = signal(false);

  private inFlight: Observable<boolean> | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | undefined;

  login(body: LoginRequest): Observable<CurrentUserDto> {
    return this.api.login(body).pipe(map((response) => this.start(response)));
  }

  register(body: RegisterRequest): Observable<CurrentUserDto> {
    return this.api.register(body).pipe(map((response) => this.start(response)));
  }

  /** On app start: true when the refresh cookie still holds a session. */
  restore(): Promise<boolean> {
    return firstValueFrom(this.attempt(false));
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
    try {
      await firstValueFrom(this.api.logout());
    } catch {
      // Expected when offline or the API is down; the local session still ends.
    } finally {
      this.end(false);
    }
  }

  private attempt(markEnded: boolean): Observable<boolean> {
    return this.api.refresh().pipe(
      catchError((error: unknown) =>
        toApiProblem(error).errorCode === REFRESH_SUPERSEDED
          ? timer(SUPERSEDED_RETRY_DELAY_MS).pipe(switchMap(() => this.api.refresh()))
          : throwError(() => error),
      ),
      map((response) => {
        this.start(response);
        return true;
      }),
      catchError(() => {
        this.end(markEnded);
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
    clearTimeout(this.refreshTimer);
    const delay = Date.parse(expiresAt) - Date.now() - PROACTIVE_REFRESH_MS;
    if (Number.isFinite(delay)) {
      this.refreshTimer = setTimeout(
        () => this.refresh().subscribe(),
        Math.min(Math.max(delay, 0), MAX_TIMER_MS),
      );
    }
  }
}

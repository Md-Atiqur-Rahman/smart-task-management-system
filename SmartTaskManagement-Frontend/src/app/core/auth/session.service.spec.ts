import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthResponse } from '../models/api.models';
import { authTokenInterceptor } from './auth-token.interceptor';
import { AuthStore } from './auth.store';
import { errorInterceptor } from './error.interceptor';
import { refreshInterceptor } from './refresh.interceptor';
import { SessionService } from './session.service';

const API = 'https://api.test';
const REFRESH = `${API}/api/auth/refresh`;

function authResponse(token: string): AuthResponse {
  return {
    accessToken: token,
    expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
    user: { id: 'u1', fullName: 'Rafi Ahmed', email: 'r@example.com', role: 'ProjectManager' },
  };
}

const superseded = { status: 401, errorCode: 'auth.refresh_superseded' };
const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('SessionService', () => {
  let session: SessionService;
  let store: AuthStore;
  let backend: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: API },
        provideHttpClient(
          withInterceptors([errorInterceptor, authTokenInterceptor, refreshInterceptor]),
        ),
        provideHttpClientTesting(),
      ],
    });
    session = TestBed.inject(SessionService);
    store = TestBed.inject(AuthStore);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
    vi.useRealTimers();
  });

  it('restore_SignsIn_WhenRefreshSucceeds', async () => {
    const restored = session.restore();
    backend.expectOne(REFRESH).flush(authResponse('t1'));

    expect(await restored).toBe(true);
    expect(store.accessToken()).toBe('t1');
    expect(store.role()).toBe('ProjectManager');
    expect(session.sessionEnded()).toBe(false);
  });

  it('restore_StaysSignedOut_WithoutSessionEndedMessage', async () => {
    const restored = session.restore();
    backend.expectOne(REFRESH).flush(null, unauthorized);

    expect(await restored).toBe(false);
    expect(store.isSignedIn()).toBe(false);
    expect(session.sessionEnded()).toBe(false);
  });

  it('refresh_Superseded_RetriesOnce_ThenSucceeds', async () => {
    store.setSession(authResponse('old'));
    const result = firstValueFrom(session.refresh());

    backend.expectOne(REFRESH).flush(superseded, unauthorized);
    await vi.advanceTimersByTimeAsync(1000);
    backend.expectOne(REFRESH).flush(authResponse('new'));

    expect(await result).toBe(true);
    expect(store.accessToken()).toBe('new');
  });

  it('refresh_SupersededTwice_EndsSession', async () => {
    store.setSession(authResponse('old'));
    const result = firstValueFrom(session.refresh());

    backend.expectOne(REFRESH).flush(superseded, unauthorized);
    await vi.advanceTimersByTimeAsync(1000);
    backend.expectOne(REFRESH).flush(superseded, unauthorized);

    expect(await result).toBe(false);
    expect(store.isSignedIn()).toBe(false);
    expect(session.sessionEnded()).toBe(true);
  });

  it('refresh_ConcurrentCalls_ShareOneRequest', async () => {
    store.setSession(authResponse('old'));
    const a = firstValueFrom(session.refresh());
    const b = firstValueFrom(session.refresh());

    backend.expectOne(REFRESH).flush(authResponse('new'));

    expect(await a).toBe(true);
    expect(await b).toBe(true);
  });

  it('logout_ClearsSession_EvenWhenCallFails', async () => {
    store.setSession(authResponse('t'));
    const done = session.logout();

    backend
      .expectOne(`${API}/api/auth/logout`)
      .flush(null, { status: 503, statusText: 'Unavailable' });
    await done;

    expect(store.isSignedIn()).toBe(false);
    expect(store.user()).toBeNull();
  });
});

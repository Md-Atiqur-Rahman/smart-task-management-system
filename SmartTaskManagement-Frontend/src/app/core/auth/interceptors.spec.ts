import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { ApiError } from '../http/api-problem';
import { AuthResponse } from '../models/api.models';
import { AuthApi } from './auth-api.service';
import { authTokenInterceptor } from './auth-token.interceptor';
import { AuthStore } from './auth.store';
import { errorInterceptor } from './error.interceptor';
import { refreshInterceptor } from './refresh.interceptor';

const API = 'https://api.test';

function authResponse(token: string): AuthResponse {
  return {
    accessToken: token,
    expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
    user: { id: 'u1', fullName: 'Nusrat Jahan', email: 'n@example.com', role: 'TeamMember' },
  };
}

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let store: AuthStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: API },
        provideHttpClient(
          withInterceptors([errorInterceptor, authTokenInterceptor, refreshInterceptor]),
        ),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
    store.setSession(authResponse('old-token'));
  });

  afterEach(() => backend.verify());

  it('authTokenInterceptor_AddsBearer_OnlyForApiBaseUrl', () => {
    http.get(`${API}/api/projects`).subscribe();
    http.get('https://other.example/x').subscribe();

    const apiReq = backend.expectOne(`${API}/api/projects`);
    const otherReq = backend.expectOne('https://other.example/x');
    expect(apiReq.request.headers.get('Authorization')).toBe('Bearer old-token');
    expect(otherReq.request.headers.has('Authorization')).toBe(false);
    apiReq.flush({});
    otherReq.flush({});
  });

  it('AuthApi_UsesWithCredentials_OnlyOnAuthCalls', () => {
    const api = TestBed.inject(AuthApi);
    api.login({ email: 'a@b.c', password: 'x' }).subscribe();
    api.register({ email: 'a@b.c', fullName: 'A', password: 'x' }).subscribe();
    api.refresh().subscribe();
    api.logout().subscribe();
    http.get(`${API}/api/projects`).subscribe();

    for (const path of ['login', 'register', 'refresh', 'logout']) {
      const req = backend.expectOne(`${API}/api/auth/${path}`);
      expect(req.request.withCredentials).toBe(true);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush(path === 'logout' ? null : authResponse('t'));
    }
    const projects = backend.expectOne(`${API}/api/projects`);
    expect(projects.request.withCredentials).toBe(false);
    projects.flush({});
  });

  it('refreshInterceptor_ConcurrentUnauthorized_RefreshesOnce', () => {
    const results: unknown[] = [];
    for (const n of [1, 2, 3]) {
      http.get(`${API}/api/items/${n}`).subscribe((r) => results.push(r));
    }

    for (const n of [1, 2, 3]) {
      backend
        .expectOne(`${API}/api/items/${n}`)
        .flush(null, { status: 401, statusText: 'Unauthorized' });
    }
    backend.expectOne(`${API}/api/auth/refresh`).flush(authResponse('new-token'));

    for (const n of [1, 2, 3]) {
      const replay = backend.expectOne(`${API}/api/items/${n}`);
      expect(replay.request.headers.get('Authorization')).toBe('Bearer new-token');
      replay.flush({ n });
    }
    expect(results).toEqual([{ n: 1 }, { n: 2 }, { n: 3 }]);
  });

  it('refreshInterceptor_Late401AfterAnotherRefresh_ReplaysWithTheNewTokenWithoutRefreshing', () => {
    let result: unknown;
    http.get(`${API}/api/slow`).subscribe((r) => (result = r));
    const slow = backend.expectOne(`${API}/api/slow`);
    expect(slow.request.headers.get('Authorization')).toBe('Bearer old-token');

    // Another call's refresh finished while this request was still on its way.
    store.setSession(authResponse('new-token'));
    slow.flush(null, { status: 401, statusText: 'Unauthorized' });

    backend.expectNone(`${API}/api/auth/refresh`);
    const replay = backend.expectOne(`${API}/api/slow`);
    expect(replay.request.headers.get('Authorization')).toBe('Bearer new-token');
    replay.flush({ ok: true });
    expect(result).toEqual({ ok: true });
  });

  it('refreshInterceptor_DoesNotRefresh_ForAuthEndpoints', async () => {
    const login = firstValueFrom(TestBed.inject(AuthApi).login({ email: 'a@b.c', password: 'x' }));

    backend
      .expectOne(`${API}/api/auth/login`)
      .flush(
        { status: 401, errorCode: 'auth.invalid_credentials' },
        { status: 401, statusText: 'Unauthorized' },
      );

    const error = await login.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).problem.errorCode).toBe('auth.invalid_credentials');
    backend.expectNone(`${API}/api/auth/refresh`);
  });

  it('refreshFailure_ReturnsOriginalError_AndClearsSession', async () => {
    const call = firstValueFrom(http.get(`${API}/api/projects`));

    backend
      .expectOne(`${API}/api/projects`)
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    backend
      .expectOne(`${API}/api/auth/refresh`)
      .flush(
        { status: 401, errorCode: 'auth.invalid_refresh_token' },
        { status: 401, statusText: 'Unauthorized' },
      );

    const error = await call.catch((e: unknown) => e);
    expect((error as ApiError).problem.status).toBe(401);
    expect(store.isSignedIn()).toBe(false);
  });
});

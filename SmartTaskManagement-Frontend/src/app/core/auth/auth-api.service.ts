import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/api.models';
import { AUTH_PATHS } from './auth-endpoints';

/**
 * Auth endpoints. They use `withCredentials` because the refresh token lives
 * only in the HttpOnly cookie: login and register set it, refresh and logout send it.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.baseUrl + AUTH_PATHS.login, body, {
      withCredentials: true,
    });
  }

  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.baseUrl + AUTH_PATHS.register, body, {
      withCredentials: true,
    });
  }

  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.baseUrl + AUTH_PATHS.refresh, null, {
      withCredentials: true,
    });
  }

  logout(): Observable<void> {
    return this.http.post<void>(this.baseUrl + AUTH_PATHS.logout, null, {
      withCredentials: true,
    });
  }
}

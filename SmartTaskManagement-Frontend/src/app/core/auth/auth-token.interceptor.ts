import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../config/api-base-url';
import { isApiUrl, isAuthEndpoint } from './auth-endpoints';
import { AuthStore } from './auth.store';

/** Adds the bearer token, only to API calls (never to other hosts or the cookie auth endpoints). */
export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);
  const token = inject(AuthStore).accessToken();
  if (!token || !isApiUrl(req.url, baseUrl) || isAuthEndpoint(req.url, baseUrl)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};

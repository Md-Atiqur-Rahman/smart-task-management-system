import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { isApiUrl, isAuthEndpoint } from './auth-endpoints';
import { AuthStore } from './auth.store';
import { SessionService } from './session.service';

/**
 * On a 401 from an API call: one refresh shared by all concurrent requests
 * (SessionService.refresh), then each request is replayed once with the new token.
 * If the refresh fails, the original 401 is passed on and the session is over.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);
  if (!isApiUrl(req.url, baseUrl) || isAuthEndpoint(req.url, baseUrl)) {
    return next(req);
  }
  const session = inject(SessionService);
  const store = inject(AuthStore);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== HttpStatusCode.Unauthorized) {
        return throwError(() => error);
      }
      return session
        .refresh()
        .pipe(
          switchMap((refreshed) =>
            refreshed
              ? next(req.clone({ setHeaders: { Authorization: `Bearer ${store.accessToken()}` } }))
              : throwError(() => error),
          ),
        );
    }),
  );
};

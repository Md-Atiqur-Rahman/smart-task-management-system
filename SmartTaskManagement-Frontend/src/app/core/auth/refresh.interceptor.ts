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
 * A 401 for a request sent with an older token (another refresh finished meanwhile)
 * is replayed with the current token without starting a second refresh.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);
  if (!isApiUrl(req.url, baseUrl) || isAuthEndpoint(req.url, baseUrl)) {
    return next(req);
  }
  const session = inject(SessionService);
  const store = inject(AuthStore);
  const sentWith = store.accessToken();

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== HttpStatusCode.Unauthorized) {
        return throwError(() => error);
      }
      const current = store.accessToken();
      if (current && current !== sentWith) {
        return next(req.clone({ setHeaders: { Authorization: `Bearer ${current}` } }));
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

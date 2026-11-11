import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { authTokenInterceptor } from './core/auth/auth-token.interceptor';
import { errorInterceptor } from './core/auth/error.interceptor';
import { refreshInterceptor } from './core/auth/refresh.interceptor';
import { SessionService } from './core/auth/session.service';
import { API_BASE_URL } from './core/config/api-base-url';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: errorInterceptor is outermost, so refreshInterceptor sees raw 401s
    // and pages receive the final ApiError.
    provideHttpClient(
      withInterceptors([errorInterceptor, authTokenInterceptor, refreshInterceptor]),
    ),
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    // Restore the session from the HttpOnly refresh cookie before the first navigation.
    provideAppInitializer(() => inject(SessionService).restore()),
  ],
};

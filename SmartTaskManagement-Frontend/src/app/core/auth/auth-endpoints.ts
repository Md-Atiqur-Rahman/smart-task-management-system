/** The four cookie-based auth endpoints (the only calls sent with credentials). */
export const AUTH_PATHS = {
  login: '/api/auth/login',
  register: '/api/auth/register',
  refresh: '/api/auth/refresh',
  logout: '/api/auth/logout',
} as const;

export function isAuthEndpoint(url: string, apiBaseUrl: string): boolean {
  return Object.values(AUTH_PATHS).some((path) => url === apiBaseUrl + path);
}

export function isApiUrl(url: string, apiBaseUrl: string): boolean {
  return url === apiBaseUrl || url.startsWith(`${apiBaseUrl}/`);
}

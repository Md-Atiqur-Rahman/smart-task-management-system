import { InjectionToken } from '@angular/core';

/** Base URL of the API (from the environment file), e.g. https://localhost:7254. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

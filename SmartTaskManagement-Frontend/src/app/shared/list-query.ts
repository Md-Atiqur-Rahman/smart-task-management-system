import { HttpParams } from '@angular/common/http';
import { ParamMap } from '@angular/router';

/** A list page's state: flat strings, numbers and booleans (kept in the URL query string). */
export type ListQuery = Record<string, string | number | boolean>;

/** URL query params for a list state: values equal to the defaults or empty are left out. */
export function toQueryParams<T extends ListQuery>(query: T, defaults: T): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(query)) {
    if (value === '' || value === false || value === defaults[key]) {
      continue;
    }
    result[key] = String(value);
  }
  return result;
}

/** Reads a list state from the URL; missing or invalid values take the default. */
export function readListQuery<T extends ListQuery>(params: ParamMap, defaults: T): T {
  const result: ListQuery = { ...defaults };
  for (const [key, fallback] of Object.entries(defaults)) {
    const raw = params.get(key);
    if (raw === null) {
      continue;
    }
    if (typeof fallback === 'number') {
      const parsed = Number(raw);
      result[key] = Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
    } else if (typeof fallback === 'boolean') {
      result[key] = raw === 'true';
    } else {
      result[key] = raw;
    }
  }
  return result as T;
}

/** API query params: the API's PascalCase names (Search, SortBy, Page...), empty values skipped. */
export function toApiParams(query: Readonly<Record<string, unknown>>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '' || value === false) {
      continue;
    }
    params = params.set(key[0].toUpperCase() + key.slice(1), String(value));
  }
  return params;
}

export function totalPages(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / pageSize));
}

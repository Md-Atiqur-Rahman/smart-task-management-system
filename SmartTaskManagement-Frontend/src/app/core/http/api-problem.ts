import { HttpErrorResponse } from '@angular/common/http';

/**
 * A failed API call in one shape. `errorCode` and `traceId` are ProblemDetails
 * extension members the API sends but openapi.json does not declare, so both
 * are optional. Status 0 = network failure, -1 = not an HTTP error.
 */
export interface ApiProblem {
  status: number;
  errorCode?: string;
  title?: string;
  detail?: string;
  traceId?: string;
  /** Validation messages keyed by camelCase field name (form control name). */
  fieldErrors: Record<string, string[]>;
}

/** Thrown into the observable chain by the error interceptor. */
export class ApiError extends Error {
  constructor(readonly problem: ApiProblem) {
    super(problem.title ?? `HTTP ${problem.status}`);
    this.name = 'ApiError';
  }
}

export function toApiProblem(error: unknown): ApiProblem {
  if (error instanceof ApiError) {
    return error.problem;
  }
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, fieldErrors: {} };
  }
  const body: unknown = error.error;
  if (!isRecord(body)) {
    return { status: error.status, fieldErrors: {} };
  }
  return {
    status: error.status,
    errorCode: stringOrUndefined(body['errorCode']),
    title: stringOrUndefined(body['title']),
    detail: stringOrUndefined(body['detail']),
    traceId: stringOrUndefined(body['traceId']),
    fieldErrors: readFieldErrors(body['errors']),
  };
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (!isRecord(value)) {
    return result;
  }
  for (const [key, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      result[camelCase(key)] = messages.filter((m): m is string => typeof m === 'string');
    }
  }
  return result;
}

function camelCase(key: string): string {
  return key.length === 0 ? key : key[0].toLowerCase() + key.slice(1);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function stringOrUndefined(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

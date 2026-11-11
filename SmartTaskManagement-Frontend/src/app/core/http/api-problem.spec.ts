import { HttpErrorResponse } from '@angular/common/http';
import { toApiProblem } from './api-problem';
import { messageFor } from './error-messages';

function httpError(status: number, body: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: body });
}

describe('toApiProblem', () => {
  it('reads errorCode, traceId and field errors from ProblemDetails', () => {
    const problem = toApiProblem(
      httpError(400, {
        title: 'Validation failed',
        status: 400,
        errorCode: 'validation.failed',
        traceId: 'abc-123',
        errors: { Title: ['Title is required.'] },
      }),
    );

    expect(problem.status).toBe(400);
    expect(problem.errorCode).toBe('validation.failed');
    expect(problem.traceId).toBe('abc-123');
    expect(problem.fieldErrors).toEqual({ title: ['Title is required.'] });
  });

  it('maps a network failure to status 0 without an error code', () => {
    const problem = toApiProblem(httpError(0, new ProgressEvent('error')));

    expect(problem.status).toBe(0);
    expect(problem.errorCode).toBeUndefined();
    expect(problem.fieldErrors).toEqual({});
  });

  it('handles a non-HTTP error as an unknown failure', () => {
    expect(toApiProblem(new Error('boom')).status).toBe(-1);
  });
});

describe('messageFor', () => {
  const cases: [number, string | undefined, string][] = [
    [401, 'auth.invalid_credentials', 'Email or password is incorrect.'],
    [409, 'concurrency.conflict', 'Someone else changed this. Reload to see the latest version.'],
    [
      409,
      'users.owns_projects',
      'This user still owns projects. Give them to another Project Manager first.',
    ],
    [409, 'users.last_admin', 'There must be at least one Admin.'],
    [
      403,
      'auth.origin_not_allowed',
      "This app's address is not allowed by the server (CORS settings).",
    ],
    [400, 'ai.rejected', 'The AI service declined this text. Rewrite it and try again.'],
    [503, 'ai.unavailable', 'The AI service is unavailable. Try again later.'],
    [503, 'ai.not_configured', 'The AI service is not set up on this server.'],
    [429, undefined, 'Too many requests. Wait a minute and try again.'],
    [503, undefined, 'The service is temporarily unavailable. Please try again.'],
    [0, undefined, "Can't reach the server. Check your connection."],
    [500, undefined, 'Something went wrong. Please try again.'],
  ];

  it.each(cases)('status %i with code %s', (status, errorCode, expected) => {
    expect(messageFor({ status, errorCode, fieldErrors: {} })).toBe(expected);
  });
});

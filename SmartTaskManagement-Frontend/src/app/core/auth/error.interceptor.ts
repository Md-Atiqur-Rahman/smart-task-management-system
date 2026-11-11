import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError, toApiProblem } from '../http/api-problem';

/**
 * Outermost interceptor: turns every final HTTP failure into an ApiError
 * (ProblemDetails with errorCode), so pages never handle raw HttpErrorResponse.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) =>
      throwError(() =>
        error instanceof HttpErrorResponse ? new ApiError(toApiProblem(error)) : error,
      ),
    ),
  );

import { Observable, catchError, map, of, startWith } from 'rxjs';
import { ApiProblem, toApiProblem } from '../core/http/api-problem';
import { messageFor } from '../core/http/error-messages';

/** What a page shows while data loads: a skeleton, the data, or an error with its message. */
export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; problem: ApiProblem; message: string };

export function loadState<T>(source: Observable<T>): Observable<LoadState<T>> {
  return source.pipe(
    map((data): LoadState<T> => ({ status: 'ready', data })),
    catchError((error: unknown) => {
      const problem = toApiProblem(error);
      return of<LoadState<T>>({ status: 'error', problem, message: messageFor(problem) });
    }),
    startWith<LoadState<T>>({ status: 'loading' }),
  );
}

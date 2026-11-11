import { Observable, OperatorFunction, catchError, map, of, scan, startWith } from 'rxjs';
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

/**
 * On a reload (manual or live), keep showing the last data instead of going
 * back to "loading": pages do not blank and embedded components keep their state.
 */
export function keepDataWhileLoading<T>(): OperatorFunction<LoadState<T>, LoadState<T>> {
  return scan(
    (previous: LoadState<T> | null, next: LoadState<T>) =>
      next.status === 'loading' && previous?.status === 'ready' ? previous : next,
    null,
  ) as OperatorFunction<LoadState<T>, LoadState<T>>;
}

import { Subject, switchMap } from 'rxjs';
import { LoadState, keepDataWhileLoading, loadState } from './load-state';

describe('keepDataWhileLoading (final review, Important 1)', () => {
  it('keeps the last data while a reload runs, so pages do not blank and children keep their state', () => {
    const trigger = new Subject<number>();
    const results = new Subject<number>();
    const seen: LoadState<number>[] = [];
    trigger
      .pipe(
        switchMap(() => loadState(results.pipe())),
        keepDataWhileLoading(),
      )
      .subscribe((state) => seen.push(state));

    trigger.next(1);
    results.next(10);
    trigger.next(2);
    results.next(20);

    expect(seen.map((s) => s.status)).toEqual(['loading', 'ready', 'ready', 'ready']);
    expect(seen.at(-1)).toEqual({ status: 'ready', data: 20 });
  });

  it('still shows loading on the first load', () => {
    const seen: string[] = [];
    const trigger = new Subject<LoadState<number>>();
    trigger.pipe(keepDataWhileLoading()).subscribe((s) => seen.push(s.status));

    trigger.next({ status: 'loading' });

    expect(seen).toEqual(['loading']);
  });
});

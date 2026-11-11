import { Injectable, signal } from '@angular/core';
import { Observable, Subject, merge } from 'rxjs';

export type LiveStatus = 'off' | 'live' | 'reconnecting';

/**
 * Live dashboard hook (PRD 13). The SSE client calls notifyChanged() on a
 * `dashboard-changed` event or after a reconnect; only the dashboard listens
 * (decision 3: lists do not refresh by themselves).
 */
@Injectable({ providedIn: 'root' })
export class DashboardRefresh {
  private readonly changed$ = new Subject<void>();

  /** Connection state shown on the dashboard ("Live" / "Reconnecting…"). */
  readonly status = signal<LiveStatus>('off');

  notifyChanged(): void {
    this.changed$.next();
  }

  /** Emits on each manual reload and each live change. */
  changes(manual: Observable<void>): Observable<void> {
    return merge(manual, this.changed$);
  }
}

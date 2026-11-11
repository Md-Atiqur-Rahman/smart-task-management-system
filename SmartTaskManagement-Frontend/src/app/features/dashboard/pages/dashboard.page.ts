import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { DashboardRefresh } from '../../../core/realtime/dashboard-refresh';
import { PriorityBadgeComponent, StatusBadgeComponent } from '../../../shared/badges.component';
import { dueLabel, formatDateOnly, utcToday } from '../../../shared/date-utils';
import { LoadState, loadState } from '../../../shared/load-state';
import {
  DashboardApi,
  DashboardSummaryDto,
  UpcomingTaskDto,
} from '../data-access/dashboard-api.service';

interface Bar {
  label: string;
  count: number;
  percent: number;
  color: string;
}

/** Dashboard (prototype D02): totals, status and priority, completed vs pending, upcoming. */
@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, PriorityBadgeComponent, StatusBadgeComponent],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly api = inject(DashboardApi);
  private readonly store = inject(AuthStore);
  private readonly refresh = inject(DashboardRefresh);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly summary = toSignal(
    this.refresh.changes(this.reload$).pipe(switchMap(() => loadState(this.api.summary()))),
    { initialValue: { status: 'loading' } as LoadState<DashboardSummaryDto> },
  );
  protected readonly upcoming = toSignal(
    this.refresh.changes(this.reload$).pipe(switchMap(() => loadState(this.api.upcoming()))),
    { initialValue: { status: 'loading' } as LoadState<UpcomingTaskDto[]> },
  );
  protected readonly live = this.refresh.status;
  protected readonly date = formatDateOnly;
  private readonly today = utcToday();

  protected readonly scope = computed(() => {
    switch (this.store.role()) {
      case 'Admin':
        return 'The whole system';
      case 'ProjectManager':
        return 'Your projects and the tasks assigned to you';
      default:
        return 'The tasks assigned to you';
    }
  });

  protected statusBars(s: DashboardSummaryDto): Bar[] {
    const t = s.tasksByStatus;
    return this.bars(s.totalTasks, [
      ['To Do', t.toDo, '#64748b'],
      ['In Progress', t.inProgress, '#2563eb'],
      ['Completed', t.completed, '#0f766e'],
      ['Cancelled', t.cancelled, '#9ca3af'],
    ]);
  }

  protected priorityBars(s: DashboardSummaryDto): Bar[] {
    const p = s.tasksByPriority;
    return this.bars(s.totalTasks, [
      ['Critical', p.critical, '#b91c1c'],
      ['High', p.high, '#f97316'],
      ['Medium', p.medium, '#6366f1'],
      ['Low', p.low, '#94a3b8'],
    ]);
  }

  protected donePercent(s: DashboardSummaryDto): number {
    const base = s.completedTasks + s.pendingTasks;
    return base ? Math.round((s.completedTasks / base) * 100) : 0;
  }

  protected due(task: UpcomingTaskDto): string | null {
    return dueLabel(task.dueDate, this.today, task.status);
  }

  protected retry(): void {
    this.reload$.next();
  }

  private bars(total: number, rows: [string, number, string][]): Bar[] {
    return rows.map(([label, count, color]) => ({
      label,
      count,
      color,
      percent: total ? Math.round((count / total) * 100) : 0,
    }));
  }
}

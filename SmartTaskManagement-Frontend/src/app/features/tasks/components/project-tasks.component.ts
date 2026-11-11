import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { BehaviorSubject, combineLatest, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { PagedResult } from '../../../core/models/api.models';
import { LoadState, loadState } from '../../../shared/load-state';
import { PagerComponent } from '../../../shared/pager.component';
import { TasksApi } from '../data-access/tasks-api.service';
import { TaskListItemDto } from '../models/task.models';
import { TASK_FILTER_DEFAULTS, TaskFilterState, toTaskListQuery } from '../task-query';
import { TaskFiltersComponent } from './task-filters.component';
import { TaskFormDialogComponent } from './task-form.dialog';
import { TaskTableComponent } from './task-table.component';

/** The task list inside a project page (prototype D05), with Add task for those who may. */
@Component({
  selector: 'app-project-tasks',
  imports: [TaskFiltersComponent, TaskTableComponent, PagerComponent, TaskFormDialogComponent],
  template: `
    <section class="card" aria-labelledby="project-tasks-title">
      <div class="head">
        <h2 id="project-tasks-title">Tasks in this project</h2>
        @if (canAdd()) {
          <button type="button" class="btn btn-primary btn-sm" (click)="creating.set(true)">
            Add task
          </button>
        }
      </div>
      <app-task-filters
        [value]="filters()"
        [canPickAssignee]="canPickAssignee()"
        (changed)="patch($event)"
        (clear)="filters.set(defaults)"
      />
      @let s = state();
      @if (s.status === 'loading') {
        <p class="empty muted" role="status">Loading tasks…</p>
      } @else if (s.status === 'error') {
        <div class="empty">
          <p class="alert alert-error" role="alert">{{ s.message }}</p>
        </div>
      } @else {
        @if (s.data.items.length === 0) {
          <p class="empty muted">No tasks match these filters.</p>
        } @else {
          <app-task-table
            [tasks]="s.data.items"
            [projectOwnerId]="projectOwnerId()"
            (statusChanged)="onStatusChanged()"
          />
        }
        <app-pager
          noun="tasks"
          [page]="s.data.page"
          [pageSize]="s.data.pageSize"
          [totalCount]="s.data.totalCount"
          (pageChange)="patch({ page: $event })"
          (pageSizeChange)="patch({ pageSize: $event, page: 1 })"
        />
      }
    </section>
    @if (creating()) {
      <app-task-form-dialog
        [projectId]="projectId()"
        (saved)="onCreated($event)"
        (closed)="creating.set(false)"
      />
    }
  `,
  styles: `
    .head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      padding: 14px 20px 0;
    }
    .head h2 {
      font-size: 16px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectTasksComponent {
  private readonly api = inject(TasksApi);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  readonly projectId = input.required<string>();
  readonly projectOwnerId = input.required<string>();
  readonly canAdd = input(false);
  /** Counts on the project page change when tasks change. */
  readonly changed = output<void>();

  protected readonly defaults = TASK_FILTER_DEFAULTS;
  protected readonly filters = signal<TaskFilterState>(TASK_FILTER_DEFAULTS);
  protected readonly creating = signal(false);
  protected readonly canPickAssignee = computed(() => this.store.role() !== 'TeamMember');
  protected readonly state = toSignal(
    combineLatest([toObservable(this.projectId), toObservable(this.filters), this.reload$]).pipe(
      switchMap(([projectId, filters]) =>
        loadState(
          this.api.listForProject(projectId, toTaskListQuery(filters, this.store.user()?.id ?? '')),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<TaskListItemDto>> },
  );

  protected patch(change: Partial<TaskFilterState>): void {
    this.filters.update((current) => ({ ...current, ...change }));
  }

  protected onStatusChanged(): void {
    this.changed.emit();
  }

  protected onCreated(taskId: string): void {
    this.creating.set(false);
    void this.router.navigate(['/tasks', taskId]);
  }
}

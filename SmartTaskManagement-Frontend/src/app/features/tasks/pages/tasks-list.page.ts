import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { PagedResult } from '../../../core/models/api.models';
import { readListQuery, toQueryParams } from '../../../shared/list-query';
import { LoadState, loadState } from '../../../shared/load-state';
import { PagerComponent } from '../../../shared/pager.component';
import { TaskFiltersComponent } from '../components/task-filters.component';
import { TaskTableComponent } from '../components/task-table.component';
import { ProjectsApi } from '../../projects/data-access/projects-api.service';
import { TasksApi } from '../data-access/tasks-api.service';
import { TaskListItemDto } from '../models/task.models';
import {
  TASK_FILTER_DEFAULTS,
  TaskFilterState,
  TaskProjectOption,
  toTaskListQuery,
} from '../task-query';

/** The API's largest page (Paging:MaxPageSize); the project filter lists at most this many, by name. */
const PROJECT_OPTIONS_LIMIT = 100;

/**
 * Tasks across all visible projects (prototype D06). The list DTO has no project
 * owner, so a row offers the status control only to the assignee (and Admins);
 * edit, assign and delete are on the task page (decision 1). Filters live in the URL.
 */
@Component({
  selector: 'app-tasks-list-page',
  imports: [TaskFiltersComponent, TaskTableComponent, PagerComponent],
  template: `
    <div class="page">
      <div class="page-head">
        <h1>Tasks</h1>
      </div>
      <p class="muted">{{ scope() }}</p>
      <section class="card" aria-label="Tasks">
        <app-task-filters
          [value]="query()"
          [canPickAssignee]="canPickAssignee()"
          [projects]="projects()"
          (changed)="update($event)"
          (clear)="clear()"
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
            <p class="empty muted">
              {{
                isTeamMember()
                  ? 'Nothing matches, or nothing is assigned to you yet.'
                  : 'No tasks match these filters.'
              }}
            </p>
          } @else {
            <app-task-table [tasks]="s.data.items" [showProject]="true" />
          }
          <app-pager
            noun="tasks"
            [page]="s.data.page"
            [pageSize]="s.data.pageSize"
            [totalCount]="s.data.totalCount"
            (pageChange)="update({ page: $event })"
            (pageSizeChange)="update({ pageSize: $event, page: 1 })"
          />
        }
      </section>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksListPage {
  private readonly api = inject(TasksApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly store = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isTeamMember = computed(() => this.store.role() === 'TeamMember');
  protected readonly canPickAssignee = computed(() => !this.isTeamMember());
  protected readonly scope = computed(() => {
    switch (this.store.role()) {
      case 'Admin':
        return 'All tasks in all projects.';
      case 'ProjectManager':
        return 'Tasks in your projects and tasks assigned to you. Open a task to edit, assign or delete it.';
      default:
        return 'Tasks assigned to you. You can change their status.';
    }
  });
  /** Visible projects for the project filter; a failed load only hides the filter. */
  protected readonly projects = toSignal(
    this.projectsApi
      .list({
        sortBy: 'name',
        sortDirection: 'Ascending',
        page: 1,
        pageSize: PROJECT_OPTIONS_LIMIT,
      })
      .pipe(
        map((page): readonly TaskProjectOption[] =>
          page.items.map((p) => ({ id: p.id, name: p.name })),
        ),
        catchError(() => of([] as readonly TaskProjectOption[])),
      ),
    { initialValue: [] as readonly TaskProjectOption[] },
  );
  protected readonly query = toSignal(
    this.route.queryParamMap.pipe(map((params) => readListQuery(params, TASK_FILTER_DEFAULTS))),
    { initialValue: TASK_FILTER_DEFAULTS },
  );
  protected readonly state = toSignal(
    this.route.queryParamMap.pipe(
      map((params) => readListQuery(params, TASK_FILTER_DEFAULTS)),
      switchMap((q) => loadState(this.api.list(toTaskListQuery(q, this.store.user()?.id ?? '')))),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<TaskListItemDto>> },
  );

  protected update(patch: Partial<TaskFilterState>): void {
    const next = { ...this.query(), ...patch };
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(next, TASK_FILTER_DEFAULTS),
    });
  }

  protected clear(): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }
}

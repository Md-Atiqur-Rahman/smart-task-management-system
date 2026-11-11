import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, map, of, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { PriorityBadgeComponent, StatusBadgeComponent } from '../../../shared/badges.component';
import { dueLabel, formatDateOnly, utcToday } from '../../../shared/date-utils';
import { LoadState, loadState } from '../../../shared/load-state';
import { ConfirmDialogComponent } from '../../../shared/modal.component';
import { ProjectsApi } from '../../projects/data-access/projects-api.service';
import { AssignDialogComponent } from '../components/assign.dialog';
import { StatusSelectComponent } from '../components/status-select.component';
import { TaskFormDialogComponent } from '../components/task-form.dialog';
import { TasksApi } from '../data-access/tasks-api.service';
import { TaskDetailsDto } from '../models/task.models';
import { canChangeStatus, canManageTask } from '../task-permissions';

interface TaskView {
  task: TaskDetailsDto;
  /** From GET /api/projects/{id}; null when the caller cannot see the project's owner. */
  projectOwnerId: string | null;
}

/** Task detail (prototype D07). Notifications link here. */
@Component({
  selector: 'app-task-detail-page',
  imports: [
    DatePipe,
    RouterLink,
    PriorityBadgeComponent,
    StatusBadgeComponent,
    StatusSelectComponent,
    ConfirmDialogComponent,
    TaskFormDialogComponent,
    AssignDialogComponent,
  ],
  templateUrl: './task-detail.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskDetailPage {
  private readonly tasks = inject(TasksApi);
  private readonly projects = inject(ProjectsApi);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  readonly id = input.required<string>();

  protected readonly state = toSignal(
    combineLatest([toObservable(this.id), this.reload$]).pipe(
      switchMap(([id]) =>
        loadState(
          this.tasks.get(id).pipe(
            switchMap((task) =>
              this.projects.get(task.project.id).pipe(
                map((project): TaskView => ({ task, projectOwnerId: project.owner.id })),
                catchError(() => of<TaskView>({ task, projectOwnerId: null })),
              ),
            ),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState<TaskView> },
  );
  private readonly me = computed(() => this.store.user()?.id ?? '');
  protected readonly date = formatDateOnly;
  protected readonly editing = signal(false);
  protected readonly assigning = signal(false);
  protected readonly deleting = signal(false);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);

  protected canManage(view: TaskView): boolean {
    const role = this.store.role();
    return role !== null && canManageTask(role, this.me(), view.projectOwnerId);
  }

  protected canStatus(view: TaskView): boolean {
    const role = this.store.role();
    return (
      role !== null &&
      canChangeStatus(role, this.me(), view.task.assignee?.id ?? null, view.projectOwnerId)
    );
  }

  protected due(task: TaskDetailsDto): string | null {
    return dueLabel(task.dueDate, utcToday(), task.status);
  }

  protected done(text: string): void {
    this.editing.set(false);
    this.assigning.set(false);
    this.message.set(text);
    this.reload$.next();
  }

  protected unassign(task: TaskDetailsDto): void {
    this.busy.set(true);
    this.tasks.assign(task.id, null).subscribe({
      next: () => {
        this.busy.set(false);
        this.done('Task unassigned.');
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.actionError.set(messageFor(toApiProblem(err)));
      },
    });
  }

  protected confirmDelete(task: TaskDetailsDto): void {
    this.busy.set(true);
    this.tasks.remove(task.id).subscribe({
      next: () => void this.router.navigate(['/projects', task.project.id]),
      error: (err: unknown) => {
        this.busy.set(false);
        this.deleting.set(false);
        this.actionError.set(messageFor(toApiProblem(err)));
      },
    });
  }
}

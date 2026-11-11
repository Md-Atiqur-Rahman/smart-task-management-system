import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { TaskItemStatus } from '../../../core/models/api.models';
import { PriorityBadgeComponent, StatusBadgeComponent } from '../../../shared/badges.component';
import { dueLabel, formatDateOnly, utcToday } from '../../../shared/date-utils';
import { TaskListItemDto } from '../models/task.models';
import { canChangeStatus } from '../task-permissions';
import { StatusSelectComponent } from './status-select.component';

/**
 * Task rows (prototype D05, D06). A status control appears where the user may
 * change it; edit, assign and delete live on the task page.
 */
@Component({
  selector: 'app-task-table',
  imports: [RouterLink, StatusSelectComponent, StatusBadgeComponent, PriorityBadgeComponent],
  template: `
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th scope="col">Task</th>
            <th scope="col">Assignee</th>
            <th scope="col">Priority</th>
            <th scope="col">Status</th>
            <th scope="col">Due</th>
          </tr>
        </thead>
        <tbody>
          @for (task of tasks(); track task.id) {
            <tr>
              <td>
                <a class="strong" [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
                @if (showProject()) {
                  <div class="muted small">{{ task.projectName }}</div>
                }
              </td>
              <td class="nowrap">
                @if (task.assigneeName) {
                  {{ task.assigneeName }}{{ task.assigneeId === me() ? ' (you)' : '' }}
                } @else {
                  <span class="muted">Unassigned</span>
                }
              </td>
              <td><app-priority-badge [priority]="task.priority" /></td>
              <td>
                @if (editable(task)) {
                  <app-status-select
                    [taskId]="task.id"
                    [taskTitle]="task.title"
                    [status]="task.status"
                    (changed)="statusChanged.emit($event)"
                  />
                } @else {
                  <app-status-badge [status]="task.status" />
                }
              </td>
              <td class="nowrap">
                {{ date(task.dueDate) }}
                @if (due(task); as label) {
                  <div [class.overdue]="label !== 'Tomorrow'" [class.muted]="label === 'Tomorrow'">
                    {{ label }}
                  </div>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: `
    .small {
      font-size: 13px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskTableComponent {
  private readonly store = inject(AuthStore);

  readonly tasks = input.required<TaskListItemDto[]>();
  /** Known on the project page; null in the all-tasks list (decision 1). */
  readonly projectOwnerId = input<string | null>(null);
  readonly showProject = input(false);
  readonly statusChanged = output<TaskItemStatus>();

  protected readonly me = computed(() => this.store.user()?.id ?? '');
  private readonly today = utcToday();
  protected readonly date = formatDateOnly;

  protected editable(task: TaskListItemDto): boolean {
    const role = this.store.role();
    return (
      role !== null && canChangeStatus(role, this.me(), task.assigneeId, this.projectOwnerId())
    );
  }

  protected due(task: TaskListItemDto): string | null {
    return dueLabel(task.dueDate, this.today, task.status);
  }
}

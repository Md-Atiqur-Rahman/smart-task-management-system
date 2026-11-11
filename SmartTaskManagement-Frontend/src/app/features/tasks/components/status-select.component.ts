import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { TASK_STATUSES, TaskItemStatus } from '../../../core/models/api.models';
import { statusLabel } from '../../../shared/labels';
import { TasksApi } from '../data-access/tasks-api.service';

/** Changes a task's status: any status to any status (PRD 8). Reverts and explains on failure. */
@Component({
  selector: 'app-status-select',
  template: `
    <select
      class="input input-sm"
      [id]="selectId()"
      [attr.aria-label]="'Status of ' + taskTitle()"
      [disabled]="saving()"
      (change)="onChange($event)"
    >
      @for (s of statuses; track s) {
        <option [value]="s" [selected]="s === current()">{{ label(s) }}</option>
      }
    </select>
    @if (error(); as message) {
      <span class="error" role="alert">{{ message }}</span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-direction: column;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusSelectComponent {
  private readonly api = inject(TasksApi);

  readonly taskId = input.required<string>();
  readonly taskTitle = input.required<string>();
  readonly status = input.required<TaskItemStatus>();
  readonly selectId = input<string | null>(null);
  readonly changed = output<TaskItemStatus>();

  protected readonly statuses = TASK_STATUSES;
  protected readonly label = statusLabel;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly override = signal<TaskItemStatus | null>(null);

  protected current(): TaskItemStatus {
    return this.override() ?? this.status();
  }

  protected onChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const next = select.value as TaskItemStatus;
    const previous = this.current();
    this.saving.set(true);
    this.error.set(null);
    this.api.changeStatus(this.taskId(), next).subscribe({
      next: () => {
        this.override.set(next);
        this.saving.set(false);
        this.changed.emit(next);
      },
      error: (err: unknown) => {
        select.value = previous;
        this.saving.set(false);
        this.error.set(messageFor(toApiProblem(err)));
      },
    });
  }
}

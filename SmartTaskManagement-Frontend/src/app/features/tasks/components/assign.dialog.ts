import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { PersonDto } from '../../../core/models/api.models';
import { ModalComponent } from '../../../shared/modal.component';
import { UserPickerComponent } from '../../../shared/user-picker.component';
import { UsersApi } from '../../users/data-access/users-api.service';
import { TasksApi } from '../data-access/tasks-api.service';
import { TaskDetailsDto } from '../models/task.models';

/** Assign, change or remove the assignee (prototype D11; PUT /api/tasks/{id}/assignee). */
@Component({
  selector: 'app-assign-dialog',
  imports: [ModalComponent, UserPickerComponent],
  template: `
    <app-modal heading="Assign task" (closed)="closed.emit()">
      @if (error(); as message) {
        <div class="alert alert-error" role="alert">{{ message }}</div>
      }
      @if (task().assignee; as current) {
        <div class="current">
          <span>
            Assigned to <strong>{{ current.fullName }}</strong>
            <span class="muted">· {{ current.email }}</span>
          </span>
          <button type="button" class="btn btn-sm" [disabled]="saving()" (click)="save(null)">
            Unassign
          </button>
        </div>
      } @else {
        <p class="muted">This task is unassigned.</p>
      }
      <app-user-picker
        [label]="task().assignee ? 'Assign to someone else' : 'Assign to'"
        [search]="searchPeople"
        hint="Project Managers can assign Team Members and themselves; Admins can assign anyone."
        (picked)="chosen.set($event)"
      />
      <p class="muted small">
        The new assignee gets a notification and an email. The previous assignee loses access at
        once.
      </p>
      <div class="actions">
        <button type="button" class="btn" (click)="closed.emit()">Cancel</button>
        <button
          type="button"
          class="btn btn-primary"
          [disabled]="!chosen() || saving()"
          (click)="save(chosen()?.id ?? null)"
        >
          {{ chosen() ? 'Assign to ' + chosen()?.fullName : 'Assign' }}
        </button>
      </div>
    </app-modal>
  `,
  styles: `
    .current {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      border: 1px solid var(--color-border);
      border-radius: 10px;
      background: #f9fafb;
    }
    .small {
      font-size: 12px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssignDialogComponent {
  private readonly api = inject(TasksApi);
  private readonly users = inject(UsersApi);

  readonly task = input.required<TaskDetailsDto>();
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly chosen = signal<PersonDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly searchPeople = (term: string) => this.users.assignable(term);

  protected save(assigneeId: string | null): void {
    this.saving.set(true);
    this.error.set(null);
    this.api.assign(this.task().id, assigneeId).subscribe({
      next: () => this.saved.emit(),
      error: (err: unknown) => {
        this.error.set(messageFor(toApiProblem(err)));
        this.saving.set(false);
      },
    });
  }
}

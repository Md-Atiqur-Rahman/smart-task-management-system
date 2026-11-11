import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, map } from 'rxjs';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { PersonDto, TASK_PRIORITIES, TaskPriority } from '../../../core/models/api.models';
import { applyServerErrors, controlError } from '../../../shared/forms';
import { priorityLabel } from '../../../shared/labels';
import { ModalComponent } from '../../../shared/modal.component';
import { UserPickerComponent } from '../../../shared/user-picker.component';
import { UsersApi } from '../../users/data-access/users-api.service';
import { TasksApi } from '../data-access/tasks-api.service';
import { TASK_DESCRIPTION_MAX, TASK_TITLE_MAX, TaskDetailsDto } from '../models/task.models';

/**
 * New task (in a project) or Edit task (prototype D08, D11). Edit covers title,
 * description, priority and due date only; status and assignee have their own
 * operations (PRD 8). "Improve with AI" is shown disabled until the AI endpoint exists.
 */
@Component({
  selector: 'app-task-form-dialog',
  imports: [ReactiveFormsModule, ModalComponent, UserPickerComponent],
  template: `
    <app-modal [heading]="task() ? 'Edit task' : 'New task'" (closed)="closed.emit()">
      <form class="dialog-body" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (error(); as message) {
          <div class="alert alert-error" role="alert">{{ message }}</div>
        }
        <div class="field">
          <label class="label" for="task-title">Title</label>
          <input
            class="input"
            id="task-title"
            formControlName="title"
            [maxlength]="titleMax"
            [attr.aria-invalid]="!!controlError(form.controls.title, 'Title')"
            aria-describedby="task-title-error"
          />
          <div class="field-footer">
            <span class="error" id="task-title-error">{{
              controlError(form.controls.title, 'Title')
            }}</span>
            <span class="hint">{{ form.controls.title.value.length }} / {{ titleMax }}</span>
          </div>
        </div>
        <div class="two">
          <div class="field">
            <label class="label" for="task-priority">Priority</label>
            <select class="input" id="task-priority" formControlName="priority">
              @for (p of priorities; track p) {
                <option [value]="p">{{ priorityLabel(p) }}</option>
              }
            </select>
          </div>
          <div class="field">
            <label class="label" for="task-due">Due date</label>
            <input class="input" id="task-due" type="date" formControlName="dueDate" />
            <span class="hint">Optional. Date only; past dates are allowed.</span>
          </div>
        </div>
        @if (!task()) {
          <app-user-picker
            label="Assignee"
            placeholder="Search by name or email (optional)"
            [search]="searchPeople"
            hint="Leave empty to create the task unassigned."
            (picked)="assignee.set($event)"
          />
          @if (assignee(); as person) {
            <p class="hint">
              Will be assigned to <strong>{{ person.fullName }}</strong
              >.
              <button type="button" class="btn btn-sm btn-ghost" (click)="assignee.set(null)">
                Clear
              </button>
            </p>
          }
        }
        <div class="field">
          <div class="label-row">
            <label class="label" for="task-description">Description</label>
            <span class="tip">
              <button type="button" class="btn btn-sm" disabled aria-describedby="ai-tip">
                Improve with AI
              </button>
              <span class="tip-text" id="ai-tip" role="tooltip">Coming soon</span>
            </span>
          </div>
          <textarea
            class="input"
            id="task-description"
            formControlName="description"
            [maxlength]="descriptionMax"
          ></textarea>
          <div class="field-footer">
            <span class="error">{{ controlError(form.controls.description, 'Description') }}</span>
            <span class="hint"
              >{{ form.controls.description.value.length }} / {{ descriptionMax }}</span
            >
          </div>
        </div>
        <div class="actions">
          <button type="button" class="btn" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">
            {{ task() ? 'Save changes' : 'Create task' }}
          </button>
        </div>
      </form>
    </app-modal>
  `,
  styles: `
    .two {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
    }
    .label-row {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      gap: 8px;
      margin-bottom: 6px;
    }
    .label-row .label {
      margin: 0;
    }
    .tip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .tip-text {
      font-size: 12px;
      color: var(--color-muted);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskFormDialogComponent implements OnInit {
  private readonly api = inject(TasksApi);
  private readonly users = inject(UsersApi);

  /** Create in this project (when task is null). */
  readonly projectId = input<string | null>(null);
  /** null = create. */
  readonly task = input<TaskDetailsDto | null>(null);
  readonly saved = output<string>();
  readonly closed = output<void>();

  protected readonly titleMax = TASK_TITLE_MAX;
  protected readonly descriptionMax = TASK_DESCRIPTION_MAX;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly priorityLabel = priorityLabel;
  protected readonly controlError = controlError;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly assignee = signal<PersonDto | null>(null);
  protected readonly searchPeople = (term: string) => this.users.assignable(term);
  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(TASK_TITLE_MAX)]],
    description: ['', Validators.maxLength(TASK_DESCRIPTION_MAX)],
    priority: ['Medium' as TaskPriority],
    dueDate: [''],
  });

  ngOnInit(): void {
    const task = this.task();
    if (task) {
      this.form.patchValue({
        title: task.title,
        description: task.description ?? '',
        priority: task.priority,
        dueDate: task.dueDate ?? '',
      });
    }
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const body = {
      title: value.title.trim(),
      description: value.description.trim() || null,
      priority: value.priority,
      dueDate: value.dueDate || null,
    };
    const task = this.task();
    const projectId = this.projectId();
    const request: Observable<string> = task
      ? this.api.update(task.id, body).pipe(map(() => task.id))
      : this.api
          .create(projectId ?? '', { ...body, assigneeId: this.assignee()?.id ?? null })
          .pipe(map((created) => created.id));

    request.subscribe({
      next: (id) => this.saved.emit(id),
      error: (err: unknown) => {
        const problem = toApiProblem(err);
        const unmatched = applyServerErrors(this.form, problem);
        this.error.set(unmatched.length ? unmatched.join(' ') : messageFor(problem));
        this.saving.set(false);
      },
    });
  }
}

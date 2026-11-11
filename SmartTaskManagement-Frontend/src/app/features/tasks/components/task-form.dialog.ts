import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, TimeoutError, map } from 'rxjs';
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
 * operations (PRD 8). "Improve with AI" asks the API for a suggestion; the user's text
 * is replaced only when they choose "Use this description" (AI design: the user decides).
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
            <button
              type="button"
              class="btn btn-sm ai-btn"
              aria-describedby="ai-note"
              [disabled]="!canImprove() || improving()"
              (click)="improve()"
            >
              {{ improving() ? 'Improving…' : 'Improve with AI' }}
            </button>
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
          <p class="hint" id="ai-note">
            Improve with AI sends the title and description to Google Gemini. Do not include
            confidential or personal information.
          </p>
        </div>
        @if (aiError(); as message) {
          <div class="alert alert-error" role="alert">{{ message }}</div>
        }
        @if (suggestion(); as text) {
          <section class="suggestion" aria-labelledby="ai-suggestion-title">
            <div class="suggestion-head">
              <strong id="ai-suggestion-title">AI suggestion</strong>
              <span class="hint">Review it before you use it.</span>
            </div>
            <p class="suggestion-text">{{ text }}</p>
            <div class="actions">
              <button type="button" class="btn btn-sm" (click)="suggestion.set(null)">
                Discard
              </button>
              <button type="button" class="btn btn-sm btn-primary" (click)="useSuggestion(text)">
                Use this description
              </button>
            </div>
          </section>
        }
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
    .ai-btn {
      border-color: var(--color-primary-border);
      background: var(--color-primary-soft);
      color: var(--color-primary-hover);
    }
    .suggestion {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 14px;
      border: 1px solid var(--color-primary-border);
      border-radius: 10px;
      background: var(--color-primary-soft);
    }
    .suggestion-head {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 8px;
      color: var(--color-primary-hover);
    }
    .suggestion-text {
      margin: 0;
      padding: 12px;
      white-space: pre-wrap;
      line-height: 1.6;
      font-size: 13px;
      background: #fff;
      border: 1px solid var(--color-primary-border);
      border-radius: 8px;
      max-height: 280px;
      overflow-y: auto;
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

  private readonly description = toSignal(this.form.controls.description.valueChanges, {
    initialValue: '',
  });
  protected readonly canImprove = computed(() => this.description().trim().length > 0);
  protected readonly improving = signal(false);
  protected readonly suggestion = signal<string | null>(null);
  protected readonly aiError = signal<string | null>(null);

  /** Asks the API for a better description; the user's text changes only if they accept it. */
  protected improve(): void {
    const { title, description } = this.form.getRawValue();
    if (!description.trim() || this.improving()) {
      return;
    }
    this.improving.set(true);
    this.aiError.set(null);
    this.suggestion.set(null);
    this.api.improveDescription(title.trim() || null, description.trim()).subscribe({
      next: (text) => {
        this.suggestion.set(text);
        this.improving.set(false);
      },
      error: (err: unknown) => {
        this.aiError.set(
          err instanceof TimeoutError
            ? 'The AI service took too long. Try again.'
            : messageFor(toApiProblem(err)),
        );
        this.improving.set(false);
      },
    });
  }

  protected useSuggestion(text: string): void {
    this.form.controls.description.setValue(text);
    this.form.controls.description.markAsDirty();
    this.suggestion.set(null);
  }

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

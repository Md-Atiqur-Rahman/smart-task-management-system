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
import { PagedResult, PersonDto } from '../../../core/models/api.models';
import { applyServerErrors, controlError } from '../../../shared/forms';
import { ModalComponent } from '../../../shared/modal.component';
import { UserPickerComponent } from '../../../shared/user-picker.component';
import { UsersApi } from '../../users/data-access/users-api.service';
import { ProjectsApi } from '../data-access/projects-api.service';
import {
  PROJECT_DESCRIPTION_MAX,
  PROJECT_NAME_MAX,
  ProjectDetailsDto,
} from '../models/project.models';

/**
 * New project / Edit project (prototype D08, D11). An Admin creating a project
 * must choose a Project Manager as owner; a Project Manager becomes the owner.
 */
@Component({
  selector: 'app-project-form-dialog',
  imports: [ReactiveFormsModule, ModalComponent, UserPickerComponent],
  template: `
    <app-modal [heading]="project() ? 'Edit project' : 'New project'" (closed)="closed.emit()">
      <form class="dialog-body" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (error(); as message) {
          <div class="alert alert-error" role="alert">{{ message }}</div>
        }
        <div class="field">
          <label class="label" for="project-name">Name</label>
          <input
            class="input"
            id="project-name"
            formControlName="name"
            [maxlength]="nameMax"
            [attr.aria-invalid]="!!controlError(form.controls.name, 'Name')"
            aria-describedby="project-name-error"
          />
          <div class="field-footer">
            <span class="error" id="project-name-error">{{
              controlError(form.controls.name, 'Name')
            }}</span>
            <span class="hint">{{ form.controls.name.value.length }} / {{ nameMax }}</span>
          </div>
        </div>
        <div class="field">
          <label class="label" for="project-description">Description</label>
          <textarea
            class="input"
            id="project-description"
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
        @if (chooseOwner()) {
          <div class="field">
            <app-user-picker
              label="Owner (Project Manager)"
              [search]="searchManagers"
              hint="Admins choose the owner; a Project Manager who creates a project becomes its owner."
              (picked)="form.controls.ownerId.setValue($event.id)"
            />
            <span class="error">{{ controlError(form.controls.ownerId, 'Owner') }}</span>
          </div>
        }
        <div class="actions">
          <button type="button" class="btn" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">
            {{ project() ? 'Save changes' : 'Create project' }}
          </button>
        </div>
      </form>
    </app-modal>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectFormDialogComponent implements OnInit {
  private readonly api = inject(ProjectsApi);
  private readonly users = inject(UsersApi);

  /** null = create. */
  readonly project = input<ProjectDetailsDto | null>(null);
  /** True for an Admin creating a project. */
  readonly chooseOwner = input(false);
  readonly saved = output<string>();
  readonly closed = output<void>();

  protected readonly nameMax = PROJECT_NAME_MAX;
  protected readonly descriptionMax = PROJECT_DESCRIPTION_MAX;
  protected readonly controlError = controlError;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(PROJECT_NAME_MAX)]],
    description: ['', Validators.maxLength(PROJECT_DESCRIPTION_MAX)],
    ownerId: [''],
  });
  protected readonly searchManagers = (term: string): Observable<PagedResult<PersonDto>> =>
    this.users.projectManagers(term);

  ngOnInit(): void {
    const project = this.project();
    if (project) {
      this.form.patchValue({ name: project.name, description: project.description ?? '' });
    }
    if (this.chooseOwner()) {
      this.form.controls.ownerId.addValidators(Validators.required);
      this.form.controls.ownerId.updateValueAndValidity();
    }
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const { name, ownerId } = this.form.getRawValue();
    const description = this.form.controls.description.value.trim() || null;
    const project = this.project();
    const request: Observable<string> = project
      ? this.api.update(project.id, { name, description }).pipe(map(() => project.id))
      : this.api
          .create({ name, description, ownerId: ownerId || null })
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

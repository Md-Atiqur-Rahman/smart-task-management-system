import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { PagedResult, PersonDto } from '../../../core/models/api.models';
import { ModalComponent } from '../../../shared/modal.component';
import { UserPickerComponent } from '../../../shared/user-picker.component';
import { UsersApi } from '../../users/data-access/users-api.service';
import { ProjectsApi } from '../data-access/projects-api.service';
import { ProjectDetailsDto } from '../models/project.models';

/** Give a project to another Project Manager (Admin only; prototype D08, PUT /api/projects/{id}/owner). */
@Component({
  selector: 'app-change-owner-dialog',
  imports: [ModalComponent, UserPickerComponent],
  template: `
    <app-modal heading="Change owner" (closed)="closed.emit()">
      @if (error(); as message) {
        <div class="alert alert-error" role="alert">{{ message }}</div>
      }
      <p>
        Current owner: <strong>{{ project().owner.fullName }}</strong>
        <span class="muted">· {{ project().owner.email }}</span>
      </p>
      <app-user-picker
        label="New owner (Project Manager)"
        [search]="searchManagers"
        hint="Only a Project Manager can own a project."
        (picked)="chosen.set($event)"
      />
      <p class="muted small">
        The new owner can manage the project and its tasks; the previous owner loses that access.
        Both get a notification and an email.
      </p>
      <div class="actions">
        <button type="button" class="btn" (click)="closed.emit()">Cancel</button>
        <button type="button" class="btn btn-primary" [disabled]="!canSave()" (click)="save()">
          {{ chosen() ? 'Make ' + chosen()?.fullName + ' the owner' : 'Change owner' }}
        </button>
      </div>
    </app-modal>
  `,
  styles: `
    .small {
      font-size: 12px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeOwnerDialogComponent {
  private readonly api = inject(ProjectsApi);
  private readonly users = inject(UsersApi);

  readonly project = input.required<ProjectDetailsDto>();
  readonly saved = output<void>();
  readonly closed = output<void>();

  protected readonly chosen = signal<PersonDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly searchManagers = (term: string): Observable<PagedResult<PersonDto>> =>
    this.users.projectManagers(term);

  /** A different person must be chosen; the current owner is not a change. */
  protected canSave(): boolean {
    const chosen = this.chosen();
    return !!chosen && chosen.id !== this.project().owner.id && !this.saving();
  }

  protected save(): void {
    const chosen = this.chosen();
    if (!chosen) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api.changeOwner(this.project().id, chosen.id).subscribe({
      next: () => this.saved.emit(),
      error: (err: unknown) => {
        this.error.set(messageFor(toApiProblem(err)));
        this.saving.set(false);
      },
    });
  }
}

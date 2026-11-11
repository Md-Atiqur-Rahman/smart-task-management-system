import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { BehaviorSubject, combineLatest, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { LoadState, keepDataWhileLoading, loadState } from '../../../shared/load-state';
import { ConfirmDialogComponent } from '../../../shared/modal.component';
import { ProjectTasksComponent } from '../../tasks';
import { ChangeOwnerDialogComponent } from '../components/change-owner.dialog';
import { ProjectFormDialogComponent } from '../components/project-form.dialog';
import { ProjectsApi } from '../data-access/projects-api.service';
import { ProjectDetailsDto } from '../models/project.models';

/** Project detail (prototype D05): details, task counts, and the project's task list. */
@Component({
  selector: 'app-project-detail-page',
  imports: [
    DatePipe,
    RouterLink,
    ConfirmDialogComponent,
    ProjectFormDialogComponent,
    ChangeOwnerDialogComponent,
    ProjectTasksComponent,
  ],
  templateUrl: './project-detail.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailPage {
  private readonly api = inject(ProjectsApi);
  private readonly store = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  /** Route parameter :id (component input binding). */
  readonly id = input.required<string>();

  protected readonly state = toSignal(
    combineLatest([toObservable(this.id), this.reload$]).pipe(
      switchMap(([id]) => loadState(this.api.get(id))),
      keepDataWhileLoading(),
    ),
    { initialValue: { status: 'loading' } as LoadState<ProjectDetailsDto> },
  );
  protected readonly me = computed(() => this.store.user()?.id ?? null);
  protected readonly isAdmin = computed(() => this.store.role() === 'Admin');
  protected readonly editing = signal(false);
  protected readonly deleting = signal(false);
  protected readonly changingOwner = signal(false);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected canManage(project: ProjectDetailsDto): boolean {
    return this.isAdmin() || project.owner.id === this.me();
  }

  protected onSaved(): void {
    this.editing.set(false);
    this.reload$.next();
  }

  protected onOwnerChanged(): void {
    this.changingOwner.set(false);
    this.reload$.next();
  }

  protected reload(): void {
    this.reload$.next();
  }

  protected confirmDelete(project: ProjectDetailsDto): void {
    this.busy.set(true);
    this.api.remove(project.id).subscribe({
      next: () => void this.router.navigate(['/projects']),
      error: (err: unknown) => {
        this.busy.set(false);
        this.deleting.set(false);
        this.actionError.set(messageFor(toApiProblem(err)));
      },
    });
  }
}

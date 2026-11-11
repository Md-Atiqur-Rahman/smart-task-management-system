import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BehaviorSubject, Subject, combineLatest, debounceTime, map, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import { DEFAULT_PAGE_SIZE, SortDirection } from '../../../core/models/api.models';
import { readListQuery, toQueryParams } from '../../../shared/list-query';
import { LoadState, loadState } from '../../../shared/load-state';
import { ConfirmDialogComponent } from '../../../shared/modal.component';
import { PagerComponent } from '../../../shared/pager.component';
import { UserPickerComponent } from '../../../shared/user-picker.component';
import { UsersApi } from '../../users/data-access/users-api.service';
import { ProjectFormDialogComponent } from '../components/project-form.dialog';
import { ProjectsApi } from '../data-access/projects-api.service';
import { ProjectDetailsDto, ProjectListItemDto, ProjectSortField } from '../models/project.models';

const DEFAULTS = {
  search: '',
  ownerId: '',
  sort: 'updatedAt:Descending',
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

const SORTS: readonly { value: string; label: string }[] = [
  { value: 'updatedAt:Descending', label: 'Recently updated' },
  { value: 'createdAt:Descending', label: 'Recently created' },
  { value: 'createdAt:Ascending', label: 'Oldest first' },
  { value: 'name:Ascending', label: 'Name A–Z' },
  { value: 'name:Descending', label: 'Name Z–A' },
];

/** Projects list (prototype D04): server-side search, owner filter (Admin), sort and paging. */
@Component({
  selector: 'app-projects-list-page',
  imports: [
    DatePipe,
    RouterLink,
    PagerComponent,
    ConfirmDialogComponent,
    ProjectFormDialogComponent,
    UserPickerComponent,
  ],
  templateUrl: './projects-list.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectsListPage {
  private readonly api = inject(ProjectsApi);
  private readonly users = inject(UsersApi);
  private readonly store = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reload$ = new BehaviorSubject<void>(undefined);
  private readonly searchInput$ = new Subject<string>();

  protected readonly sorts = SORTS;
  protected readonly isAdmin = computed(() => this.store.role() === 'Admin');
  protected readonly canCreate = computed(() => this.store.role() !== 'TeamMember');
  protected readonly query = toSignal(
    this.route.queryParamMap.pipe(map((params) => readListQuery(params, DEFAULTS))),
    { initialValue: DEFAULTS },
  );
  protected readonly state = toSignal(
    combineLatest([this.route.queryParamMap, this.reload$]).pipe(
      switchMap(([params]) => {
        const q = readListQuery(params, DEFAULTS);
        const [sortBy, sortDirection] = q.sort.split(':') as [ProjectSortField, SortDirection];
        return loadState(
          this.api.list({
            search: q.search,
            ownerId: q.ownerId,
            sortBy,
            sortDirection,
            page: q.page,
            pageSize: q.pageSize,
          }),
        );
      }),
    ),
    { initialValue: { status: 'loading' } as LoadState<never> },
  );

  protected readonly creating = signal(false);
  protected readonly editing = signal<ProjectDetailsDto | null>(null);
  protected readonly deleting = signal<ProjectListItemDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly searchManagers = (term: string) => this.users.projectManagers(term);

  constructor() {
    this.searchInput$
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe((search) => this.update({ search }));
  }

  protected canManage(project: ProjectListItemDto): boolean {
    return this.isAdmin() || project.ownerId === this.store.user()?.id;
  }

  protected progress(project: ProjectListItemDto): number {
    return project.taskCount
      ? Math.round((project.completedTaskCount / project.taskCount) * 100)
      : 0;
  }

  protected onSearch(event: Event): void {
    this.searchInput$.next((event.target as HTMLInputElement).value.trim());
  }

  protected onSort(event: Event): void {
    this.update({ sort: (event.target as HTMLSelectElement).value });
  }

  protected update(patch: Partial<typeof DEFAULTS>): void {
    const next = { ...this.query(), page: 1, ...patch };
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(next, DEFAULTS),
    });
  }

  protected openEdit(project: ProjectListItemDto): void {
    this.actionError.set(null);
    this.api.get(project.id).subscribe({
      next: (details) => this.editing.set(details),
      error: (err: unknown) => this.actionError.set(messageFor(toApiProblem(err))),
    });
  }

  protected onSaved(id: string): void {
    const wasCreate = this.creating();
    this.creating.set(false);
    this.editing.set(null);
    if (wasCreate) {
      void this.router.navigate(['/projects', id]);
    } else {
      this.reload$.next();
    }
  }

  protected confirmDelete(): void {
    const project = this.deleting();
    if (!project) return;
    this.busy.set(true);
    this.api.remove(project.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.deleting.set(null);
        this.reload$.next();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.actionError.set(messageFor(toApiProblem(err)));
        this.deleting.set(null);
      },
    });
  }
}

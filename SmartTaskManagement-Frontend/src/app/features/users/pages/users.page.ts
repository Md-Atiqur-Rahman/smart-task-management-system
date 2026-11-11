import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BehaviorSubject, Subject, combineLatest, debounceTime, map, switchMap } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { toApiProblem } from '../../../core/http/api-problem';
import { messageFor } from '../../../core/http/error-messages';
import {
  DEFAULT_PAGE_SIZE,
  PagedResult,
  ROLES,
  Role,
  SortDirection,
} from '../../../core/models/api.models';
import { roleLabel } from '../../../shared/labels';
import { readListQuery, toQueryParams } from '../../../shared/list-query';
import { LoadState, loadState } from '../../../shared/load-state';
import { ConfirmDialogComponent } from '../../../shared/modal.component';
import { PagerComponent } from '../../../shared/pager.component';
import { UserDto, UserSortField, UsersApi } from '../data-access/users-api.service';

const DEFAULTS = {
  search: '',
  role: '',
  sort: 'fullName:Ascending',
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

const SORTS: readonly { value: string; label: string }[] = [
  { value: 'fullName:Ascending', label: 'Name A–Z' },
  { value: 'email:Ascending', label: 'Email A–Z' },
  { value: 'role:Ascending', label: 'Role' },
  { value: 'createdAt:Descending', label: 'Newest first' },
];

interface RoleChange {
  user: UserDto;
  role: Role;
}

/** Users and roles (Admin only, prototype D09). People register themselves as Team Members. */
@Component({
  selector: 'app-users-page',
  imports: [DatePipe, RouterLink, PagerComponent, ConfirmDialogComponent],
  templateUrl: './users.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage {
  private readonly api = inject(UsersApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reload$ = new BehaviorSubject<void>(undefined);
  private readonly searchInput$ = new Subject<string>();

  protected readonly me = inject(AuthStore).user;
  protected readonly roles = ROLES;
  protected readonly sorts = SORTS;
  protected readonly roleLabel = roleLabel;
  protected readonly query = toSignal(
    this.route.queryParamMap.pipe(map((params) => readListQuery(params, DEFAULTS))),
    { initialValue: DEFAULTS },
  );
  protected readonly state = toSignal(
    combineLatest([this.route.queryParamMap, this.reload$]).pipe(
      switchMap(([params]) => {
        const q = readListQuery(params, DEFAULTS);
        const [sortBy, sortDirection] = q.sort.split(':') as [UserSortField, SortDirection];
        return loadState(
          this.api.list({
            search: q.search,
            role: q.role as Role | '',
            sortBy,
            sortDirection,
            page: q.page,
            pageSize: q.pageSize,
          }),
        );
      }),
    ),
    { initialValue: { status: 'loading' } as LoadState<PagedResult<UserDto>> },
  );

  protected readonly pending = signal<RoleChange | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<{ message: string; ownerId: string | null } | null>(null);
  protected readonly notice = signal<string | null>(null);

  constructor() {
    this.searchInput$
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe((search) => this.update({ search }));
  }

  protected onSearch(event: Event): void {
    this.searchInput$.next((event.target as HTMLInputElement).value.trim());
  }

  protected value(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }

  protected update(patch: Partial<typeof DEFAULTS>): void {
    const next = { ...this.query(), page: 1, ...patch };
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(next, DEFAULTS),
    });
  }

  protected askRoleChange(user: UserDto, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const role = select.value as Role;
    select.value = user.role;
    if (role !== user.role) {
      this.pending.set({ user, role });
    }
  }

  protected confirmText(change: RoleChange): string {
    return `${change.user.fullName} becomes ${roleLabel(change.role)} and is signed out everywhere; they must sign in again.`;
  }

  protected confirmRoleChange(): void {
    const change = this.pending();
    if (!change) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.changeRole(change.user.id, change.role).subscribe({
      next: () => {
        this.busy.set(false);
        this.pending.set(null);
        this.notice.set(`${change.user.fullName} is now ${roleLabel(change.role)}.`);
        this.reload$.next();
      },
      error: (err: unknown) => {
        const problem = toApiProblem(err);
        this.busy.set(false);
        this.pending.set(null);
        this.error.set({
          message: messageFor(problem),
          ownerId: problem.errorCode === 'users.owns_projects' ? change.user.id : null,
        });
      },
    });
  }
}

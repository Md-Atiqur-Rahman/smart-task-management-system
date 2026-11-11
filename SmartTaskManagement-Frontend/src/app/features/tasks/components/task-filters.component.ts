import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime } from 'rxjs';
import { TASK_PRIORITIES, TASK_STATUSES } from '../../../core/models/api.models';
import { priorityLabel, statusLabel } from '../../../shared/labels';
import { UserPickerComponent } from '../../../shared/user-picker.component';
import { UsersApi } from '../../users/data-access/users-api.service';
import { TASK_SORTS, TaskFilterState } from '../task-query';

/** Search, filters and sort for task lists (server-side; prototype D05, D06). */
@Component({
  selector: 'app-task-filters',
  imports: [UserPickerComponent],
  template: `
    <form role="search" class="filters" (submit)="$event.preventDefault()">
      <div class="field wide">
        <label class="label" [for]="id('q')">Search tasks</label>
        <input
          class="input"
          type="search"
          [id]="id('q')"
          placeholder="Title or description"
          [value]="value().search"
          (input)="search$.next(text($event))"
        />
      </div>
      <div class="field">
        <label class="label" [for]="id('status')">Status</label>
        <select class="input" [id]="id('status')" (change)="emit({ status: text($event) })">
          <option value="">All statuses</option>
          @for (s of statuses; track s) {
            <option [value]="s" [selected]="s === value().status">{{ statusLabel(s) }}</option>
          }
        </select>
      </div>
      <div class="field">
        <label class="label" [for]="id('priority')">Priority</label>
        <select class="input" [id]="id('priority')" (change)="emit({ priority: text($event) })">
          <option value="">All priorities</option>
          @for (p of priorities; track p) {
            <option [value]="p" [selected]="p === value().priority">{{ priorityLabel(p) }}</option>
          }
        </select>
      </div>
      <div class="field">
        <label class="label" [for]="id('assignee')">Assignee</label>
        <select class="input" [id]="id('assignee')" (change)="emit({ assignee: text($event) })">
          <option value="" [selected]="value().assignee === ''">Anyone</option>
          <option value="me" [selected]="value().assignee === 'me'">Assigned to me</option>
          <option value="unassigned" [selected]="value().assignee === 'unassigned'">
            Unassigned
          </option>
          @if (personChosen()) {
            <option [value]="value().assignee" selected>Chosen person</option>
          }
        </select>
      </div>
      @if (canPickAssignee()) {
        <div class="field">
          <app-user-picker
            label="Choose a person"
            [search]="searchPeople"
            (picked)="emit({ assignee: $event.id })"
          />
        </div>
      }
      <div class="field">
        <label class="label" [for]="id('from')">Due from</label>
        <input
          class="input"
          type="date"
          [id]="id('from')"
          [value]="value().dueFrom"
          (change)="emit({ dueFrom: text($event) })"
        />
      </div>
      <div class="field">
        <label class="label" [for]="id('to')">Due to</label>
        <input
          class="input"
          type="date"
          [id]="id('to')"
          [value]="value().dueTo"
          (change)="emit({ dueTo: text($event) })"
        />
      </div>
      <div class="field">
        <label class="label" [for]="id('sort')">Sort by</label>
        <select class="input" [id]="id('sort')" (change)="emit({ sort: text($event) })">
          @for (sort of sorts; track sort.value) {
            <option [value]="sort.value" [selected]="sort.value === value().sort">
              {{ sort.label }}
            </option>
          }
        </select>
      </div>
      <div class="field end">
        <button type="button" class="btn" (click)="clear.emit()">Clear filters</button>
      </div>
    </form>
  `,
  styles: `
    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: 12px;
      align-items: end;
      padding: 16px;
    }
    .wide {
      grid-column: span 2;
    }
    @media (max-width: 520px) {
      .wide {
        grid-column: auto;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskFiltersComponent {
  private static nextId = 0;
  private readonly users = inject(UsersApi);
  private readonly prefix = `task-filter-${TaskFiltersComponent.nextId++}`;

  readonly value = input.required<TaskFilterState>();
  /** Admin and Project Manager only (a Team Member may not call /api/users/assignable). */
  readonly canPickAssignee = input(false);
  readonly changed = output<Partial<TaskFilterState>>();
  readonly clear = output<void>();

  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly sorts = TASK_SORTS;
  protected readonly statusLabel = statusLabel;
  protected readonly priorityLabel = priorityLabel;
  protected readonly search$ = new Subject<string>();
  protected readonly searchPeople = (term: string) => this.users.assignable(term);

  constructor() {
    this.search$
      .pipe(debounceTime(300), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((search) => this.emit({ search: search.trim() }));
  }

  protected id(name: string): string {
    return `${this.prefix}-${name}`;
  }

  protected personChosen(): boolean {
    const assignee = this.value().assignee;
    return assignee !== '' && assignee !== 'me' && assignee !== 'unassigned';
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement).value;
  }

  protected emit(patch: Partial<TaskFilterState>): void {
    this.changed.emit({ ...patch, page: 1 });
  }
}

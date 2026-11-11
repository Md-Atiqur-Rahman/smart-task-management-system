import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  Observable,
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  of,
  switchMap,
} from 'rxjs';
import { PagedResult, PersonDto } from '../core/models/api.models';

/** Searches people (assignable users, Project Managers) as you type: an ARIA combobox. */
@Component({
  selector: 'app-user-picker',
  template: `
    <div class="picker">
      <label class="label" [for]="inputId">{{ label() }}</label>
      <input
        class="input"
        [id]="inputId"
        role="combobox"
        autocomplete="off"
        aria-autocomplete="list"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="listId"
        [attr.aria-activedescendant]="open() && results().length ? optionId(active()) : null"
        [placeholder]="placeholder()"
        [value]="text()"
        (input)="onInput($event)"
        (keydown)="onKey($event)"
        (blur)="closeSoon()"
      />
      @if (open()) {
        <div class="list" role="listbox" [id]="listId" [attr.aria-label]="label()">
          @for (person of results(); track person.id; let i = $index) {
            <button
              type="button"
              role="option"
              tabindex="-1"
              class="option"
              [id]="optionId(i)"
              [class.active]="i === active()"
              [attr.aria-selected]="i === active()"
              (mousedown)="$event.preventDefault()"
              (click)="pick(person)"
            >
              <strong>{{ person.fullName }}</strong>
              <span class="muted email">{{ person.email }}</span>
            </button>
          } @empty {
            <p class="muted none">No matching people.</p>
          }
        </div>
      }
      @if (hint()) {
        <span class="hint">{{ hint() }}</span>
      }
    </div>
  `,
  styles: `
    .picker {
      position: relative;
      display: flex;
      flex-direction: column;
    }
    .list {
      position: absolute;
      top: 100%;
      left: 0;
      right: 0;
      z-index: 20;
      margin-top: 4px;
      padding: 4px;
      max-height: 240px;
      overflow-y: auto;
      background: #fff;
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius);
      box-shadow: 0 8px 20px rgba(15, 23, 42, 0.12);
    }
    .option {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      width: 100%;
      padding: 8px 10px;
      border: 0;
      border-radius: 6px;
      background: transparent;
      text-align: left;
      cursor: pointer;
      font: inherit;
    }
    .option.active {
      background: var(--color-primary-soft);
      outline: 2px solid var(--color-focus);
    }
    .email {
      font-size: 12px;
    }
    .none {
      padding: 8px 10px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserPickerComponent {
  private static nextId = 0;

  readonly label = input.required<string>();
  readonly search = input.required<(term: string) => Observable<PagedResult<PersonDto>>>();
  readonly placeholder = input('Search by name or email');
  readonly hint = input<string | null>(null);
  readonly picked = output<PersonDto>();

  private readonly id = UserPickerComponent.nextId++;
  protected readonly inputId = `user-picker-${this.id}`;
  protected readonly listId = `user-picker-list-${this.id}`;
  protected readonly text = signal('');
  protected readonly results = signal<PersonDto[]>([]);
  protected readonly open = signal(false);
  protected readonly active = signal(0);
  private readonly terms = new Subject<string>();

  constructor() {
    this.terms
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) =>
          this.search()(term).pipe(
            catchError(() => of({ items: [] } as unknown as PagedResult<PersonDto>)),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((page) => {
        this.results.set(page.items);
        this.active.set(0);
        this.open.set(true);
      });
  }

  protected optionId(index: number): string {
    return `${this.listId}-option-${index}`;
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.text.set(value);
    this.terms.next(value.trim());
  }

  protected onKey(event: KeyboardEvent): void {
    const count = this.results().length;
    if (event.key === 'ArrowDown' && count) {
      event.preventDefault();
      this.open.set(true);
      this.active.set((this.active() + 1) % count);
    } else if (event.key === 'ArrowUp' && count) {
      event.preventDefault();
      this.active.set((this.active() - 1 + count) % count);
    } else if (event.key === 'Enter' && this.open() && count) {
      event.preventDefault();
      this.pick(this.results()[this.active()]);
    } else if (event.key === 'Escape' && this.open()) {
      event.stopPropagation();
      this.open.set(false);
    }
  }

  protected pick(person: PersonDto): void {
    this.text.set(person.fullName);
    this.open.set(false);
    this.picked.emit(person);
  }

  protected closeSoon(): void {
    this.open.set(false);
  }
}

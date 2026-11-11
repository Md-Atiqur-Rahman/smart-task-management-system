import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { PAGE_SIZE_OPTIONS } from '../core/models/api.models';
import { totalPages } from './list-query';

/** Pager for server-side lists (PagedResult: page, pageSize, totalCount). */
@Component({
  selector: 'app-pager',
  template: `
    <div class="pager">
      <span class="muted summary" aria-live="polite">{{ summary() }}</span>
      <div class="controls">
        <label class="muted size-label" for="page-size">Rows per page</label>
        <select
          id="page-size"
          class="input input-sm"
          [value]="pageSize()"
          (change)="onSize($event)"
        >
          @for (size of sizes; track size) {
            <option [value]="size" [selected]="size === pageSize()">{{ size }}</option>
          }
        </select>
        <nav aria-label="Pagination" class="pages">
          <button
            type="button"
            class="btn btn-sm"
            [disabled]="page() <= 1"
            aria-label="Previous page"
            (click)="pageChange.emit(page() - 1)"
          >
            ‹
          </button>
          @for (n of pageNumbers(); track n) {
            <button
              type="button"
              class="btn btn-sm"
              [class.current]="n === page()"
              [attr.aria-current]="n === page() ? 'page' : null"
              [attr.aria-label]="'Page ' + n"
              (click)="pageChange.emit(n)"
            >
              {{ n }}
            </button>
          }
          <button
            type="button"
            class="btn btn-sm"
            [disabled]="page() >= pages()"
            aria-label="Next page"
            (click)="pageChange.emit(page() + 1)"
          >
            ›
          </button>
        </nav>
      </div>
    </div>
  `,
  styles: `
    .pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      padding: 12px 16px;
      border-top: 1px solid var(--color-border);
    }
    .summary,
    .size-label {
      font-size: 13px;
    }
    .controls,
    .pages {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
    }
    .current {
      background: var(--color-primary);
      border-color: var(--color-primary);
      color: #fff;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PagerComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly noun = input('items');
  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  protected readonly sizes = PAGE_SIZE_OPTIONS;
  protected readonly pages = computed(() => totalPages(this.totalCount(), this.pageSize()));
  protected readonly pageNumbers = computed(() => {
    const last = this.pages();
    const start = Math.max(1, Math.min(this.page() - 2, last - 4));
    const end = Math.min(last, start + 4);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });
  protected readonly summary = computed(() => {
    const total = this.totalCount();
    if (total === 0) {
      return `No ${this.noun()}`;
    }
    const first = (this.page() - 1) * this.pageSize() + 1;
    const last = Math.min(total, this.page() * this.pageSize());
    return `Showing ${first}–${last} of ${total} ${this.noun()}`;
  });

  protected onSize(event: Event): void {
    this.pageSizeChange.emit(Number((event.target as HTMLSelectElement).value));
  }
}

import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

/**
 * Accessible modal built on the native <dialog>: focus is trapped, Esc closes,
 * and focus returns to the opener. Render it with @if; it opens itself.
 */
@Component({
  selector: 'app-modal',
  template: `
    <dialog
      #dialog
      class="dialog"
      [attr.role]="alert() ? 'alertdialog' : null"
      [attr.aria-labelledby]="headingId"
      (close)="closed.emit()"
    >
      <div class="dialog-body">
        <h2 [id]="headingId" class="heading">{{ heading() }}</h2>
        <ng-content />
      </div>
    </dialog>
  `,
  styles: `
    .heading {
      font-size: 20px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModalComponent {
  private static nextId = 0;

  readonly heading = input.required<string>();
  readonly alert = input(false);
  readonly closed = output<void>();

  protected readonly headingId = `modal-heading-${ModalComponent.nextId++}`;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  /** The element that had focus before the modal opened (usually the button that opened it). */
  private readonly opener = document.activeElement;

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
    // A native close() returns focus by itself, but @if removes the modal without close().
    inject(DestroyRef).onDestroy(() => this.returnFocus());
  }

  close(): void {
    this.dialog().nativeElement.close();
  }

  private returnFocus(): void {
    const focused = document.activeElement;
    const focusWasHere =
      !focused || focused === document.body || this.dialog().nativeElement.contains(focused);
    if (focusWasHere && this.opener instanceof HTMLElement && this.opener.isConnected) {
      this.opener.focus();
    }
  }
}

/** Yes/no confirmation (delete, role change). */
@Component({
  selector: 'app-confirm-dialog',
  imports: [ModalComponent],
  template: `
    <app-modal [heading]="heading()" [alert]="true" (closed)="cancelled.emit()">
      <p class="muted message">{{ message() }}</p>
      @if (error(); as text) {
        <div class="alert alert-error" role="alert">{{ text }}</div>
      }
      <div class="actions">
        <button type="button" class="btn" (click)="cancelled.emit()">Cancel</button>
        <button
          type="button"
          class="btn"
          [class.btn-danger]="danger()"
          [class.btn-primary]="!danger()"
          [disabled]="busy()"
          (click)="confirmed.emit()"
        >
          {{ confirmText() }}
        </button>
      </div>
    </app-modal>
  `,
  styles: `
    .message {
      line-height: 1.6;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  readonly heading = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmText = input('Confirm');
  readonly danger = input(true);
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();
}

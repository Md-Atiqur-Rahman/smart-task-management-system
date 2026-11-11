import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from './toast.service';

/** Live toasts, bottom right (prototype D02). Announced politely to screen readers. */
@Component({
  selector: 'app-toast-host',
  imports: [RouterLink],
  template: `
    <div class="host" role="status" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast">
          <div class="text">
            <span>{{ toast.message }}</span>
            @if (toast.link) {
              <a [routerLink]="toast.link" (click)="toasts.dismiss(toast.id)">Open</a>
            }
          </div>
          <button
            type="button"
            class="close"
            aria-label="Dismiss notification"
            (click)="toasts.dismiss(toast.id)"
          >
            ×
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .host {
      position: fixed;
      right: 16px;
      bottom: 16px;
      z-index: 60;
      display: flex;
      flex-direction: column;
      gap: 8px;
      width: min(380px, calc(100vw - 32px));
    }
    .toast {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 14px 16px;
      border-radius: 12px;
      background: var(--color-sidebar);
      color: #fff;
      box-shadow: 0 10px 30px rgba(15, 23, 42, 0.25);
    }
    .text {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    a {
      color: #5eead4;
      font-weight: 600;
    }
    .close {
      width: 32px;
      height: 32px;
      border: 0;
      border-radius: 8px;
      background: transparent;
      color: #cbd5e1;
      font-size: 18px;
      cursor: pointer;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ToastHostComponent {
  protected readonly toasts = inject(ToastService);
}

import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  link: string[] | null;
}

const TOAST_MS = 6000;

/** Live pop-ups for new notifications (PRD 13). */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  show(message: string, link: string[] | null): void {
    const toast: Toast = { id: this.nextId++, message, link };
    this.toasts.update((list) => [...list.slice(-2), toast]);
    setTimeout(() => this.dismiss(toast.id), TOAST_MS);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }
}

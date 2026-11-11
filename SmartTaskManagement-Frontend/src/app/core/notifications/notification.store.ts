import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { API_BASE_URL } from '../config/api-base-url';

export type NotificationType =
  'TaskAssigned' | 'TaskUnassigned' | 'ProjectOwnerAssigned' | 'ProjectOwnerRemoved';

/** NotificationDto (openapi.json). */
export interface NotificationDto {
  id: string;
  type: NotificationType;
  message: string;
  taskId: string | null;
  projectId: string | null;
  createdAt: string;
  isRead: boolean;
}

/** "Removed" notifications never link to the task or project (PRD 13). */
export function notificationLink(n: NotificationDto): string[] | null {
  if (n.type === 'TaskAssigned' && n.taskId) return ['/tasks', n.taskId];
  if (n.type === 'ProjectOwnerAssigned' && n.projectId) return ['/projects', n.projectId];
  return null;
}

/** Bell badge and list (last 7 days, kept by the API). */
@Injectable({ providedIn: 'root' })
export class NotificationStore {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/api/notifications`;

  readonly unread = signal(0);
  readonly items = signal<NotificationDto[]>([]);
  readonly loading = signal(false);

  loadCount(): void {
    this.http
      .get<{ count: number }>(`${this.url}/unread-count`)
      .subscribe({ next: (result) => this.unread.set(result.count), error: () => undefined });
  }

  loadList(): void {
    this.loading.set(true);
    this.http.get<NotificationDto[]>(this.url).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** A `notification` SSE event. */
  onLive(notification: NotificationDto): void {
    this.items.update((items) => [notification, ...items.filter((n) => n.id !== notification.id)]);
    if (!notification.isRead) {
      this.unread.update((count) => count + 1);
    }
  }

  markRead(id: string): void {
    const item = this.items().find((n) => n.id === id);
    if (item?.isRead) {
      return;
    }
    this.http.post<void>(`${this.url}/${encodeURIComponent(id)}/read`, null).subscribe({
      next: () => {
        this.items.update((items) => items.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
        this.unread.update((count) => Math.max(0, count - 1));
      },
      error: () => undefined,
    });
  }

  markAllRead(): void {
    this.http.post<void>(`${this.url}/read-all`, null).subscribe({
      next: () => {
        this.items.update((items) => items.map((n) => ({ ...n, isRead: true })));
        this.unread.set(0);
      },
      error: () => undefined,
    });
  }

  reset(): void {
    this.unread.set(0);
    this.items.set([]);
  }
}

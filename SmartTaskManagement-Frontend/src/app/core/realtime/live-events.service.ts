import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { SessionService } from '../auth/session.service';
import { API_BASE_URL } from '../config/api-base-url';
import {
  NotificationDto,
  NotificationStore,
  notificationLink,
} from '../notifications/notification.store';
import { ToastService } from '../notifications/toast.service';
import { DashboardRefresh } from './dashboard-refresh';
import { SseEvent, SseParser } from './sse-parser';

const BASE_DELAY_MS = 1000;
const MAX_DELAY_MS = 30_000;
/** A stream that stayed open this long was healthy: the next reconnect starts the backoff again. */
const HEALTHY_STREAM_MS = 30_000;

/**
 * SSE client for GET /api/notifications/stream (notifications-design.md 6).
 * The stream needs the bearer header, so it uses fetch + ReadableStream, not
 * EventSource; the token never goes into the URL. The server ends the stream at
 * token expiry or on a role change; the client reconnects with exponential
 * backoff and a fresh token, and stops on logout.
 */
@Injectable({ providedIn: 'root' })
export class LiveEventsService {
  private readonly url = `${inject(API_BASE_URL)}/api/notifications/stream`;
  private readonly store = inject(AuthStore);
  private readonly session = inject(SessionService);
  private readonly notifications = inject(NotificationStore);
  private readonly toasts = inject(ToastService);
  private readonly dashboard = inject(DashboardRefresh);

  readonly status = this.dashboard.status;

  private running = false;
  private abort: AbortController | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private wake: (() => void) | null = null;

  start(): void {
    if (this.running) {
      return;
    }
    this.running = true;
    void this.loop();
  }

  stop(): void {
    this.running = false;
    this.abort?.abort();
    clearTimeout(this.timer);
    this.wake?.();
    this.status.set('off');
  }

  private async loop(): Promise<void> {
    let attempt = 0;
    let connectedBefore = false;
    while (this.running) {
      const openedAt = Date.now();
      const opened = await this.connect(connectedBefore);
      connectedBefore ||= opened;
      if (!this.running) {
        break;
      }
      attempt = opened && Date.now() - openedAt >= HEALTHY_STREAM_MS ? 0 : attempt;
      this.status.set('reconnecting');
      await this.delay(this.backoff(attempt++));
    }
  }

  /** One connection; true when the stream opened. */
  private async connect(reconnect: boolean): Promise<boolean> {
    let token = this.store.accessToken();
    if (!token) {
      if (!(await firstValueFrom(this.session.refresh()))) {
        this.stop();
        return false;
      }
      token = this.store.accessToken();
    }
    this.abort = new AbortController();
    try {
      const response = await fetch(this.url, {
        headers: { Authorization: `Bearer ${token}`, Accept: 'text/event-stream' },
        signal: this.abort.signal,
        cache: 'no-store',
      });
      if (response.status === 401) {
        if (!(await firstValueFrom(this.session.refresh()))) {
          this.stop();
        }
        return false;
      }
      if (!response.ok || !response.body) {
        return false;
      }
      this.status.set('live');
      if (reconnect) {
        // Events may have been missed while disconnected.
        this.dashboard.notifyChanged();
        this.notifications.loadCount();
      }
      await this.read(response.body);
      return true;
    } catch {
      return false;
    }
  }

  private async read(body: ReadableStream<Uint8Array>): Promise<void> {
    const reader = body.getReader();
    const decoder = new TextDecoder();
    const parser = new SseParser();
    for (;;) {
      const { value, done } = await reader.read();
      if (done || !this.running) {
        return;
      }
      for (const event of parser.push(decoder.decode(value, { stream: true }))) {
        this.handle(event);
      }
    }
  }

  private handle(event: SseEvent): void {
    switch (event.event) {
      case 'notification': {
        const notification = this.parse(event.data);
        if (notification) {
          this.notifications.onLive(notification);
          this.toasts.show(notification.message, notificationLink(notification));
        }
        break;
      }
      case 'notifications-read':
        this.notifications.loadCount();
        break;
      case 'dashboard-changed':
        this.dashboard.notifyChanged();
        break;
      default:
        // heartbeat and unknown events: nothing to do.
        break;
    }
  }

  private parse(data: string): NotificationDto | null {
    try {
      return JSON.parse(data) as NotificationDto;
    } catch {
      return null;
    }
  }

  private backoff(attempt: number): number {
    const base = Math.min(MAX_DELAY_MS, BASE_DELAY_MS * 2 ** attempt);
    return base + Math.random() * base * 0.2;
  }

  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => {
      this.wake = resolve;
      this.timer = setTimeout(resolve, ms);
    });
  }
}

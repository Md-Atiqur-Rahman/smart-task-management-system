import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationDto, NotificationStore, notificationLink } from './notification.store';

/** Bell with the unread badge and the notification panel (prototype D03, M01-6). */
@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe],
  host: { '(document:keydown.escape)': 'open.set(false)' },
  template: `
    <div class="bell-wrap">
      <button
        type="button"
        class="bell"
        [attr.aria-label]="label()"
        [attr.aria-expanded]="open()"
        aria-controls="notification-panel"
        (click)="toggle()"
      >
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M6 8a6 6 0 1112 0c0 7 3 8 3 8H3s3-1 3-8"></path>
          <path d="M10 20a2 2 0 004 0"></path>
        </svg>
        @if (store.unread() > 0) {
          <span class="count" aria-hidden="true">{{
            store.unread() > 99 ? '99+' : store.unread()
          }}</span>
        }
      </button>
      @if (open()) {
        <section id="notification-panel" class="panel card" aria-labelledby="notification-title">
          <div class="panel-head">
            <h2 id="notification-title">Notifications</h2>
            <button
              type="button"
              class="btn btn-sm btn-ghost link"
              [disabled]="store.unread() === 0"
              (click)="store.markAllRead()"
            >
              Mark all as read
            </button>
          </div>
          @if (store.loading() && store.items().length === 0) {
            <p class="muted pad" role="status">Loading…</p>
          } @else if (store.items().length === 0) {
            <p class="muted pad">No notifications in the last 7 days.</p>
          } @else {
            <ul class="list">
              @for (n of store.items(); track n.id) {
                <li class="item" [class.unread]="!n.isRead">
                  <span class="dot" [class.on]="!n.isRead" aria-hidden="true"></span>
                  <div class="body">
                    @if (link(n)) {
                      <button type="button" class="open" (click)="openItem(n)">
                        @if (!n.isRead) {
                          <span class="sr-only">Unread: </span>
                        }
                        {{ n.message }}
                      </button>
                    } @else {
                      <span class="text">
                        @if (!n.isRead) {
                          <span class="sr-only">Unread: </span>
                        }
                        {{ n.message }}</span
                      >
                    }
                    <span class="muted when">{{ n.createdAt | date: 'medium' }}</span>
                  </div>
                  @if (!n.isRead) {
                    <button
                      type="button"
                      class="mark"
                      aria-label="Mark as read"
                      (click)="store.markRead(n.id)"
                    >
                      ✓
                    </button>
                  }
                </li>
              }
            </ul>
          }
          <p class="muted foot">Notifications from the last 7 days.</p>
        </section>
      }
    </div>
  `,
  styles: `
    .bell-wrap {
      position: relative;
    }
    .bell {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 44px;
      height: 44px;
      border-radius: 10px;
      border: 1px solid var(--color-border);
      background: #fff;
      color: var(--color-text);
      cursor: pointer;
    }
    .bell[aria-expanded='true'] {
      border-color: var(--color-primary);
      background: var(--color-primary-soft);
    }
    .count {
      position: absolute;
      top: -6px;
      right: -6px;
      min-width: 20px;
      height: 20px;
      padding: 0 6px;
      border-radius: 999px;
      background: var(--color-danger);
      color: #fff;
      font-size: 11px;
      font-weight: 700;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    .panel {
      position: absolute;
      right: 0;
      top: calc(100% + 8px);
      z-index: 30;
      width: min(420px, calc(100vw - 24px));
      box-shadow: 0 16px 40px rgba(15, 23, 42, 0.18);
      overflow: hidden;
    }
    .panel-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-border);
    }
    h2 {
      font-size: 17px;
    }
    .link {
      color: var(--color-primary);
    }
    .list {
      list-style: none;
      margin: 0;
      padding: 0;
      max-height: 60vh;
      overflow-y: auto;
    }
    .item {
      display: flex;
      gap: 10px;
      align-items: flex-start;
      padding: 12px 16px;
      border-bottom: 1px solid #f1f5f9;
    }
    .item.unread {
      background: var(--color-primary-soft);
    }
    .dot {
      flex: none;
      width: 10px;
      height: 10px;
      margin-top: 6px;
      border-radius: 999px;
      border: 2px solid #cbd5e1;
    }
    .dot.on {
      border-color: var(--color-primary);
      background: var(--color-primary);
    }
    .body {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .unread .open,
    .unread .text {
      font-weight: 600;
    }
    .open {
      padding: 0;
      border: 0;
      background: none;
      text-align: left;
      font: inherit;
      color: var(--color-text);
      cursor: pointer;
      text-decoration: underline;
    }
    .when,
    .foot {
      font-size: 12px;
    }
    .mark {
      flex: none;
      width: 32px;
      height: 32px;
      border: 1px solid var(--color-border-strong);
      border-radius: 8px;
      background: #fff;
      color: var(--color-primary);
      cursor: pointer;
    }
    .pad,
    .foot {
      padding: 12px 16px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationBellComponent {
  protected readonly store = inject(NotificationStore);
  private readonly router = inject(Router);

  protected readonly open = signal(false);
  protected readonly label = computed(() => {
    const count = this.store.unread();
    return count ? `Notifications, ${count} unread` : 'Notifications, no unread';
  });
  protected readonly link = notificationLink;

  protected toggle(): void {
    this.open.update((value) => !value);
    if (this.open()) {
      this.store.loadList();
    }
  }

  protected openItem(n: NotificationDto): void {
    const target = notificationLink(n);
    this.store.markRead(n.id);
    this.open.set(false);
    if (target) {
      void this.router.navigate(target);
    }
  }
}

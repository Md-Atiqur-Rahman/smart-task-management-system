import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { roleLabel } from '../../shared/labels';
import { AuthStore } from '../auth/auth.store';
import { SessionService } from '../auth/session.service';
import { Role } from '../models/api.models';
import { NotificationBellComponent } from '../notifications/notification-bell.component';
import { NotificationStore } from '../notifications/notification.store';
import { ToastHostComponent } from '../notifications/toast-host.component';
import { LiveEventsService } from '../realtime/live-events.service';

interface NavItem {
  path: string;
  label: string;
  roles?: readonly Role[];
}

const NAV: readonly NavItem[] = [
  { path: '/dashboard', label: 'Dashboard' },
  { path: '/projects', label: 'Projects' },
  { path: '/tasks', label: 'Tasks' },
  { path: '/users', label: 'Users', roles: ['Admin'] },
];

/** Signed-in layout: sidebar (a drawer below 768 px), top bar and the routed page. */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    NotificationBellComponent,
    ToastHostComponent,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closeMenu()' },
})
export class ShellComponent {
  private readonly store = inject(AuthStore);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly live = inject(LiveEventsService);
  private readonly notifications = inject(NotificationStore);

  protected readonly user = this.store.user;
  protected readonly roleText = computed(() => {
    const role = this.store.role();
    return role ? roleLabel(role) : '';
  });
  protected readonly initials = computed(() =>
    (this.user()?.fullName ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join(''),
  );
  protected readonly navItems = computed(() => {
    const role = this.store.role();
    return NAV.filter((item) => !item.roles || (role !== null && item.roles.includes(role)));
  });
  protected readonly menuOpen = signal(false);
  /** An intentional logout goes to plain /login, without a return URL. */
  private loggingOut = false;

  constructor() {
    // Live events and the bell run while the signed-in shell is shown (PRD 13).
    this.notifications.loadCount();
    this.live.start();
    inject(DestroyRef).onDestroy(() => {
      this.live.stop();
      this.notifications.reset();
    });
    // A session that ends while the shell is open (refresh failed, role changed) goes to sign-in.
    effect(() => {
      if (!this.store.isSignedIn() && !this.loggingOut) {
        void this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
      }
    });
  }

  protected openMenu(): void {
    this.menuOpen.set(true);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }

  protected async logout(): Promise<void> {
    this.loggingOut = true;
    this.live.stop();
    await this.session.logout();
    await this.router.navigate(['/login']);
  }
}

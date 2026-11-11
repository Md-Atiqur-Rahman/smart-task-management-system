import { Injectable, computed, signal } from '@angular/core';
import { AuthResponse } from '../models/api.models';

/**
 * The signed-in session, held in memory only (PRD 5): never written to
 * localStorage, sessionStorage, a cookie or a URL. A reload restores it
 * through the HttpOnly refresh cookie (SessionService.restore).
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly session = signal<AuthResponse | null>(null);

  readonly user = computed(() => this.session()?.user ?? null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);
  readonly expiresAt = computed(() => this.session()?.expiresAt ?? null);
  readonly role = computed(() => this.session()?.user.role ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);

  setSession(response: AuthResponse): void {
    this.session.set(response);
  }

  clear(): void {
    this.session.set(null);
  }
}

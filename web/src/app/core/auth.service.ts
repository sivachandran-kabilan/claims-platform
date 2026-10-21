import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Api } from '../api/api';
import { login } from '../api/functions';

export type Role = 'Claimant' | 'ClaimStaff' | 'ClaimManager';

interface TokenClaims {
  sub: string;
  email: string;
  role: Role;
  exp: number;
}

// Access token lives in memory; sessionStorage is only the reload fallback (README: XSS trade-off).
const STORAGE_KEY = 'claims.accessToken';

/**
 * Holds the session. The role read from the token drives navigation and guards only (UX);
 * the API enforces every permission.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly token = signal<string | null>(readStored());

  readonly claims = computed(() => decode(this.token()));
  readonly isLoggedIn = computed(() => this.claims() !== null);
  readonly role = computed(() => this.claims()?.role ?? null);
  readonly userId = computed(() => this.claims()?.sub ?? null);
  readonly email = computed(() => this.claims()?.email ?? null);

  /** The token to send, or null if missing/expired (an expired token is cleared). */
  accessToken(): string | null {
    const claims = decode(this.token());
    if (!claims) {
      if (this.token()) this.clear();
      return null;
    }
    return this.token();
  }

  async login(email: string, password: string): Promise<void> {
    const response = await this.api.invoke(login, { body: { email, password } });
    this.token.set(response.accessToken ?? null);
    try {
      sessionStorage.setItem(STORAGE_KEY, response.accessToken ?? '');
    } catch {
      // Storage unavailable (private mode): the session just won't survive a reload.
    }
  }

  logout(returnUrl?: string): void {
    this.clear();
    void this.router.navigate(['/login'], { queryParams: returnUrl ? { returnUrl } : {} });
  }

  homeRoute(): string {
    switch (this.role()) {
      case 'Claimant':
        return '/claims';
      case 'ClaimStaff':
        return '/staff/queue';
      case 'ClaimManager':
        return '/manager/dashboard';
      default:
        return '/login';
    }
  }

  hasRole(...roles: Role[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  private clear(): void {
    this.token.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }
}

function readStored(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

/** Reads the JWT payload (base64url). No signature check here: that is the API's job. */
function decode(token: string | null): TokenClaims | null {
  if (!token) return null;
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(atob(payload)) as TokenClaims;
    return claims.exp * 1000 > Date.now() ? claims : null;
  } catch {
    return null;
  }
}

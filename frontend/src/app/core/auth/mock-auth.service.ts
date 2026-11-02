import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthApi } from '../../api/auth-api';
import { DEMO_USERS, DemoUser } from './demo-users';
import {
  canApproveReserves,
  canApproveTier,
  canReopen,
  canSetAggregateLimitOverride,
} from './permissions';
import { AuthorityTier } from '../../shared/domain/authority-tier';

export const SELECTED_USER_STORAGE_KEY = 'claims.demoUserId';

/**
 * §11.4's mock authentication: the app acts as one of the three Demo Users and holds a real JWT for
 * them, minted by the API's dev token issuer (ADR-0010) — the API validates it like any other token.
 * Only the selected user id is persisted (localStorage); the token lives in memory and is re-minted
 * on start-up.
 */
@Injectable({ providedIn: 'root' })
export class MockAuthService {
  private readonly authApi = inject(AuthApi);
  private readonly user = signal<DemoUser>(DEMO_USERS[0]);
  private readonly token = signal<string | null>(null);

  readonly currentUser = this.user.asReadonly();
  readonly accessToken = this.token.asReadonly();
  readonly demoUsers = DEMO_USERS;

  readonly canApproveReserves = computed(() => canApproveReserves(this.user().role));
  readonly canReopen = computed(() => canReopen(this.user().role));
  readonly canSetAggregateLimitOverride = computed(() =>
    canSetAggregateLimitOverride(this.user().role),
  );

  canApproveTier(tier: AuthorityTier): boolean {
    return canApproveTier(this.user().role, tier);
  }

  /** Restores the remembered Demo User (default: the handler) — run once before the app starts. */
  restore(): Observable<void> {
    const remembered = readRememberedUserId();
    const user = DEMO_USERS.find((u) => u.userId === remembered) ?? DEMO_USERS[0];
    return this.signIn(user.userId);
  }

  /** Switches to a Demo User; the visible user changes only once their token has arrived. */
  signIn(userId: string): Observable<void> {
    const user = DEMO_USERS.find((u) => u.userId === userId);
    if (user === undefined) {
      throw new Error(`Unknown demo user ${userId}.`);
    }
    return this.authApi.mintToken(user.userId).pipe(
      tap((response) => {
        this.token.set(response.accessToken);
        this.user.set(user);
        rememberUserId(user.userId);
      }),
      map(() => undefined),
    );
  }

  /** Re-mints the current user's token (the auth interceptor's single retry on a 401). */
  refreshToken(): Observable<string> {
    return this.authApi.mintToken(this.user().userId).pipe(
      map((response) => response.accessToken),
      tap((accessToken) => this.token.set(accessToken)),
    );
  }
}

// Browser storage can throw (private mode, blocked storage) — the remembered user is a convenience only.
function readRememberedUserId(): string | null {
  try {
    return localStorage.getItem(SELECTED_USER_STORAGE_KEY);
  } catch {
    return null;
  }
}

function rememberUserId(userId: string): void {
  try {
    localStorage.setItem(SELECTED_USER_STORAGE_KEY, userId);
  } catch {
    // ignore — see readRememberedUserId
  }
}

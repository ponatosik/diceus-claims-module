import { Injectable, signal } from '@angular/core';

/**
 * Broadcast for "the data on screen is stale — reload it" (the 409 conflict snackbar's Reload action,
 * spec 007 story 7). Feature stores react to `count()` changing and re-fetch.
 */
@Injectable({ providedIn: 'root' })
export class ReloadRequests {
  private readonly requests = signal(0);

  readonly count = this.requests.asReadonly();

  request(): void {
    this.requests.update((n) => n + 1);
  }
}

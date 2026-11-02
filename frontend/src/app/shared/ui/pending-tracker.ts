import { computed, signal } from '@angular/core';
import { Observable, defer, finalize } from 'rxjs';

/**
 * §11.4's loading convention: `pending()` is true while any tracked request is in flight. Templates
 * bind buttons' `[disabled]` and skeleton/spinner visibility to it; one tracker per action or screen.
 */
export class PendingTracker {
  private readonly inFlight = signal(0);

  readonly pending = computed(() => this.inFlight() > 0);

  track<T>(source: Observable<T>): Observable<T> {
    return defer(() => {
      this.inFlight.update((n) => n + 1);
      return source.pipe(finalize(() => this.inFlight.update((n) => n - 1)));
    });
  }
}

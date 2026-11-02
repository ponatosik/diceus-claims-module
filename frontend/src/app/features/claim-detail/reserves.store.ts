import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { latestValue } from '../../shared/state/latest-value';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, map, tap } from 'rxjs';
import { OpenReserveRequest } from '../../api/models';
import { ReservesApi } from '../../api/reserves-api';
import { ClaimDetailStore } from './claim-detail.store';
import { hasPostingsInFlight } from './reserve-rules';

const POSTING_POLL_MS = 5000;

/**
 * Reserves tab state (§11.3 Tab 3). Loads on ClaimDetailStore's reserves revision; polls every 5s only
 * while a GL posting is in flight, so Pending → Posted shows without a manual refresh (spec 011 story
 * 10); after any reserve action re-fetches reserves, header and audit log (story 11).
 */
@Injectable()
export class ReservesStore {
  private readonly reservesApi = inject(ReservesApi);
  private readonly detail = inject(ClaimDetailStore);
  private readonly pollTick = signal(0);

  private readonly resource = rxResource({
    params: () => {
      const claimId = this.detail.claimId();
      return claimId === null
        ? undefined
        : { claimId, revision: this.detail.reservesRevision(), poll: this.pollTick() };
    },
    stream: ({ params }) => this.reservesApi.list(params.claimId),
  });

  /** The latest loaded list — kept on screen during re-fetches and polls. */
  private readonly latest = latestValue(this.resource);

  readonly components = computed(() => this.latest()?.components ?? []);
  readonly history = computed(() => this.latest()?.history ?? []);
  readonly loaded = computed(() => this.latest() !== undefined);
  readonly pollingPostings = computed(() => hasPostingsInFlight(this.history()));

  constructor() {
    effect((onCleanup) => {
      if (!this.pollingPostings()) return;
      const timer = setInterval(() => this.pollTick.update((n) => n + 1), POSTING_POLL_MS);
      onCleanup(() => clearInterval(timer));
    });
  }

  submit(request: OpenReserveRequest): Observable<void> {
    return this.afterChange((claimId) => this.reservesApi.open(claimId, request));
  }

  approve(transactionId: string): Observable<void> {
    return this.afterChange((claimId) => this.reservesApi.approve(claimId, transactionId));
  }

  reject(transactionId: string, rejectionReason: string): Observable<void> {
    return this.afterChange((claimId) =>
      this.reservesApi.reject(claimId, transactionId, rejectionReason),
    );
  }

  retract(transactionId: string): Observable<void> {
    return this.afterChange((claimId) => this.reservesApi.retract(claimId, transactionId));
  }

  retryPosting(transactionId: string): Observable<void> {
    return this.afterChange((claimId) => this.reservesApi.retryPosting(claimId, transactionId));
  }

  setAggregateLimitOverride(): Observable<void> {
    return this.afterChange((claimId) => this.reservesApi.setAggregateLimitOverride(claimId));
  }

  private afterChange(call: (claimId: string) => Observable<unknown>): Observable<void> {
    const claimId = this.detail.claimId();
    if (claimId === null) throw new Error('No claim loaded.');
    return call(claimId).pipe(
      tap(() => this.detail.refreshAfterReserveChange()),
      map(() => undefined),
    );
  }
}

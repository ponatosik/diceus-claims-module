import {
  Injectable,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import {
  ClaimDetail,
  ClaimStatus,
  CreatePartyRequest,
  TransitionPreflight,
  TransitionRequest,
} from '../../api/models';
import { isApiError } from '../../core/errors/api-error';
import { ReloadRequests } from '../../core/errors/reload-requests';

/**
 * Claim Detail state (§11.3). Mutations go to the API and then *re-fetch* what they affect — the claim
 * (header + Overview + Parties) and the audit log — rather than patching local state, so the server's
 * projections stay authoritative (spec 010 story 9). The tabs that load their own data (Reserves,
 * Documents, Audit — Slices K/L) key their loads on the revision signals below.
 */
@Injectable()
export class ClaimDetailStore {
  private readonly claimsApi = inject(ClaimsApi);
  private readonly router = inject(Router);
  private readonly reloadRequests = inject(ReloadRequests);

  private readonly claimId = signal<string | null>(null);
  private readonly claimRevision = signal(0);

  /** Bumped whenever the audit log / reserves / documents should be re-fetched. */
  readonly auditRevision = signal(0);
  readonly reservesRevision = signal(0);
  readonly documentsRevision = signal(0);

  private readonly claimResource = rxResource({
    params: () => {
      const claimId = this.claimId();
      return claimId === null ? undefined : { claimId, revision: this.claimRevision() };
    },
    stream: ({ params }) => this.claimsApi.get(params.claimId),
  });

  /** The latest loaded claim — kept on screen while a re-fetch is in flight. */
  readonly claim = linkedSignal<ClaimDetail | undefined, ClaimDetail | undefined>({
    source: () => (this.claimResource.hasValue() ? this.claimResource.value() : undefined),
    computation: (claim, previous) => claim ?? previous?.value,
  });
  readonly loading = this.claimResource.isLoading;
  readonly loadFailed = computed(() => this.claimResource.status() === 'error');

  constructor() {
    // A 404 shows the not-found page (spec 010 story 10), keeping the URL the user asked for.
    effect(() => {
      const error = this.claimResource.error();
      // rxResource wraps stream errors; the ApiError from the interceptor is the cause.
      const cause: unknown = error?.cause ?? error;
      if (isApiError(cause) && cause.kind === 'notFound') {
        void untracked(() => this.router.navigate(['/not-found'], { skipLocationChange: true }));
      }
    });

    // The 409 conflict snackbar's "Reload" re-fetches everything on screen.
    let initial = true;
    effect(() => {
      this.reloadRequests.count();
      if (initial) {
        initial = false;
        return;
      }
      untracked(() => this.reloadAll());
    });
  }

  load(claimId: string): void {
    this.claimId.set(claimId);
  }

  reloadAll(): void {
    this.claimRevision.update((n) => n + 1);
    this.auditRevision.update((n) => n + 1);
    this.reservesRevision.update((n) => n + 1);
    this.documentsRevision.update((n) => n + 1);
  }

  /** The Pre-flight Check (GLOSSARY.md) for a target status — changes nothing. */
  preflight(target: ClaimStatus): Observable<TransitionPreflight> {
    return this.claimsApi.preflight(this.requireClaimId(), target);
  }

  transition(request: TransitionRequest): Observable<void> {
    return this.mutate((claimId) => this.claimsApi.transition(claimId, request));
  }

  updateNotes(notes: string | null): Observable<void> {
    return this.mutate((claimId) => this.claimsApi.updateNotes(claimId, notes));
  }

  addParty(party: CreatePartyRequest): Observable<void> {
    return this.mutate((claimId) => this.claimsApi.addParty(claimId, party));
  }

  removeParty(partyId: string): Observable<void> {
    return this.mutate((claimId) => this.claimsApi.removeParty(claimId, partyId));
  }

  /** Runs a claim mutation, then re-fetches the claim and its audit log (only on success). */
  private mutate(call: (claimId: string) => Observable<unknown>): Observable<void> {
    return call(this.requireClaimId()).pipe(
      tap(() => {
        this.claimRevision.update((n) => n + 1);
        this.auditRevision.update((n) => n + 1);
      }),
      map(() => undefined),
    );
  }

  private requireClaimId(): string {
    const claimId = this.claimId();
    if (claimId === null) throw new Error('No claim loaded.');
    return claimId;
  }
}

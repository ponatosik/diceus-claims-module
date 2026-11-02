import { Injectable, inject } from '@angular/core';
import { Observable, catchError, shareReplay, throwError } from 'rxjs';
import { ClaimStatusReference } from './models';
import { ReferenceApi } from './reference-api';

/** Session-cached reference data. The §4.2 transition table never changes at runtime (spec 010 story 2). */
@Injectable({ providedIn: 'root' })
export class ReferenceData {
  private readonly referenceApi = inject(ReferenceApi);
  private cachedClaimStatuses: Observable<ClaimStatusReference[]> | null = null;

  claimStatuses(): Observable<ClaimStatusReference[]> {
    this.cachedClaimStatuses ??= this.referenceApi.claimStatuses().pipe(
      catchError((error: unknown) => {
        this.cachedClaimStatuses = null; // don't cache a failure
        return throwError(() => error);
      }),
      shareReplay(1),
    );
    return this.cachedClaimStatuses;
  }
}

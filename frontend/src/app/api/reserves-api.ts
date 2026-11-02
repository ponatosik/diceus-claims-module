import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoint } from './api-endpoint';
import { OpenReserveRequest, ReserveList, ReserveTransaction } from './models';

/** /api/claims/{id}/reserves — reserve balances, history and the authority workflow (§10.2, spec 006). */
@Injectable({ providedIn: 'root' })
export class ReservesApi {
  private readonly http = inject(HttpClient);
  private readonly endpoint = inject(ApiEndpoint);

  list(claimId: string): Observable<ReserveList> {
    return this.http.get<ReserveList>(this.reservesUrl(claimId));
  }

  open(claimId: string, request: OpenReserveRequest): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(this.reservesUrl(claimId), request);
  }

  approve(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      this.reservesUrl(claimId, `${transactionId}/approve`),
      null,
    );
  }

  reject(
    claimId: string,
    transactionId: string,
    rejectionReason: string,
  ): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      this.reservesUrl(claimId, `${transactionId}/reject`),
      { rejectionReason },
    );
  }

  retract(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      this.reservesUrl(claimId, `${transactionId}/retract`),
      null,
    );
  }

  retryPosting(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      this.reservesUrl(claimId, `${transactionId}/retry-posting`),
      null,
    );
  }

  /** BR-R-05's Aggregate Limit Override (Manager only). */
  setAggregateLimitOverride(claimId: string): Observable<void> {
    return this.http.post<void>(this.reservesUrl(claimId, 'override'), null);
  }

  private reservesUrl(claimId: string, path = ''): string {
    return this.endpoint.url(`claims/${claimId}/reserves${path === '' ? '' : `/${path}`}`);
  }
}

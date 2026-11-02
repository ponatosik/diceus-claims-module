import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoint } from './api-endpoint';
import { CauseOfLossCode, ClaimStatusReference } from './models';

/** /api/reference — cause-of-loss codes (§5.6) and the claim-status transition table (§4.2). */
@Injectable({ providedIn: 'root' })
export class ReferenceApi {
  private readonly http = inject(HttpClient);
  private readonly endpoint = inject(ApiEndpoint);

  causeOfLossCodes(): Observable<CauseOfLossCode[]> {
    return this.http.get<CauseOfLossCode[]>(this.endpoint.url('reference/cause-of-loss-codes'));
  }

  claimStatuses(): Observable<ClaimStatusReference[]> {
    return this.http.get<ClaimStatusReference[]>(this.endpoint.url('reference/claim-statuses'));
  }
}

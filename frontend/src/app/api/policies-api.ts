import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoint } from './api-endpoint';
import { toHttpParams } from './http-params';
import { PolicyCoverage, PolicySearchResult } from './models';

/** /api/policies — the simulated policy lookup (§5.5, §10.4). */
@Injectable({ providedIn: 'root' })
export class PoliciesApi {
  private readonly http = inject(HttpClient);
  private readonly endpoint = inject(ApiEndpoint);

  search(query: string): Observable<PolicySearchResult[]> {
    return this.http.get<PolicySearchResult[]>(this.endpoint.url('policies/search'), {
      params: toHttpParams({ q: query }),
    });
  }

  coverage(policyId: string): Observable<PolicyCoverage> {
    return this.http.get<PolicyCoverage>(this.endpoint.url(`policies/${policyId}/coverage`));
  }
}

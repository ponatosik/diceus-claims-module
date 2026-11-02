import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoint } from './api-endpoint';
import { toHttpParams } from './http-params';
import {
  AuditLogEntry,
  Claim,
  ClaimDetail,
  ClaimListQuery,
  ClaimParty,
  ClaimStatus,
  ClaimSummary,
  CreateClaimRequest,
  CreatePartyRequest,
  PagedResult,
  TransitionPreflight,
  TransitionRequest,
  ValidationIssue,
} from './models';

/** /api/claims — FNOL, list/detail, status lifecycle, notes, parties, audit (§10.1, spec 006). */
@Injectable({ providedIn: 'root' })
export class ClaimsApi {
  private readonly http = inject(HttpClient);
  private readonly endpoint = inject(ApiEndpoint);

  list(query: ClaimListQuery): Observable<PagedResult<ClaimSummary>> {
    const { statuses, ...rest } = query;
    return this.http.get<PagedResult<ClaimSummary>>(this.claimsUrl(), {
      params: toHttpParams({ ...rest, status: statuses }),
    });
  }

  get(claimId: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(this.claimsUrl(claimId));
  }

  create(request: CreateClaimRequest): Observable<Claim> {
    return this.http.post<Claim>(this.claimsUrl(), request);
  }

  /** ADR-0008's "tell me what's wrong" surface — always 200, every Error and Warning issue. */
  validate(request: CreateClaimRequest): Observable<ValidationIssue[]> {
    return this.http.post<ValidationIssue[]>(this.claimsUrl('validate'), request);
  }

  transition(claimId: string, request: TransitionRequest): Observable<Claim> {
    return this.http.put<Claim>(this.claimsUrl(`${claimId}/status`), request);
  }

  /** The Pre-flight Check (GLOSSARY.md) for a target status — changes nothing. */
  preflight(claimId: string, target: ClaimStatus): Observable<TransitionPreflight> {
    return this.http.get<TransitionPreflight>(this.claimsUrl(`${claimId}/status/preflight`), {
      params: toHttpParams({ target }),
    });
  }

  updateNotes(claimId: string, notes: string | null): Observable<void> {
    return this.http.patch<void>(this.claimsUrl(`${claimId}/notes`), { notes });
  }

  addParty(claimId: string, party: CreatePartyRequest): Observable<ClaimParty> {
    return this.http.post<ClaimParty>(this.claimsUrl(`${claimId}/parties`), party);
  }

  removeParty(claimId: string, partyId: string): Observable<void> {
    return this.http.delete<void>(this.claimsUrl(`${claimId}/parties/${partyId}`));
  }

  audit(claimId: string, page: number, pageSize: number): Observable<PagedResult<AuditLogEntry>> {
    return this.http.get<PagedResult<AuditLogEntry>>(this.claimsUrl(`${claimId}/audit`), {
      params: toHttpParams({ page, pageSize }),
    });
  }

  private claimsUrl(path = ''): string {
    return this.endpoint.url(path === '' ? 'claims' : `claims/${path}`);
  }
}

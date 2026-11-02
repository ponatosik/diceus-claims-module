import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Role } from '../core/auth/demo-users';
import { ApiEndpoint } from './api-endpoint';

export interface DevTokenResponse {
  accessToken: string;
  userId: string;
  displayName: string;
  role: Role;
}

/** The dev token issuer (ADR-0010) — POST /api/dev/auth/token. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly endpoint = inject(ApiEndpoint);

  readonly tokenUrl = this.endpoint.url('dev/auth/token');

  mintToken(userId: string): Observable<DevTokenResponse> {
    return this.http.post<DevTokenResponse>(this.tokenUrl, { userId });
  }
}

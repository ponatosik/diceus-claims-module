import { Injectable, inject } from '@angular/core';
import { RUNTIME_CONFIG } from '../core/config/runtime-config';

/** Builds absolute API URLs from the runtime-configured base, and recognizes them (for the auth interceptor). */
@Injectable({ providedIn: 'root' })
export class ApiEndpoint {
  private readonly apiRoot = `${inject(RUNTIME_CONFIG).apiBaseUrl}/api/`;

  url(path: string): string {
    return this.apiRoot + path.replace(/^\/+/, '');
  }

  isApiUrl(url: string): boolean {
    return url.startsWith(this.apiRoot);
  }
}

import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { RUNTIME_CONFIG } from '../core/config/runtime-config';

export const TEST_API_BASE = 'https://api.test';

/** HttpClient against HttpTestingController, with the API rooted at {@link TEST_API_BASE}. */
export const apiTestProviders = [
  provideHttpClient(),
  provideHttpClientTesting(),
  { provide: RUNTIME_CONFIG, useValue: { apiBaseUrl: TEST_API_BASE } },
];

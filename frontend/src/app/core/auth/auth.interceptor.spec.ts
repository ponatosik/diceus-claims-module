import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { RUNTIME_CONFIG } from '../config/runtime-config';
import { authInterceptor } from './auth.interceptor';
import { MockAuthService } from './mock-auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let token: string;
  let refreshes: number;

  beforeEach(() => {
    token = 'token-1';
    refreshes = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: { apiBaseUrl: 'https://api.test' } },
        {
          provide: MockAuthService,
          useValue: {
            accessToken: () => token,
            refreshToken: () => {
              refreshes++;
              token = `token-${refreshes + 1}`;
              return of(token);
            },
          },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('adds the Bearer token to API requests', () => {
    http.get('https://api.test/api/claims').subscribe();

    expect(
      backend.expectOne('https://api.test/api/claims').request.headers.get('Authorization'),
    ).toBe('Bearer token-1');
  });

  it('leaves non-API requests and the token endpoint itself untouched', () => {
    http.get('/assets/config.json').subscribe();
    http.post('https://api.test/api/dev/auth/token', {}).subscribe();

    expect(backend.expectOne('/assets/config.json').request.headers.has('Authorization')).toBe(
      false,
    );
    expect(
      backend.expectOne('https://api.test/api/dev/auth/token').request.headers.has('Authorization'),
    ).toBe(false);
  });

  it('on a 401, refreshes the token once and retries with the new one', async () => {
    const result = firstValueFrom(http.get<{ ok: boolean }>('https://api.test/api/claims'));

    backend
      .expectOne('https://api.test/api/claims')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    const retry = backend.expectOne('https://api.test/api/claims');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
    retry.flush({ ok: true });

    expect(await result).toEqual({ ok: true });
    expect(refreshes).toBe(1);
  });

  it('does not retry a second time when the retried request is also rejected', async () => {
    const result = firstValueFrom(http.get('https://api.test/api/claims'));

    backend
      .expectOne('https://api.test/api/claims')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    backend
      .expectOne('https://api.test/api/claims')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(refreshes).toBe(1);
  });
});

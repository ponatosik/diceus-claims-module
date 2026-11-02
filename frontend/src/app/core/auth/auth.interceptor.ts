import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { ApiEndpoint } from '../../api/api-endpoint';
import { MockAuthService } from './mock-auth.service';

const TOKEN_PATH = 'dev/auth/token';

/**
 * §11.4: adds `Authorization: Bearer <token>` to every API request. On a 401 (e.g. the in-memory token
 * expired) it re-mints the current Demo User's token once and retries; a second 401 propagates.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const endpoint = inject(ApiEndpoint);
  const auth = inject(MockAuthService);

  if (!endpoint.isApiUrl(request.url) || request.url === endpoint.url(TOKEN_PATH)) {
    return next(request);
  }

  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return auth
        .refreshToken()
        .pipe(switchMap((freshToken) => next(withToken(request, freshToken))));
    }),
  );
};

function withToken<T>(request: HttpRequest<T>, token: string | null): HttpRequest<T> {
  return token === null
    ? request
    : request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

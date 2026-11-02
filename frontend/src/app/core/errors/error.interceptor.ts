import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiError, toApiError } from './api-error';
import { NotificationService } from './notification.service';
import { ReloadRequests } from './reload-requests';

/**
 * §11.4: every API error is caught here, normalized to an ApiError, and announced as a snackbar
 * of matching severity; the typed ApiError is then rethrown so the caller can also show a 422's
 * field errors / Blocking Conditions inline (spec 007 story 7). Registered outermost, so it sees the
 * final outcome after the auth interceptor's 401 retry.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);
  const reloadRequests = inject(ReloadRequests);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }
      const apiError = toApiError(error);
      announce(apiError, notifications, reloadRequests);
      return throwError(() => apiError);
    }),
  );
};

function announce(
  error: ApiError,
  notifications: NotificationService,
  reloadRequests: ReloadRequests,
): void {
  switch (error.kind) {
    case 'validation':
      notifications.warning(error.message);
      return;
    case 'businessRule':
      notifications.warning(error.blockingConditions[0]?.message ?? error.message);
      return;
    case 'conflict':
      notifications.error(error.message, 'Reload').subscribe((reload) => {
        if (reload) reloadRequests.request();
      });
      return;
    default:
      notifications.error(error.message).subscribe();
  }
}

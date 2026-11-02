import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  DEFAULT_CURRENCY_CODE,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_ICON_DEFAULT_OPTIONS } from '@angular/material/icon';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { catchError, of } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { MockAuthService } from './core/auth/mock-auth.service';
import { errorInterceptor } from './core/errors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Error interceptor outermost: it sees the final outcome after the auth interceptor's 401 retry.
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor])),
    // All money is USD (§6.3's thresholds are dollar amounts).
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'USD' },
    // index.html loads the Material Symbols font; make it mat-icon's default.
    { provide: MAT_ICON_DEFAULT_OPTIONS, useValue: { fontSet: 'material-symbols-outlined' } },
    // Sign in the remembered Demo User before the first screen loads. If the API is unreachable the
    // error interceptor has already announced it; the app still starts.
    provideAppInitializer(() =>
      inject(MockAuthService)
        .restore()
        .pipe(catchError(() => of(undefined))),
    ),
  ],
};

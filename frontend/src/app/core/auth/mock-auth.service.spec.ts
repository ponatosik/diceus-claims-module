import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { RUNTIME_CONFIG } from '../config/runtime-config';
import { DEMO_USERS } from './demo-users';
import { MockAuthService, SELECTED_USER_STORAGE_KEY } from './mock-auth.service';

const [handler, supervisor] = DEMO_USERS;
const TOKEN_URL = 'https://api.test/api/dev/auth/token';

describe('MockAuthService', () => {
  let service: MockAuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: { apiBaseUrl: 'https://api.test' } },
      ],
    });
    service = TestBed.inject(MockAuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function answerTokenRequest(userId: string, token: string) {
    const request = http.expectOne(TOKEN_URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userId });
    const user = DEMO_USERS.find((u) => u.userId === userId)!;
    request.flush({ accessToken: token, userId, displayName: user.displayName, role: user.role });
  }

  it('signs in as a demo user by minting a token for that fixed user id', async () => {
    const signedIn = firstValueFrom(service.signIn(supervisor.userId));
    answerTokenRequest(supervisor.userId, 'supervisor-token');
    await signedIn;

    expect(service.currentUser()).toEqual(supervisor);
    expect(service.accessToken()).toBe('supervisor-token');
  });

  it('remembers the selected demo user across reloads', async () => {
    const signedIn = firstValueFrom(service.signIn(supervisor.userId));
    answerTokenRequest(supervisor.userId, 't');
    await signedIn;

    expect(localStorage.getItem(SELECTED_USER_STORAGE_KEY)).toBe(supervisor.userId);
  });

  it('restores the remembered user on start-up, defaulting to the handler', async () => {
    const restoredDefault = firstValueFrom(service.restore());
    answerTokenRequest(handler.userId, 'h');
    await restoredDefault;
    expect(service.currentUser()).toEqual(handler);

    localStorage.setItem(SELECTED_USER_STORAGE_KEY, supervisor.userId);
    const restoredRemembered = firstValueFrom(service.restore());
    answerTokenRequest(supervisor.userId, 's');
    await restoredRemembered;
    expect(service.currentUser()).toEqual(supervisor);
  });

  it('ignores a remembered id that is not a demo user', async () => {
    localStorage.setItem(SELECTED_USER_STORAGE_KEY, 'not-a-demo-user');

    const restored = firstValueFrom(service.restore());
    answerTokenRequest(handler.userId, 'h');
    await restored;

    expect(service.currentUser()).toEqual(handler);
  });

  it('refreshes the token for the current user', async () => {
    const signedIn = firstValueFrom(service.signIn(supervisor.userId));
    answerTokenRequest(supervisor.userId, 'old');
    await signedIn;

    const refreshed = firstValueFrom(service.refreshToken());
    answerTokenRequest(supervisor.userId, 'new');

    expect(await refreshed).toBe('new');
    expect(service.accessToken()).toBe('new');
  });

  it('switching user changes the user only once the new token arrives', async () => {
    const first = firstValueFrom(service.signIn(handler.userId));
    answerTokenRequest(handler.userId, 'h');
    await first;

    const switched = firstValueFrom(service.signIn(supervisor.userId));
    expect(service.currentUser()).toEqual(handler);
    answerTokenRequest(supervisor.userId, 's');
    await switched;

    expect(service.currentUser()).toEqual(supervisor);
  });
});

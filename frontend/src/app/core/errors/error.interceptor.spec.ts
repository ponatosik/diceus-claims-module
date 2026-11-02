import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { ApiError } from './api-error';
import { errorInterceptor } from './error.interceptor';
import { NotificationService } from './notification.service';
import { ReloadRequests } from './reload-requests';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let notifications: { warning: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };
  let reloadRequests: ReloadRequests;

  beforeEach(() => {
    notifications = { warning: vi.fn(), error: vi.fn(() => of(false)) };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    reloadRequests = TestBed.inject(ReloadRequests);
  });

  afterEach(() => backend.verify());

  async function failWith(status: number, body: object | null = null): Promise<ApiError> {
    const result = firstValueFrom(http.get('/api/claims'));
    backend.expectOne('/api/claims').flush(body, { status, statusText: 'x' });
    return result.then(
      () => {
        throw new Error('expected the request to fail');
      },
      (error: ApiError) => error,
    );
  }

  it('rethrows a typed ApiError so callers can show 422 details inline', async () => {
    const error = await failWith(422, { title: 'Invalid.', errors: { LossDate: ['Future.'] } });

    expect(error.kind).toBe('validation');
    expect(error.errors).toEqual({ LossDate: ['Future.'] });
  });

  it('shows a warning snackbar for a validation 422', async () => {
    await failWith(422, {
      title: 'One or more validation errors occurred.',
      errors: { LossDate: ['Future.'] },
    });

    expect(notifications.warning).toHaveBeenCalledWith('One or more validation errors occurred.');
  });

  it('shows the first blocking condition for a business-rule 422', async () => {
    await failWith(422, {
      title: 'Rule.',
      blockingConditions: [{ code: 'BR-R-03', message: 'Self-approval is not permitted.' }],
    });

    expect(notifications.warning).toHaveBeenCalledWith('Self-approval is not permitted.');
  });

  it.each([404, 500, 0])('shows an error snackbar for HTTP %i', async (status) => {
    await failWith(status);

    expect(notifications.error).toHaveBeenCalledTimes(1);
  });

  it('offers Reload on a 409 conflict and broadcasts a reload request when chosen', async () => {
    notifications.error.mockReturnValue(of(true));
    const before = reloadRequests.count();

    await failWith(409);

    expect(notifications.error).toHaveBeenCalledWith(expect.any(String), 'Reload');
    expect(reloadRequests.count()).toBe(before + 1);
  });
});

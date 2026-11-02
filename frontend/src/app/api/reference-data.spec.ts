import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { CachedReferenceData } from './reference-data';
import { apiTestProviders } from './test-providers';

const URL = 'https://api.test/api/reference/claim-statuses';

describe('CachedReferenceData', () => {
  let data: CachedReferenceData;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: apiTestProviders });
    data = TestBed.inject(CachedReferenceData);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('fetches the claim-status transition table once per session', async () => {
    const first = firstValueFrom(data.claimStatuses());
    http.expectOne(URL).flush([{ status: 'Draft', validNextStatuses: ['Open'] }]);
    await first;

    const second = await firstValueFrom(data.claimStatuses());

    expect(second).toEqual([{ status: 'Draft', validNextStatuses: ['Open'] }]);
    http.expectNone(URL);
  });

  it('does not cache a failure — the next caller retries', async () => {
    const failed = firstValueFrom(data.claimStatuses());
    http.expectOne(URL).flush(null, { status: 500, statusText: 'x' });
    await expect(failed).rejects.toBeTruthy();

    const retried = firstValueFrom(data.claimStatuses());
    http.expectOne(URL).flush([]);
    expect(await retried).toEqual([]);
  });
});

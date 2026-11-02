import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReservesApi } from './reserves-api';
import { apiTestProviders } from './test-providers';

describe('ReservesApi', () => {
  let api: ReservesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: apiTestProviders });
    api = TestBed.inject(ReservesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists and opens reserves', () => {
    api.list('c1').subscribe();
    api
      .open('c1', {
        component: 'Indemnity',
        amount: 25_000,
        changeReason: 'Estimate',
        transactionType: 'Add',
      })
      .subscribe();

    http.expectOne(
      (r) => r.method === 'GET' && r.url === 'https://api.test/api/claims/c1/reserves',
    );
    const open = http.expectOne(
      (r) => r.method === 'POST' && r.url === 'https://api.test/api/claims/c1/reserves',
    );
    expect(open.request.body).toEqual({
      component: 'Indemnity',
      amount: 25_000,
      changeReason: 'Estimate',
      transactionType: 'Add',
    });
  });

  it('posts each transaction action to its own route', () => {
    api.approve('c1', 't1').subscribe();
    api.reject('c1', 't1', 'Not supported by evidence').subscribe();
    api.retract('c1', 't1').subscribe();
    api.retryPosting('c1', 't1').subscribe();
    api.setAggregateLimitOverride('c1').subscribe();

    expect(
      http.expectOne('https://api.test/api/claims/c1/reserves/t1/approve').request.method,
    ).toBe('POST');
    expect(
      http.expectOne('https://api.test/api/claims/c1/reserves/t1/reject').request.body,
    ).toEqual({
      rejectionReason: 'Not supported by evidence',
    });
    expect(
      http.expectOne('https://api.test/api/claims/c1/reserves/t1/retract').request.method,
    ).toBe('POST');
    expect(
      http.expectOne('https://api.test/api/claims/c1/reserves/t1/retry-posting').request.method,
    ).toBe('POST');
    expect(http.expectOne('https://api.test/api/claims/c1/reserves/override').request.method).toBe(
      'POST',
    );
  });
});

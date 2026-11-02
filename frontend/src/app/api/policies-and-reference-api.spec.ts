import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PoliciesApi } from './policies-api';
import { ReferenceApi } from './reference-api';
import { apiTestProviders } from './test-providers';

describe('PoliciesApi and ReferenceApi', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: apiTestProviders });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('searches policies by q and fetches a policy coverage', () => {
    const policies = TestBed.inject(PoliciesApi);
    policies.search('Meridian').subscribe();
    policies.coverage('p1').subscribe();

    const search = http.expectOne((r) => r.url === 'https://api.test/api/policies/search');
    expect(search.request.params.get('q')).toBe('Meridian');
    http.expectOne('https://api.test/api/policies/p1/coverage');
  });

  it('loads cause-of-loss codes and the claim-status transition table', () => {
    const reference = TestBed.inject(ReferenceApi);
    reference.causeOfLossCodes().subscribe();
    reference.claimStatuses().subscribe();

    http.expectOne('https://api.test/api/reference/cause-of-loss-codes');
    http.expectOne('https://api.test/api/reference/claim-statuses');
  });
});

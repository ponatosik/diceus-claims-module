import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ClaimsApi } from './claims-api';
import { apiTestProviders } from './test-providers';

describe('ClaimsApi', () => {
  let api: ClaimsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: apiTestProviders });
    api = TestBed.inject(ClaimsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists claims with repeated status params and only the filters that are set', () => {
    api
      .list({
        statuses: ['Open', 'Closed'],
        assignedHandler: 'morg',
        dateFrom: '2026-01-01',
        page: 2,
        pageSize: 50,
      })
      .subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/claims');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.getAll('status')).toEqual(['Open', 'Closed']);
    expect(request.request.params.get('assignedHandler')).toBe('morg');
    expect(request.request.params.get('dateFrom')).toBe('2026-01-01');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.has('search')).toBe(false);
    expect(request.request.params.has('dateTo')).toBe(false);
  });

  it('passes the assigned handler id filter through', () => {
    api.list({ assignedHandlerId: 'u1', causeOfLossCode: 'COL-FIRE' }).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/claims');
    expect(request.request.params.get('assignedHandlerId')).toBe('u1');
    expect(request.request.params.get('causeOfLossCode')).toBe('COL-FIRE');
  });

  it('drops blank text filters', () => {
    api.list({ search: '  ', assignedHandler: '' }).subscribe();

    const request = http.expectOne((r) => r.url === 'https://api.test/api/claims');
    expect(request.request.params.keys()).toEqual([]);
  });

  it('gets claim detail, audit pages and a transition preflight', () => {
    api.get('c1').subscribe();
    api.audit('c1', 3, 25).subscribe();
    api.preflight('c1', 'Closed').subscribe();

    http.expectOne('https://api.test/api/claims/c1');
    const audit = http.expectOne((r) => r.url === 'https://api.test/api/claims/c1/audit');
    expect(audit.request.params.get('page')).toBe('3');
    expect(audit.request.params.get('pageSize')).toBe('25');
    const preflight = http.expectOne(
      (r) => r.url === 'https://api.test/api/claims/c1/status/preflight',
    );
    expect(preflight.request.params.get('target')).toBe('Closed');
  });

  it('sends commands with the expected verbs and bodies', () => {
    api.transition('c1', { targetStatus: 'Withdrawn', reason: 'Claimant withdrew.' }).subscribe();
    api.updateNotes('c1', 'Called the insured.').subscribe();
    api.removeParty('c1', 'p1').subscribe();

    const transition = http.expectOne('https://api.test/api/claims/c1/status');
    expect(transition.request.method).toBe('PUT');
    expect(transition.request.body).toEqual({
      targetStatus: 'Withdrawn',
      reason: 'Claimant withdrew.',
    });
    const notes = http.expectOne('https://api.test/api/claims/c1/notes');
    expect(notes.request.method).toBe('PATCH');
    expect(notes.request.body).toEqual({ notes: 'Called the insured.' });
    expect(http.expectOne('https://api.test/api/claims/c1/parties/p1').request.method).toBe(
      'DELETE',
    );
  });

  it('posts FNOL to create and to validate', () => {
    const body = {
      policyId: null,
      lossDate: '2026-10-01T10:00:00.000Z',
      lossDescription: 'A pipe burst and flooded the basement overnight.',
      lossLocation: null,
      causeOfLossCode: 'COL-FIRE',
      estimatedLossAmount: null,
      parties: [],
      riskObjects: [],
      initialReserve: null,
    };
    api.create(body).subscribe();
    api.validate(body).subscribe();

    expect(http.expectOne('https://api.test/api/claims').request.body).toEqual(body);
    expect(http.expectOne('https://api.test/api/claims/validate').request.method).toBe('POST');
  });
});

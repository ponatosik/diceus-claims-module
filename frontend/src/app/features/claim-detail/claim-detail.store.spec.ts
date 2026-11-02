import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import { ClaimDetail } from '../../api/models';
import { ApiError } from '../../core/errors/api-error';
import { ReloadRequests } from '../../core/errors/reload-requests';
import { ClaimDetailStore } from './claim-detail.store';

const detail = (overrides: Partial<ClaimDetail> = {}) =>
  ({ claimId: 'c-1', status: 'Open', notes: null, ...overrides }) as ClaimDetail;

describe('ClaimDetailStore', () => {
  let api: Record<string, ReturnType<typeof vi.fn>>;
  let store: ClaimDetailStore;
  let navigate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    api = {
      get: vi.fn(() => of(detail())),
      transition: vi.fn(() => of({})),
      updateNotes: vi.fn(() => of(undefined)),
      addParty: vi.fn(() => of({})),
      removeParty: vi.fn(() => of(undefined)),
    };
    navigate = vi.fn(() => Promise.resolve(true));
    TestBed.configureTestingModule({
      providers: [
        ClaimDetailStore,
        { provide: ClaimsApi, useValue: api },
        { provide: Router, useValue: { navigate } },
      ],
    });
    store = TestBed.inject(ClaimDetailStore);
    store.load('c-1');
    TestBed.tick();
  });

  it('loads the claim for the routed id', () => {
    expect(api['get']).toHaveBeenCalledWith('c-1');
    expect(store.claim()?.claimId).toBe('c-1');
  });

  it.each([
    ['transition', () => store.transition({ targetStatus: 'UnderInvestigation' })],
    ['notes update', () => store.updateNotes('Called the insured.')],
    [
      'party added',
      () =>
        store.addParty({
          partyRole: 'Witness',
          partyType: 'Person',
          firstName: 'A',
          lastName: 'B',
          companyName: null,
          email: null,
          phone: null,
        }),
    ],
    ['party removed', () => store.removeParty('p-1')],
  ])('after a %s, re-fetches the claim and the audit log from the server', async (_, mutate) => {
    api['get'].mockReturnValue(of(detail({ status: 'UnderInvestigation' })));
    const auditBefore = store.auditRevision();

    await firstValueFrom(mutate());
    TestBed.tick();

    expect(api['get']).toHaveBeenCalledTimes(2);
    expect(store.claim()?.status).toBe('UnderInvestigation');
    expect(store.auditRevision()).toBe(auditBefore + 1);
  });

  it('after a reserve change, re-fetches the claim (header) and bumps the reserves and audit revisions', () => {
    const before = { audit: store.auditRevision(), reserves: store.reservesRevision() };

    store.refreshAfterReserveChange();
    TestBed.tick();

    expect(api['get']).toHaveBeenCalledTimes(2);
    expect(store.auditRevision()).toBe(before.audit + 1);
    expect(store.reservesRevision()).toBe(before.reserves + 1);
  });

  it('does not re-fetch when the mutation fails', async () => {
    api['transition'].mockReturnValue(throwError(() => ({ kind: 'businessRule' }) as ApiError));

    await expect(firstValueFrom(store.transition({ targetStatus: 'Closed' }))).rejects.toBeTruthy();
    TestBed.tick();

    expect(api['get']).toHaveBeenCalledTimes(1);
  });

  it('re-fetches everything when a 409 conflict asks for a reload', () => {
    const before = { reserves: store.reservesRevision(), documents: store.documentsRevision() };

    TestBed.inject(ReloadRequests).request();
    TestBed.tick();

    expect(api['get']).toHaveBeenCalledTimes(2);
    expect(store.reservesRevision()).toBe(before.reserves + 1);
    expect(store.documentsRevision()).toBe(before.documents + 1);
  });

  it('shows the not-found page for an unknown claim, keeping the URL', () => {
    const notFound: ApiError = {
      kind: 'notFound',
      status: 404,
      message: 'Not found.',
      errors: {},
      blockingConditions: [],
    };
    api['get'].mockReturnValue(throwError(() => notFound));

    store.load('missing');
    TestBed.tick();

    expect(navigate).toHaveBeenCalledWith(['/not-found'], { skipLocationChange: true });
  });
});

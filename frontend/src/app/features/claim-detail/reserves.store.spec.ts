import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { ReserveList, ReserveTransaction } from '../../api/models';
import { ReservesApi } from '../../api/reserves-api';
import { ClaimDetailStore } from './claim-detail.store';
import { ReservesStore } from './reserves.store';

const list = (history: Partial<ReserveTransaction>[]): ReserveList => ({
  components: [],
  history: history as ReserveTransaction[],
});
const inFlight = list([
  { reserveHistoryId: 't1', approvalStatus: 'AutoApproved', postingStatus: 'Pending' },
]);
const settled = list([
  { reserveHistoryId: 't1', approvalStatus: 'AutoApproved', postingStatus: 'Posted' },
]);

describe('ReservesStore', () => {
  let api: Record<string, ReturnType<typeof vi.fn>>;
  let detail: {
    claimId: ReturnType<typeof signal<string | null>>;
    reservesRevision: ReturnType<typeof signal<number>>;
    refreshAfterReserveChange: ReturnType<typeof vi.fn>;
  };

  function createStore(first: ReserveList) {
    api = {
      list: vi.fn(() => of(first)),
      approve: vi.fn(() => of({})),
      reject: vi.fn(() => of({})),
      retract: vi.fn(() => of({})),
      retryPosting: vi.fn(() => of({})),
      open: vi.fn(() => of({})),
      setAggregateLimitOverride: vi.fn(() => of(undefined)),
    };
    detail = {
      claimId: signal<string | null>('c-1'),
      reservesRevision: signal(0),
      refreshAfterReserveChange: vi.fn(),
    };
    TestBed.configureTestingModule({
      providers: [
        ReservesStore,
        { provide: ReservesApi, useValue: api },
        { provide: ClaimDetailStore, useValue: detail },
      ],
    });
    const store = TestBed.inject(ReservesStore);
    TestBed.tick();
    return store;
  }

  afterEach(() => vi.useRealTimers());

  it('loads the claim’s reserves', () => {
    const store = createStore(settled);

    expect(api['list']).toHaveBeenCalledWith('c-1');
    expect(store.history()).toHaveLength(1);
  });

  it('re-loads when the claim detail store bumps the reserves revision', () => {
    createStore(settled);

    detail.reservesRevision.set(1);
    TestBed.tick();

    expect(api['list']).toHaveBeenCalledTimes(2);
  });

  it('polls every 5 seconds while a GL posting is in flight, and stops once it has posted', () => {
    vi.useFakeTimers();
    const store = createStore(inFlight);
    expect(store.pollingPostings()).toBe(true);

    vi.advanceTimersByTime(5000);
    TestBed.tick();
    expect(api['list']).toHaveBeenCalledTimes(2);

    api['list'].mockReturnValue(of(settled));
    vi.advanceTimersByTime(5000);
    TestBed.tick();
    expect(api['list']).toHaveBeenCalledTimes(3);
    expect(store.pollingPostings()).toBe(false);

    vi.advanceTimersByTime(20_000);
    TestBed.tick();
    expect(api['list']).toHaveBeenCalledTimes(3);
  });

  it.each([
    ['approve', (s: ReservesStore) => s.approve('t1')],
    ['reject', (s: ReservesStore) => s.reject('t1', 'Not supported')],
    ['retract', (s: ReservesStore) => s.retract('t1')],
    ['retry posting', (s: ReservesStore) => s.retryPosting('t1')],
    [
      'submit',
      (s: ReservesStore) =>
        s.submit({ component: 'Expense', amount: 100, changeReason: 'x', transactionType: 'Add' }),
    ],
    ['set override', (s: ReservesStore) => s.setAggregateLimitOverride()],
  ])('after %s, refreshes reserves, header and audit log', async (_, action) => {
    const store = createStore(settled);

    await firstValueFrom(action(store));

    expect(detail.refreshAfterReserveChange).toHaveBeenCalledTimes(1);
  });
});

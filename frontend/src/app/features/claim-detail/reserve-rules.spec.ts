import { ReserveSummary, ReserveTransaction } from '../../api/models';
import { DEMO_USERS } from '../../core/auth/demo-users';
import {
  approvalAction,
  canRetract,
  canRetryPosting,
  hasPostingsInFlight,
  needsAggregateLimitOverride,
  plannedChange,
  reversalOf,
} from './reserve-rules';

const [handler, supervisor, manager] = DEMO_USERS;

const component = (overrides: Partial<ReserveSummary>): ReserveSummary => ({
  reserveComponentId: 'rc',
  component: 'Indemnity',
  currentAmount: 0,
  status: 'Active',
  pendingAmount: 0,
  ...overrides,
});

const txn = (overrides: Partial<ReserveTransaction>): ReserveTransaction =>
  ({
    reserveHistoryId: 't',
    amount: 5000,
    approvalStatus: 'PendingApproval',
    postingStatus: 'Pending',
    submittedByUserId: handler.userId,
    ...overrides,
  }) as ReserveTransaction;

describe('plannedChange (what the Add Reserve panel will submit)', () => {
  it('opens a component that does not exist yet with an Add', () => {
    expect(plannedChange([], 'Expense', 4000)).toEqual({
      transactionType: 'Add',
      currentBalance: 0,
      newBalance: 4000,
    });
  });

  it('adjusts an existing component by a signed delta, previewing the new balance', () => {
    const components = [component({ component: 'Indemnity', currentAmount: 25000 })];

    expect(plannedChange(components, 'Indemnity', -5000)).toEqual({
      transactionType: 'Adjust',
      currentBalance: 25000,
      newBalance: 20000,
    });
  });
});

describe('reversalOf', () => {
  it('cancels the component’s current balance exactly', () => {
    expect(reversalOf(component({ component: 'ALAE', currentAmount: 7250 }))).toEqual({
      component: 'ALAE',
      amount: -7250,
      transactionType: 'Reverse',
      changeReason: 'Reserve reversed',
    });
  });
});

describe('approvalAction (FR-ROLE-01, BR-R-02, BR-R-03)', () => {
  it('is hidden from handlers and for transactions not awaiting approval', () => {
    expect(approvalAction(txn({}), handler).visible).toBe(false);
    expect(approvalAction(txn({ approvalStatus: 'Approved' }), supervisor).visible).toBe(false);
  });

  it('is enabled for a supervisor on a supervisor-tier transaction someone else submitted', () => {
    expect(approvalAction(txn({ amount: 50_000 }), supervisor)).toEqual({
      visible: true,
      enabled: true,
      reason: null,
    });
  });

  it('is disabled, with a reason, when the tier is above the user’s authority', () => {
    const action = approvalAction(txn({ amount: 250_000 }), supervisor);

    expect(action.enabled).toBe(false);
    expect(action.reason).toMatch(/Manager approval/);
    expect(approvalAction(txn({ amount: 250_000 }), manager).enabled).toBe(true);
  });

  it('is disabled for the submitter’s own transaction (no self-approval)', () => {
    const action = approvalAction(
      txn({ amount: 50_000, submittedByUserId: supervisor.userId }),
      supervisor,
    );

    expect(action.enabled).toBe(false);
    expect(action.reason).toMatch(/BR-R-03/);
  });
});

describe('row actions', () => {
  it('lets only the Submitter retract, and only while pending', () => {
    expect(canRetract(txn({}), handler.userId)).toBe(true);
    expect(canRetract(txn({}), supervisor.userId)).toBe(false);
    expect(canRetract(txn({ approvalStatus: 'Approved' }), handler.userId)).toBe(false);
  });

  it('offers a GL posting retry only for Failed postings', () => {
    expect(canRetryPosting(txn({ approvalStatus: 'Approved', postingStatus: 'Failed' }))).toBe(
      true,
    );
    expect(canRetryPosting(txn({ approvalStatus: 'Approved', postingStatus: 'Pending' }))).toBe(
      false,
    );
  });
});

describe('hasPostingsInFlight (drives the 5s poll)', () => {
  it('is true while an approved transaction waits for its GL posting', () => {
    expect(
      hasPostingsInFlight([txn({ approvalStatus: 'AutoApproved', postingStatus: 'Pending' })]),
    ).toBe(true);
    expect(
      hasPostingsInFlight([txn({ approvalStatus: 'Approved', postingStatus: 'Pending' })]),
    ).toBe(true);
  });

  it('ignores transactions still awaiting approval (no posting is enqueued yet) and settled ones', () => {
    expect(
      hasPostingsInFlight([txn({ approvalStatus: 'PendingApproval', postingStatus: 'Pending' })]),
    ).toBe(false);
    expect(
      hasPostingsInFlight([txn({ approvalStatus: 'Approved', postingStatus: 'Posted' })]),
    ).toBe(false);
  });
});

describe('needsAggregateLimitOverride (BR-R-05)', () => {
  const pending = [txn({ amount: 600_000 })];

  it('is needed when approving pending transactions would take total reserves past $10,000,000', () => {
    const components = [component({ currentAmount: 9_500_000 })];

    expect(needsAggregateLimitOverride(components, pending, false)).toBe(true);
  });

  it('is not needed within the limit, or once the override is set', () => {
    expect(
      needsAggregateLimitOverride([component({ currentAmount: 9_000_000 })], pending, false),
    ).toBe(false);
    expect(
      needsAggregateLimitOverride([component({ currentAmount: 9_500_000 })], pending, true),
    ).toBe(false);
  });
});

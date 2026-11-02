import { computed, signal } from '@angular/core';
import { render, screen, within } from '@testing-library/angular';
import { of } from 'rxjs';
import { ClaimDetail, ReserveSummary, ReserveTransaction } from '../../api/models';
import { DEMO_USERS, DemoUser } from '../../core/auth/demo-users';
import { MockAuthService } from '../../core/auth/mock-auth.service';
import { canSetAggregateLimitOverride } from '../../core/auth/permissions';
import { NotificationService } from '../../core/errors/notification.service';
import { ClaimDetailStore } from './claim-detail.store';
import { ClaimReservesTab } from './claim-reserves-tab';
import { ReservesStore } from './reserves.store';

const [handler, supervisor, manager] = DEMO_USERS;

const txn = (overrides: Partial<ReserveTransaction>): ReserveTransaction => ({
  reserveHistoryId: 't',
  reserveComponentId: 'rc',
  component: 'Indemnity',
  transactionType: 'Add',
  amount: 50_000,
  previousBalance: 0,
  newBalance: 50_000,
  approvalStatus: 'PendingApproval',
  approvedByUserId: null,
  approvedAt: null,
  rejectedByUserId: null,
  rejectedAt: null,
  rejectionReason: null,
  changeReason: 'Estimate',
  postingStatus: 'Pending',
  submittedByUserId: handler.userId,
  createdAt: '2026-11-01T10:00:00Z',
  submittedByDisplayName: handler.displayName,
  approvedByDisplayName: null,
  rejectedByDisplayName: null,
  ...overrides,
});

async function renderTab(
  user: DemoUser,
  history: ReserveTransaction[],
  components: ReserveSummary[] = [],
  overrideApproved = false,
) {
  const currentUser = signal(user);
  await render(ClaimReservesTab, {
    providers: [
      {
        provide: MockAuthService,
        useValue: {
          currentUser,
          canSetAggregateLimitOverride: computed(() =>
            canSetAggregateLimitOverride(currentUser().role),
          ),
        },
      },
      {
        provide: ClaimDetailStore,
        useValue: {
          claim: signal({
            claimId: 'c-1',
            reserveOverrideApproved: overrideApproved,
          } as ClaimDetail),
        },
      },
      {
        provide: NotificationService,
        useValue: { success: vi.fn(), warning: vi.fn(), error: vi.fn(() => of(false)) },
      },
    ],
    componentProviders: [
      {
        provide: ReservesStore,
        useValue: {
          components: signal(components),
          history: signal(history),
          loaded: signal(true),
          pollingPostings: signal(false),
        },
      },
    ],
  });
}

const row = (id: string) => screen.getByTestId(`txn-${id}`);
const button = (id: string, name: RegExp) =>
  within(row(id)).queryByRole('button', { name }) as HTMLButtonElement | null;

describe('ClaimReservesTab (§11.3 Tab 3)', () => {
  it('never shows Approve/Reject to a handler (FR-ROLE-01)', async () => {
    await renderTab(handler, [
      txn({ reserveHistoryId: 'a', submittedByUserId: supervisor.userId }),
    ]);

    expect(button('a', /^Approve/)).toBeNull();
    expect(button('a', /^Reject/)).toBeNull();
  });

  it('shows a supervisor Approve/Reject, disabled on a Manager-tier row and on their own submission', async () => {
    await renderTab(supervisor, [
      txn({ reserveHistoryId: 'ok', amount: 50_000 }),
      txn({ reserveHistoryId: 'big', amount: 250_000 }),
      txn({ reserveHistoryId: 'own', amount: 50_000, submittedByUserId: supervisor.userId }),
    ]);

    expect(button('ok', /^Approve/)!.disabled).toBe(false);
    expect(button('ok', /^Reject/)!.disabled).toBe(false);
    expect(button('big', /^Approve/)!.disabled).toBe(true);
    expect(button('own', /^Approve/)!.disabled).toBe(true);
  });

  it('offers Retract only on the Submitter’s own pending rows', async () => {
    await renderTab(handler, [
      txn({ reserveHistoryId: 'mine' }),
      txn({ reserveHistoryId: 'theirs', submittedByUserId: supervisor.userId }),
      txn({ reserveHistoryId: 'done', approvalStatus: 'Approved', postingStatus: 'Posted' }),
    ]);

    expect(button('mine', /^Retract/)).not.toBeNull();
    expect(button('theirs', /^Retract/)).toBeNull();
    expect(button('done', /^Retract/)).toBeNull();
  });

  it('offers Retry only on Failed GL postings', async () => {
    await renderTab(handler, [
      txn({ reserveHistoryId: 'failed', approvalStatus: 'Approved', postingStatus: 'Failed' }),
      txn({ reserveHistoryId: 'posted', approvalStatus: 'Approved', postingStatus: 'Posted' }),
    ]);

    expect(button('failed', /^Retry/)).not.toBeNull();
    expect(button('posted', /^Retry/)).toBeNull();
  });

  it('shows amounts signed and coloured: green for increases, red for decreases', async () => {
    await renderTab(handler, [
      txn({ reserveHistoryId: 'up', amount: 5000, approvalStatus: 'AutoApproved' }),
      txn({
        reserveHistoryId: 'down',
        amount: -2000,
        transactionType: 'Adjust',
        approvalStatus: 'AutoApproved',
      }),
    ]);

    expect(within(row('up')).getByText('+$5,000.00').classList).toContain('increase');
    expect(within(row('down')).getByText('-$2,000.00').classList).toContain('decrease');
  });

  it('offers the Aggregate Limit Override only to a manager, only when the $10M limit would be exceeded', async () => {
    const components: ReserveSummary[] = [
      {
        reserveComponentId: 'rc',
        component: 'Indemnity',
        currentAmount: 9_900_000,
        status: 'Active',
        pendingAmount: 0,
      },
    ];
    const pending = [
      txn({ reserveHistoryId: 'p', amount: 500_000, submittedByUserId: handler.userId }),
    ];

    await renderTab(manager, pending, components);
    expect(screen.getByRole('button', { name: /aggregate-limit override/i })).toBeTruthy();
  });

  it('does not offer the override to a supervisor', async () => {
    const components: ReserveSummary[] = [
      {
        reserveComponentId: 'rc',
        component: 'Indemnity',
        currentAmount: 9_900_000,
        status: 'Active',
        pendingAmount: 0,
      },
    ];
    await renderTab(supervisor, [txn({ reserveHistoryId: 'p', amount: 500_000 })], components);

    expect(screen.queryByRole('button', { name: /aggregate-limit override/i })).toBeNull();
  });

  it('shows “Override approved” once the manager has set it', async () => {
    await renderTab(handler, [], [], true);

    expect(screen.getByText('Override approved')).toBeTruthy();
  });
});

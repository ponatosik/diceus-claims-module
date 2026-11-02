import {
  OpenReserveRequest,
  ReserveComponentType,
  ReserveSummary,
  ReserveTransaction,
  ReserveTransactionType,
} from '../../api/models';
import { DemoUser } from '../../core/auth/demo-users';
import { canApproveReserves, canApproveTier } from '../../core/auth/permissions';
import { authorityTier } from '../../shared/domain/authority-tier';

// Pure rules behind the Reserves tab (§11.3 Tab 3, spec 011). Each mirrors a backend check so the UI
// offers exactly what the API will accept — the API stays the authority either way.

export interface PlannedChange {
  transactionType: Extract<ReserveTransactionType, 'Add' | 'Adjust'>;
  currentBalance: number;
  newBalance: number;
}

/** The Add Reserve panel picks the type itself: Add opens a new component, Adjust changes one by a signed delta (ADR-0011). */
export function plannedChange(
  components: readonly ReserveSummary[],
  component: ReserveComponentType,
  amount: number,
): PlannedChange {
  const existing = components.find((summary) => summary.component === component);
  const currentBalance = existing?.currentAmount ?? 0;
  return {
    transactionType: existing === undefined ? 'Add' : 'Adjust',
    currentBalance,
    newBalance: currentBalance + amount,
  };
}

/** A Reverse exactly cancels the component's current balance (ADR-0011, RESERVE-REVERSE-01). */
export function reversalOf(summary: ReserveSummary, changeReason: string): OpenReserveRequest {
  return {
    component: summary.component,
    amount: -summary.currentAmount,
    transactionType: 'Reverse',
    changeReason,
  };
}

/**
 * ADR-0011 mirrored for instant feedback in the Add Reserve panel: a new component must open with a
 * positive amount, and no balance may go below zero — SubrogationRecoverable excepted in both.
 */
export function amountProblem(
  components: readonly ReserveSummary[],
  component: ReserveComponentType,
  amount: number | null,
): string | null {
  if (amount === null || amount === 0 || component === 'SubrogationRecoverable') return null;
  const plan = plannedChange(components, component, amount);
  if (plan.transactionType === 'Add' && amount < 0)
    return 'A new reserve must open with a positive amount.';
  if (plan.newBalance < 0) return 'The reserve can’t go below zero.';
  return null;
}

export interface ApprovalAction {
  visible: boolean;
  enabled: boolean;
  /** Why it's disabled — shown as a tooltip. */
  reason: string | null;
}

/**
 * Approve/Reject on a transaction (FR-ROLE-01): visible to Supervisors and Managers on PendingApproval
 * rows; disabled when the Authority Tier is above the user's role (BR-R-02) or the user is the
 * Submitter (BR-R-03).
 */
export function approvalAction(transaction: ReserveTransaction, user: DemoUser): ApprovalAction {
  if (!canApproveReserves(user.role) || transaction.approvalStatus !== 'PendingApproval') {
    return { visible: false, enabled: false, reason: null };
  }
  if (transaction.submittedByUserId === user.userId) {
    return {
      visible: true,
      enabled: false,
      reason: 'You can’t approve your own reserve (BR-R-03).',
    };
  }
  if (!canApproveTier(user.role, authorityTier(transaction.amount))) {
    return { visible: true, enabled: false, reason: 'This amount needs Manager approval.' };
  }
  return { visible: true, enabled: true, reason: null };
}

/** §6.4: only the Submitter retracts, and only before approval. */
export function canRetract(transaction: ReserveTransaction, userId: string): boolean {
  return (
    transaction.approvalStatus === 'PendingApproval' && transaction.submittedByUserId === userId
  );
}

export function canRetryPosting(transaction: ReserveTransaction): boolean {
  return transaction.postingStatus === 'Failed';
}

/** Approved, manually or automatically — the point at which a GL posting is enqueued (§6.5). */
export function isApproved(transaction: ReserveTransaction): boolean {
  return transaction.approvalStatus === 'Approved' || transaction.approvalStatus === 'AutoApproved';
}

/** An approved transaction whose GL posting hasn't finished yet — the tab polls while any exist (spec 011 story 10). */
export function hasPostingsInFlight(history: readonly ReserveTransaction[]): boolean {
  return history.some(
    (transaction) =>
      (transaction.approvalStatus === 'Approved' ||
        transaction.approvalStatus === 'AutoApproved') &&
      transaction.postingStatus === 'Pending',
  );
}

const AGGREGATE_LIMIT = 10_000_000;

/**
 * BR-R-05: approving some pending transaction would take total approved reserves past $10,000,000
 * without the override. Checked per transaction against the approved total, exactly like the API
 * (ReserveAggregateLimit) — so a pending decrease can't mask a pending increase.
 */
export function needsAggregateLimitOverride(
  components: readonly ReserveSummary[],
  history: readonly ReserveTransaction[],
  overrideApproved: boolean,
): boolean {
  if (overrideApproved) return false;
  const approvedTotal = components.reduce((sum, summary) => sum + summary.currentAmount, 0);
  return history.some(
    (transaction) =>
      transaction.approvalStatus === 'PendingApproval' &&
      Math.abs(approvedTotal + transaction.amount) > AGGREGATE_LIMIT,
  );
}

import { CurrencyPipe, DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, filter, switchMap } from 'rxjs';
import { ReserveSummary, ReserveTransaction } from '../../api/models';
import { MockAuthService } from '../../core/auth/mock-auth.service';
import { isApiError } from '../../core/errors/api-error';
import { NotificationService } from '../../core/errors/notification.service';
import { reserveComponentLabel } from '../../shared/domain/labels';
import { confirm } from '../../shared/ui/confirm-dialog';
import { PendingTracker } from '../../shared/ui/pending-tracker';
import { ClaimDetailStore } from './claim-detail.store';
import { ReasonDialog, ReasonDialogData } from './reason-dialog';
import { ReservePanel } from './reserve-panel';
import {
  approvalAction,
  canRetract,
  canRetryPosting,
  isApproved,
  needsAggregateLimitOverride,
  reversalOf,
} from './reserve-rules';
import { ReservesStore } from './reserves.store';

/**
 * §11.3 Tab 3 — Reserves: per-component balance cards, the transaction history with role-aware row
 * actions, the slide-in Add Reserve panel, and live GL posting status (spec 011).
 */
@Component({
  selector: 'app-claim-reserves-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSidenavModule,
    MatTooltipModule,
    ReservePanel,
  ],
  providers: [ReservesStore],
  templateUrl: './claim-reserves-tab.html',
  styleUrl: './claim-reserves-tab.scss',
})
export class ClaimReservesTab {
  /** `?highlight=<reserveHistoryId>` from an audit-log link (spec 011 story 12). */
  readonly highlight = input<string | undefined>(undefined);

  protected readonly store = inject(ReservesStore);
  private readonly detail = inject(ClaimDetailStore);
  private readonly auth = inject(MockAuthService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly componentLabel = reserveComponentLabel;
  protected readonly busy = new PendingTracker();

  protected readonly canRetry = canRetryPosting;
  protected readonly overrideApproved = computed(
    () => this.detail.claim()?.reserveOverrideApproved === true,
  );
  /** An approval the API refused with BR-R-05 — the other trigger for offering the override (spec 011 story 9). */
  private readonly aggregateLimitHit = signal(false);
  protected readonly offerOverride = computed(
    () =>
      this.auth.canSetAggregateLimitOverride() &&
      !this.overrideApproved() &&
      (this.aggregateLimitHit() ||
        needsAggregateLimitOverride(
          this.store.components(),
          this.store.history(),
          this.overrideApproved(),
        )),
  );

  constructor() {
    // Scroll an audit-linked transaction into view once the history has rendered.
    effect(() => {
      const id = this.highlight();
      if (id === undefined || !this.store.loaded()) return;
      queueMicrotask(() =>
        document
          .querySelector(`[data-testid="txn-${id}"]`)
          ?.scrollIntoView({ block: 'center', behavior: 'smooth' }),
      );
    });
  }

  protected approvalFor(transaction: ReserveTransaction) {
    return approvalAction(transaction, this.auth.currentUser());
  }

  protected canRetract(transaction: ReserveTransaction): boolean {
    return canRetract(transaction, this.auth.currentUser().userId);
  }

  /** The GL badge is only meaningful once the transaction is approved — nothing is enqueued before that. */
  protected postingLabel(transaction: ReserveTransaction): string | null {
    return isApproved(transaction) ? transaction.postingStatus : null;
  }

  protected approve(transaction: ReserveTransaction): void {
    this.busy.track(this.store.approve(transaction.reserveHistoryId)).subscribe({
      next: () => this.notifications.success('Reserve approved.'),
      error: (error: unknown) => {
        // The snackbar already shows why; a BR-R-05 refusal also offers the manager the override.
        if (
          isApiError(error) &&
          error.blockingConditions.some((condition) => condition.code === 'BR-R-05')
        ) {
          this.aggregateLimitHit.set(true);
        }
      },
    });
  }

  protected reject(transaction: ReserveTransaction): void {
    this.askForReason(
      {
        title: 'Reject reserve',
        label: 'Rejection reason',
        confirmLabel: 'Reject',
        submit: (reason) => this.store.reject(transaction.reserveHistoryId, reason),
      },
      'Reserve rejected.',
    );
  }

  protected retract(transaction: ReserveTransaction): void {
    this.confirmThen(
      {
        title: 'Retract reserve?',
        message: 'The pending transaction will be cancelled.',
        confirmLabel: 'Retract',
      },
      () => this.store.retract(transaction.reserveHistoryId),
      'Reserve retracted.',
    );
  }

  protected retry(transaction: ReserveTransaction): void {
    this.run(this.store.retryPosting(transaction.reserveHistoryId), 'GL posting re-queued.');
  }

  protected reverse(summary: ReserveSummary): void {
    const label = reserveComponentLabel(summary.component);
    this.askForReason(
      {
        title: `Reverse the ${label} reserve?`,
        message: 'A Reverse transaction will bring this reserve’s balance to zero.',
        label: 'Reason for the reversal',
        confirmLabel: 'Reverse',
        submit: (reason) => this.store.submit(reversalOf(summary, reason)),
      },
      `${label} reverse submitted.`,
    );
  }

  protected setOverride(): void {
    this.confirmThen(
      {
        title: 'Approve aggregate-limit override?',
        message:
          'Total approved reserves on this claim will be allowed to exceed $10,000,000 (BR-R-05).',
        confirmLabel: 'Approve override',
      },
      () => this.store.setAggregateLimitOverride(),
      'Aggregate-limit override approved.',
    );
  }

  private confirmThen(
    data: { title: string; message: string; confirmLabel: string },
    action: () => Observable<void>,
    success: string,
  ): void {
    confirm(this.dialog, data)
      .pipe(
        filter(Boolean),
        switchMap(() => this.busy.track(action())),
      )
      .subscribe({ next: () => this.notifications.success(success), error: () => undefined });
  }

  /** Retry reports a 422 through the error interceptor's snackbar (the Blocking Condition message). */
  private run(action: Observable<void>, success: string): void {
    this.busy
      .track(action)
      .subscribe({ next: () => this.notifications.success(success), error: () => undefined });
  }

  /** Reject and Reverse: a reason dialog that runs the action itself and keeps any 422 on screen. */
  private askForReason(data: ReasonDialogData, success: string): void {
    this.dialog
      .open<ReasonDialog, ReasonDialogData, boolean>(ReasonDialog, { data, width: '480px' })
      .afterClosed()
      .pipe(filter((done) => done === true))
      .subscribe(() => this.notifications.success(success));
  }
}

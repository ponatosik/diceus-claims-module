import { CdkCopyToClipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, filter, of } from 'rxjs';
import { ClaimDetail } from '../../api/models';
import { ReferenceData } from '../../api/reference-data';
import { MockAuthService } from '../../core/auth/mock-auth.service';
import { NotificationService } from '../../core/errors/notification.service';
import { ClaimStatus, claimStatusLabel } from '../../shared/domain/claim-status';
import { CalendarDayPipe } from '../../shared/ui/calendar-day.pipe';
import { StatusBadge } from '../../shared/ui/status-badge';
import { ClaimDetailStore } from './claim-detail.store';
import { availableTransitions } from './claim-transitions';
import { TransitionDialog, TransitionDialogData } from './transition-dialog';

/** §11.3's always-visible header: identity, status, key facts, and the status transition menu. */
@Component({
  selector: 'app-claim-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CdkCopyToClipboard,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    CalendarDayPipe,
    StatusBadge,
  ],
  templateUrl: './claim-header.html',
  styleUrl: './claim-header.scss',
})
export class ClaimHeader {
  private readonly store = inject(ClaimDetailStore);
  private readonly auth = inject(MockAuthService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly claim = this.store.claim;
  protected readonly statusLabel = claimStatusLabel;

  private readonly statusTable = toSignal(
    inject(ReferenceData)
      .claimStatuses()
      .pipe(catchError(() => of([]))),
    {
      initialValue: [],
    },
  );

  protected readonly transitions = computed(() => {
    const claim = this.claim();
    return claim === undefined
      ? []
      : availableTransitions(this.statusTable(), claim.status, this.auth.currentUser().role);
  });

  protected openTransition(claim: ClaimDetail, target: ClaimStatus): void {
    const data: TransitionDialogData = {
      claimNumber: claim.claimNumber,
      from: claim.status,
      target,
      preflight: this.store.preflight(target),
      submit: (request) => this.store.transition(request),
    };
    this.dialog
      .open<TransitionDialog, TransitionDialogData, boolean>(TransitionDialog, {
        data,
        width: '560px',
      })
      .afterClosed()
      .pipe(filter((changed) => changed === true))
      .subscribe(() =>
        this.notifications.success(`${claim.claimNumber} moved to ${claimStatusLabel(target)}.`),
      );
  }

  protected copied(): void {
    this.notifications.success('Claim number copied.');
  }
}

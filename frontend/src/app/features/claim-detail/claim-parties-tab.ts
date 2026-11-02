import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, switchMap } from 'rxjs';
import { ClaimParty } from '../../api/models';
import { isApiError } from '../../core/errors/api-error';
import { NotificationService } from '../../core/errors/notification.service';
import { partyDisplayName, partyRoleLabel } from '../../shared/domain/labels';
import { applyServerErrors } from '../../shared/forms/apply-server-errors';
import { PartyForm, createPartyForm, toPartyRequest } from '../../shared/forms/party-form';
import { confirm } from '../../shared/ui/confirm-dialog';
import { PartyFormRow } from '../../shared/ui/party-form-row';
import { PendingTracker } from '../../shared/ui/pending-tracker';
import { ClaimDetailStore } from './claim-detail.store';

/** §11.3 Tab 2 — Parties: list, inline "Add Party", soft-remove (never the last active Claimant). */
@Component({
  selector: 'app-claim-parties-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, PartyFormRow],
  templateUrl: './claim-parties-tab.html',
  styleUrl: './claim-parties-tab.scss',
})
export class ClaimPartiesTab {
  private readonly store = inject(ClaimDetailStore);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  protected readonly roleLabel = partyRoleLabel;
  protected readonly partyName = partyDisplayName;
  protected readonly saving = new PendingTracker();

  protected readonly parties = computed(() => this.store.claim()?.parties ?? []);
  private readonly activeClaimants = computed(
    () => this.parties().filter((party) => party.isActive && party.partyRole === 'Claimant').length,
  );

  protected readonly draft = signal<PartyForm | null>(null);

  /** The backend refuses to remove the last active Claimant (422); the UI doesn't offer it. */
  protected isLastActiveClaimant(party: ClaimParty): boolean {
    return party.isActive && party.partyRole === 'Claimant' && this.activeClaimants() === 1;
  }

  protected openDraft(): void {
    const draft = createPartyForm();
    draft.controls.partyRole.setValue('ThirdParty');
    this.draft.set(draft);
  }

  protected add(draft: PartyForm): void {
    this.saving.track(this.store.addParty(toPartyRequest(draft.getRawValue()))).subscribe({
      next: () => {
        this.notifications.success(`${partyDisplayName(draft.getRawValue())} added.`);
        this.draft.set(null);
      },
      error: (error: unknown) => {
        if (isApiError(error)) applyServerErrors(draft, error.errors);
      },
    });
  }

  protected remove(party: ClaimParty): void {
    const name = partyDisplayName(party);
    confirm(this.dialog, {
      title: 'Remove party?',
      message: `${name} will be marked inactive on this claim. The audit log keeps the record.`,
      confirmLabel: 'Remove',
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.saving.track(this.store.removeParty(party.claimPartyId))),
      )
      .subscribe({
        next: () => this.notifications.success(`${name} removed.`),
        error: () => undefined, // announced by the error interceptor
      });
  }
}

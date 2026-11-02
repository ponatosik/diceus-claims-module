import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ASSET_TYPES, partyDisplayName, partyRoleLabel } from '../../shared/domain/labels';
import {
  PartyForm,
  RiskObjectForm,
  addParty,
  addRiskObject,
  createPartyForm,
  createRiskObjectForm,
} from './fnol-form';
import { PartyFormRow } from '../../shared/ui/party-form-row';
import { FnolIntakeStore } from './fnol-intake.store';

/** §11.2 Step 2 — Parties & Risk Objects: inline add rows, added items as cards with a remove button. */
@Component({
  selector: 'app-fnol-parties-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    PartyFormRow,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './fnol-parties-step.html',
  styleUrls: ['./fnol-step.scss', './fnol-parties-step.scss'],
})
export class FnolPartiesStep {
  private readonly store = inject(FnolIntakeStore);

  protected readonly assetTypes = ASSET_TYPES;
  protected readonly roleLabel = partyRoleLabel;
  protected readonly partyName = partyDisplayName;

  protected readonly partiesArray = this.store.form.controls.parties.controls.parties;
  protected readonly riskObjectsArray = this.store.form.controls.parties.controls.riskObjects;

  protected readonly parties = computed(() => this.store.value().parties.parties);
  protected readonly riskObjects = computed(() => this.store.value().parties.riskObjects);
  protected readonly claimantMissing = computed(
    () => !this.parties().some((party) => party.partyRole === 'Claimant'),
  );
  protected readonly partiesServerError = computed(() => {
    this.store.value();
    return this.partiesArray.getError('server') as string | null;
  });

  /** The open inline "Add …" rows, or null when closed. */
  protected readonly partyDraft = signal<PartyForm | null>(null);
  protected readonly riskObjectDraft = signal<RiskObjectForm | null>(null);

  protected openPartyDraft(): void {
    const draft = createPartyForm();
    // Default the first party to Claimant (§11.2 needs one); later ones to Third party.
    draft.controls.partyRole.setValue(this.claimantMissing() ? 'Claimant' : 'ThirdParty');
    this.partyDraft.set(draft);
  }

  protected addParty(draft: PartyForm): void {
    addParty(this.store.form, draft.getRawValue());
    this.partyDraft.set(null);
  }

  protected removeParty(index: number): void {
    this.partiesArray.removeAt(index);
  }

  protected openRiskObjectDraft(): void {
    this.riskObjectDraft.set(createRiskObjectForm());
  }

  protected addRiskObject(draft: RiskObjectForm): void {
    if (draft.invalid) {
      draft.markAllAsTouched();
      return;
    }
    addRiskObject(this.store.form, draft.getRawValue());
    this.riskObjectDraft.set(null);
  }

  protected removeRiskObject(index: number): void {
    this.riskObjectsArray.removeAt(index);
  }
}

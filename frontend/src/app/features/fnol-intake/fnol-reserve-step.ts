import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { toIsoDate } from '../../shared/domain/iso-date';
import {
  RESERVE_COMPONENTS,
  partyDisplayName,
  partyRoleLabel,
  reserveComponentLabel,
} from '../../shared/domain/labels';
import { AuthorityIndicator } from '../../shared/ui/authority-indicator';
import { IsoDayPipe } from '../../shared/ui/iso-day.pipe';
import { FnolIntakeStore } from './fnol-intake.store';

/** §11.2 Step 3 — optional initial reserve with the real-time authority indicator, and the review summary. */
@Component({
  selector: 'app-fnol-reserve-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    IsoDayPipe,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    AuthorityIndicator,
  ],
  templateUrl: './fnol-reserve-step.html',
  styleUrls: ['./fnol-step.scss', './fnol-reserve-step.scss'],
})
export class FnolReserveStep {
  private readonly store = inject(FnolIntakeStore);

  protected readonly group = this.store.form.controls.reserve;
  protected readonly components = RESERVE_COMPONENTS;
  protected readonly componentLabel = reserveComponentLabel;
  protected readonly roleLabel = partyRoleLabel;
  protected readonly partyName = partyDisplayName;

  protected readonly value = this.store.value;
  protected readonly lossMoment = this.store.lossMoment;
  /** The picked calendar day, for a loss entered without a time (shown as entered, not as a UTC moment). */
  protected readonly lossDay = computed(() => {
    const lossDate = this.value().loss.lossDate;
    return lossDate === null ? null : toIsoDate(lossDate);
  });
  protected readonly amount = computed(() => this.value().reserve.initialReserve.amount);
  protected readonly causeName = computed(() => {
    const cause = this.value().loss.causeOfLossCode;
    return typeof cause === 'object' && cause !== null ? cause.name : '—';
  });
  protected readonly reserveServerError = computed(() => {
    this.value();
    return this.group.controls.initialReserve.getError('server') as string | null;
  });
}

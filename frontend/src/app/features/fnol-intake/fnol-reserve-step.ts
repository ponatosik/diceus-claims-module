import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ReserveComponentType } from '../../api/models';
import { AuthorityIndicator } from '../../shared/ui/authority-indicator';
import { lossMoment } from './fnol-form';
import { FnolIntakeStore } from './fnol-intake.store';

/** §11.2 Step 3 — optional initial reserve with the real-time authority indicator, and the review summary. */
@Component({
  selector: 'app-fnol-reserve-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
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
  protected readonly components: ReserveComponentType[] = [
    'Indemnity',
    'Expense',
    'ALAE',
    'SubrogationRecoverable',
  ];

  protected readonly value = this.store.value;
  protected readonly amount = computed(() => this.value().reserve.initialReserve.amount);
  protected readonly lossMoment = computed(() => {
    const { lossDate, lossTime } = this.value().loss;
    return lossDate === null ? null : lossMoment(lossDate, lossTime);
  });
  protected readonly causeName = computed(() => {
    const cause = this.value().loss.causeOfLossCode;
    return typeof cause === 'object' && cause !== null ? cause.name : '—';
  });
  protected readonly reserveServerError = computed(() => {
    this.value();
    return this.group.controls.initialReserve.getError('server') as string | null;
  });

  protected componentLabel(component: ReserveComponentType): string {
    return component === 'SubrogationRecoverable' ? 'Subrogation recoverable' : component;
  }
}

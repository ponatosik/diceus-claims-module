import { CurrencyPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { map, startWith } from 'rxjs';
import { BlockingCondition, ReserveComponentType } from '../../api/models';
import { isApiError } from '../../core/errors/api-error';
import { NotificationService } from '../../core/errors/notification.service';
import { RESERVE_COMPONENTS, reserveComponentLabel } from '../../shared/domain/labels';
import { applyServerErrors } from '../../shared/forms/apply-server-errors';
import { AuthorityIndicator } from '../../shared/ui/authority-indicator';
import { PendingTracker } from '../../shared/ui/pending-tracker';
import { amountProblem, plannedChange } from './reserve-rules';
import { ReservesStore } from './reserves.store';

/**
 * The slide-in "Add Reserve" panel (§11.3): component, amount, reason, and the real-time authority
 * indicator. It picks the transaction type itself — Add for a new component, Adjust (signed delta,
 * with a new-balance preview) for an existing one (spec 011 story 3, ADR-0011).
 */
@Component({
  selector: 'app-reserve-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    AuthorityIndicator,
  ],
  templateUrl: './reserve-panel.html',
  styleUrl: './reserve-panel.scss',
})
export class ReservePanel {
  private readonly store = inject(ReservesStore);
  private readonly notifications = inject(NotificationService);

  readonly closed = output<void>();

  protected readonly componentOptions = RESERVE_COMPONENTS;
  protected readonly componentLabel = reserveComponentLabel;
  protected readonly saving = new PendingTracker();
  protected readonly rejection = signal<BlockingCondition[]>([]);

  protected readonly form = new FormGroup({
    component: new FormControl<ReserveComponentType>('Indemnity', { nonNullable: true }),
    amount: new FormControl<number | null>(null, Validators.required),
    changeReason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(500)],
    }),
  });

  private readonly value = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    { requireSync: true },
  );

  protected readonly plan = computed(() => {
    const { component, amount } = this.value();
    return plannedChange(this.store.components(), component, amount ?? 0);
  });
  protected readonly amount = computed(() => this.value().amount);

  protected readonly amountProblem = computed(() =>
    amountProblem(this.store.components(), this.value().component, this.value().amount),
  );

  protected readonly canSubmit = computed(() => {
    // Read the value signal first: form.valid is not a signal, so a computed that short-circuits on
    // it before touching value() would never re-run as the user types.
    const { amount } = this.value();
    return (
      amount !== 0 && this.amountProblem() === null && this.form.valid && !this.saving.pending()
    );
  });

  protected submit(): void {
    if (!this.canSubmit()) {
      this.form.markAllAsTouched();
      return;
    }
    const { component, amount, changeReason } = this.form.getRawValue();
    this.rejection.set([]);
    this.saving
      .track(
        this.store.submit({
          component,
          amount: amount!,
          changeReason: changeReason.trim(),
          transactionType: this.plan().transactionType,
        }),
      )
      .subscribe({
        next: () => {
          this.notifications.success(`${reserveComponentLabel(component)} reserve submitted.`);
          this.form.reset({ component, amount: null, changeReason: '' });
          this.closed.emit();
        },
        error: (error: unknown) => {
          if (!isApiError(error)) return;
          this.rejection.set(error.blockingConditions);
          applyServerErrors(this.form, error.errors);
        },
      });
  }
}

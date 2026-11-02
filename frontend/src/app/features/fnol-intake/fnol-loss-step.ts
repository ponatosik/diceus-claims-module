import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { CauseOfLossCode } from '../../api/models';
import { ReferenceApi } from '../../api/reference-api';
import { MIN_DESCRIPTION } from './fnol-form';
import { FnolIntakeStore } from './fnol-intake.store';
import { PolicySearch } from './policy-search';

/** §11.2 Step 1 — Policy & Loss Details. */
@Component({
  selector: 'app-fnol-loss-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatTimepickerModule,
    PolicySearch,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './fnol-loss-step.html',
  styleUrl: './fnol-step.scss',
})
export class FnolLossStep {
  private readonly store = inject(FnolIntakeStore);
  private readonly referenceApi = inject(ReferenceApi);

  protected readonly group = this.store.form.controls.loss;
  protected readonly minDescription = MIN_DESCRIPTION;
  protected readonly maxLossDate = new Date();

  protected readonly descriptionLength = computed(
    () => this.store.value().loss.lossDescription.trim().length,
  );

  private readonly causeOfLossCodes = rxResource({
    stream: () => this.referenceApi.causeOfLossCodes(),
  });

  /** The searchable cause-of-loss dropdown: options filtered by what's typed (§11.2). */
  protected readonly causeOptions = computed(() => {
    const typed = this.store.value().loss.causeOfLossCode;
    const codes = this.causeOfLossCodes.value() ?? [];
    if (typeof typed !== 'string' || typed.trim() === '') return codes;
    const query = typed.trim().toLowerCase();
    return codes.filter(
      (code) => code.name.toLowerCase().includes(query) || code.code.toLowerCase().includes(query),
    );
  });

  protected displayCause(value: CauseOfLossCode | string | null): string {
    return value === null ? '' : typeof value === 'string' ? value : value.name;
  }
}

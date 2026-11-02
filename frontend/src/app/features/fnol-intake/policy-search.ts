import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, switchMap } from 'rxjs';
import { PolicySearchResult } from '../../api/models';
import { PoliciesApi } from '../../api/policies-api';
import { IsoDayPipe } from '../../shared/ui/iso-day.pipe';
import { InForceBadge } from '../../shared/ui/in-force-badge';
import { FnolIntakeStore } from './fnol-intake.store';

const MIN_QUERY_LENGTH = 2;
const SEARCH_DEBOUNCE_MS = 300;

/**
 * §11.2 Step 1 policy lookup: typeahead over the simulated policies, the selected policy's details and
 * coverages (§10.4), the In-force badge, and the "Unknown Policy" toggle.
 */
@Component({
  selector: 'app-policy-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    IsoDayPipe,
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatCheckboxModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    InForceBadge,
  ],
  template: `
    <div class="policy-search">
      <mat-form-field class="search-field">
        <mat-label>Policy number or client name</mat-label>
        <input
          matInput
          [formControl]="searchText"
          [matAutocomplete]="policies"
          data-testid="policy-search"
        />
        <mat-icon matSuffix>search</mat-icon>
        <mat-autocomplete
          #policies="matAutocomplete"
          [displayWith]="displayPolicy"
          (optionSelected)="select($event.option.value)"
        >
          @for (policy of results(); track policy.policyId) {
            <mat-option [value]="policy">
              <strong>{{ policy.policyNumber }}</strong> — {{ policy.clientName }}
            </mat-option>
          }
        </mat-autocomplete>
      </mat-form-field>
      <mat-checkbox [formControl]="lossGroup.controls.unknownPolicy"
        >I can't find the policy (Unknown Policy)</mat-checkbox
      >
    </div>
    @if (lossGroup.hasError('policyRequired') && lossGroup.controls.policy.touched) {
      <mat-error class="policy-required" role="alert"
        >Select a policy, or tick Unknown Policy.</mat-error
      >
    }

    @if (policy(); as policy) {
      <div class="selected-policy" data-testid="selected-policy">
        <div>
          <div class="policy-number">{{ policy.policyNumber }}</div>
          <div>{{ policy.clientName }}</div>
          <div class="period">
            Effective {{ policy.effectiveDate | isoDay }} – {{ policy.expirationDate | isoDay }} ·
            {{ policy.status }}
          </div>
        </div>
        <app-in-force-badge [policy]="policy" [lossDate]="lossDate()" />
        @if (coverage.value(); as coverage) {
          <mat-chip-set aria-label="Coverage types">
            @for (type of coverage.coverageTypes; track type) {
              <mat-chip>{{ type }}</mat-chip>
            }
          </mat-chip-set>
        }
      </div>
    }
  `,
  styles: `
    .policy-search {
      display: flex;
      align-items: center;
      gap: 24px;
    }
    .policy-required {
      margin: -12px 0 16px;
    }
    .search-field {
      width: 420px;
    }
    .selected-policy {
      display: flex;
      align-items: center;
      gap: 24px;
      padding: 12px 16px;
      margin-bottom: 16px;
      border-radius: 8px;
      background: var(--mat-sys-surface-container);
    }
    .policy-number {
      font: var(--mat-sys-title-small);
    }
    .period {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class PolicySearch {
  private readonly store = inject(FnolIntakeStore);
  private readonly policiesApi = inject(PoliciesApi);

  protected readonly lossGroup = this.store.form.controls.loss;
  protected readonly searchText = new FormControl<string | PolicySearchResult>('', {
    nonNullable: true,
  });
  protected readonly policy = computed(() =>
    this.store.value().loss.unknownPolicy ? null : this.store.value().loss.policy,
  );
  protected readonly lossDate = this.store.lossMoment;

  protected readonly results = toSignal(
    this.searchText.valueChanges.pipe(
      filter((value): value is string => typeof value === 'string'),
      debounceTime(SEARCH_DEBOUNCE_MS),
      map((value) => value.trim()),
      distinctUntilChanged(),
      switchMap((query) =>
        query.length < MIN_QUERY_LENGTH
          ? of([])
          : this.policiesApi.search(query).pipe(catchError(() => of([] as PolicySearchResult[]))),
      ),
    ),
    { initialValue: [] as PolicySearchResult[] },
  );

  protected readonly coverage = rxResource({
    params: () => this.policy()?.policyId,
    stream: ({ params: policyId }) => this.policiesApi.coverage(policyId),
  });

  constructor() {
    const destroyRef = inject(DestroyRef);
    // Typing again un-selects the policy; the selection is only ever a picked option.
    this.searchText.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe((value) => {
      if (typeof value === 'string' && this.lossGroup.controls.policy.value !== null) {
        this.lossGroup.controls.policy.setValue(null);
      }
    });
    // Unknown Policy proceeds without a policy link (§11.2): clear and disable the search.
    this.lossGroup.controls.unknownPolicy.valueChanges
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe((unknown) => {
        if (unknown) {
          this.lossGroup.controls.policy.setValue(null);
          this.searchText.setValue('', { emitEvent: false });
          this.searchText.disable({ emitEvent: false });
        } else {
          this.searchText.enable({ emitEvent: false });
        }
      });
  }

  protected select(policy: PolicySearchResult): void {
    this.lossGroup.controls.policy.setValue(policy);
    this.lossGroup.controls.policy.markAsTouched();
  }

  protected displayPolicy(value: PolicySearchResult | string | null): string {
    return value === null
      ? ''
      : typeof value === 'string'
        ? value
        : `${value.policyNumber} — ${value.clientName}`;
  }
}

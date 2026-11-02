import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { ReferenceApi } from '../../api/reference-api';
import { CLAIM_STATUSES, ClaimStatus, claimStatusLabel } from '../../shared/domain/claim-status';
import { fromIsoDate, toIsoDate } from '../../shared/domain/iso-date';
import { Skeleton } from '../../shared/ui/skeleton';
import { StatusBadge } from '../../shared/ui/status-badge';
import { ClaimsListFilters, ClaimsListState, PAGE_SIZE_OPTIONS } from './claims-list-query';
import { ClaimsListStore } from './claims-list.store';

const FILTER_DEBOUNCE_MS = 300;

/** §11.1 Claims List (Dashboard): the application's entry point. */
@Component({
  selector: 'app-claims-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    Skeleton,
    StatusBadge,
  ],
  providers: [ClaimsListStore, provideNativeDateAdapter()],
  templateUrl: './claims-list-page.html',
  styleUrl: './claims-list-page.scss',
})
export class ClaimsListPage {
  protected readonly store = inject(ClaimsListStore);

  protected readonly columns = [
    'claimNumber',
    'policyNumber',
    'clientName',
    'lossDate',
    'causeOfLoss',
    'status',
    'totalReserves',
  ];
  protected readonly statuses = CLAIM_STATUSES;
  protected readonly statusLabel = claimStatusLabel;
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  private readonly referenceApi = inject(ReferenceApi);

  protected readonly causeOfLossCodes = rxResource({
    stream: () => this.referenceApi.causeOfLossCodes(),
  });

  protected readonly filters = new FormGroup({
    statuses: new FormControl<ClaimStatus[]>([], { nonNullable: true }),
    dateFrom: new FormControl<Date | null>(null),
    dateTo: new FormControl<Date | null>(null),
    assignedHandler: new FormControl('', { nonNullable: true }),
    causeOfLossCode: new FormControl('', { nonNullable: true }),
    search: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    // URL → form: keeps the filter bar in step with Back/Forward and bookmarked URLs.
    effect(() => this.filters.setValue(toFormValue(this.store.state()), { emitEvent: false }));

    // Form → URL, debounced so typing doesn't navigate on every keystroke (spec 008 story 5).
    this.filters.valueChanges
      .pipe(debounceTime(FILTER_DEBOUNCE_MS), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => {
        const filters = toFilters(this.filters.getRawValue());
        if (!sameFilters(filters, this.store.state())) {
          this.store.applyFilters(filters);
        }
      });
  }
}

type FilterFormValue = ReturnType<ClaimsListPage['filters']['getRawValue']>;

function toFormValue(state: ClaimsListState): FilterFormValue {
  return {
    statuses: state.statuses,
    dateFrom: state.dateFrom === null ? null : fromIsoDate(state.dateFrom),
    dateTo: state.dateTo === null ? null : fromIsoDate(state.dateTo),
    assignedHandler: state.assignedHandler ?? '',
    causeOfLossCode: state.causeOfLossCode ?? '',
    search: state.search ?? '',
  };
}

function toFilters(value: FilterFormValue): ClaimsListFilters {
  const text = (s: string) => (s.trim() === '' ? null : s.trim());
  return {
    statuses: value.statuses,
    dateFrom: value.dateFrom === null ? null : toIsoDate(value.dateFrom),
    dateTo: value.dateTo === null ? null : toIsoDate(value.dateTo),
    assignedHandler: text(value.assignedHandler),
    causeOfLossCode: text(value.causeOfLossCode),
    search: text(value.search),
  };
}

function sameFilters(filters: ClaimsListFilters, state: ClaimsListState): boolean {
  return (Object.keys(filters) as (keyof ClaimsListFilters)[]).every(
    (key) => JSON.stringify(filters[key]) === JSON.stringify(state[key]),
  );
}

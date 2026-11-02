import { Injectable, computed, inject } from '@angular/core';
import { latestValue } from '../../shared/state/latest-value';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import { ReloadRequests } from '../../core/errors/reload-requests';
import {
  ClaimsListFilters,
  ClaimsListState,
  DEFAULT_LIST_STATE,
  fromQueryParams,
  isFiltered,
  toApiQuery,
  toQueryParams,
  withFilters,
  withPage,
} from './claims-list-query';

/**
 * Dashboard state (§11.1). The URL is the single source of truth: the store derives its state from
 * the route's query params and changes it only by navigating, never by mutating local state
 * (spec 008 Implementation Decisions).
 */
@Injectable()
export class ClaimsListStore {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly claimsApi = inject(ClaimsApi);
  private readonly reloadRequests = inject(ReloadRequests);

  readonly state = toSignal(this.route.queryParamMap.pipe(map(fromQueryParams)), {
    initialValue: DEFAULT_LIST_STATE,
  });

  private readonly results = rxResource({
    params: () => ({ query: toApiQuery(this.state()), reload: this.reloadRequests.count() }),
    stream: ({ params }) => this.claimsApi.list(params.query),
  });

  /** The latest loaded page — kept on screen while the next one loads, so the table doesn't flash. */
  private readonly lastPage = latestValue(this.results);

  readonly claims = computed(() => this.lastPage()?.items ?? []);
  readonly totalCount = computed(() => this.lastPage()?.totalCount ?? 0);
  readonly loading = this.results.isLoading;
  readonly loaded = computed(() => this.lastPage() !== undefined);
  readonly failed = computed(() => this.results.status() === 'error');
  readonly hasActiveFilters = computed(() => isFiltered(this.state()));

  applyFilters(filters: Partial<ClaimsListFilters>): void {
    this.navigate(withFilters(this.state(), filters));
  }

  /** `pageIndex` is the paginator's 0-based index. */
  changePage(pageIndex: number, pageSize: number): void {
    this.navigate(withPage(this.state(), pageIndex + 1, pageSize));
  }

  clearFilters(): void {
    this.navigate({ ...DEFAULT_LIST_STATE, pageSize: this.state().pageSize });
  }

  retry(): void {
    this.results.reload();
  }

  open(claimId: string): void {
    void this.router.navigate(['/claims', claimId]);
  }

  private navigate(state: ClaimsListState): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: toQueryParams(state) });
  }
}

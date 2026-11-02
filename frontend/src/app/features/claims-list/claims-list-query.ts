import { ParamMap, Params } from '@angular/router';
import { ClaimListQuery } from '../../api/models';
import { CLAIM_STATUSES, ClaimStatus } from '../../shared/domain/claim-status';
import { fromIsoDate } from '../../shared/domain/iso-date';

/**
 * The dashboard's filter + paging state (§11.1). The URL query string is its single source of truth
 * (spec 008 story 4): filters are bookmarkable, survive reload, and Back restores them.
 */
export interface ClaimsListState {
  statuses: ClaimStatus[];
  /** Loss date range, `YYYY-MM-DD`. */
  dateFrom: string | null;
  dateTo: string | null;
  assignedHandler: string | null;
  causeOfLossCode: string | null;
  search: string | null;
  /** 1-based, as the API pages. */
  page: number;
  pageSize: number;
}

export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

export const DEFAULT_LIST_STATE: ClaimsListState = {
  statuses: [],
  dateFrom: null,
  dateTo: null,
  assignedHandler: null,
  causeOfLossCode: null,
  search: null,
  page: 1,
  pageSize: 20,
};

export type ClaimsListFilters = Omit<ClaimsListState, 'page' | 'pageSize'>;

/** Query-param names — what a user sees in the address bar and bookmarks. */
const QUERY_PARAM_KEYS = {
  status: 'status',
  dateFrom: 'dateFrom',
  dateTo: 'dateTo',
  handler: 'handler',
  cause: 'cause',
  search: 'search',
  page: 'page',
  pageSize: 'pageSize',
} as const;

export function fromQueryParams(params: ParamMap): ClaimsListState {
  const pageSize = Number(params.get(QUERY_PARAM_KEYS.pageSize));
  const page = Number(params.get(QUERY_PARAM_KEYS.page));
  return {
    statuses: params.getAll(QUERY_PARAM_KEYS.status).filter(isClaimStatus),
    dateFrom: isoDateOrNull(params.get(QUERY_PARAM_KEYS.dateFrom)),
    dateTo: isoDateOrNull(params.get(QUERY_PARAM_KEYS.dateTo)),
    assignedHandler: textOrNull(params.get(QUERY_PARAM_KEYS.handler)),
    causeOfLossCode: textOrNull(params.get(QUERY_PARAM_KEYS.cause)),
    search: textOrNull(params.get(QUERY_PARAM_KEYS.search)),
    page: Number.isInteger(page) && page >= 1 ? page : DEFAULT_LIST_STATE.page,
    pageSize: (PAGE_SIZE_OPTIONS as readonly number[]).includes(pageSize)
      ? pageSize
      : DEFAULT_LIST_STATE.pageSize,
  };
}

/** For `router.navigate(..., { queryParams })`: null removes a key, so defaults and empty filters stay out of the URL. */
export function toQueryParams(state: ClaimsListState): Params {
  return {
    [QUERY_PARAM_KEYS.status]: state.statuses.length > 0 ? state.statuses : null,
    [QUERY_PARAM_KEYS.dateFrom]: state.dateFrom,
    [QUERY_PARAM_KEYS.dateTo]: state.dateTo,
    [QUERY_PARAM_KEYS.handler]: state.assignedHandler,
    [QUERY_PARAM_KEYS.cause]: state.causeOfLossCode,
    [QUERY_PARAM_KEYS.search]: state.search,
    [QUERY_PARAM_KEYS.page]: state.page === DEFAULT_LIST_STATE.page ? null : state.page,
    [QUERY_PARAM_KEYS.pageSize]:
      state.pageSize === DEFAULT_LIST_STATE.pageSize ? null : state.pageSize,
  };
}

/** Whether any filter (not paging) differs from the defaults. */
export function isFiltered(state: ClaimsListState): boolean {
  return !sameFilters(state, DEFAULT_LIST_STATE);
}

/** Whether two filter sets are the same (paging ignored). */
export function sameFilters(a: ClaimsListFilters, b: ClaimsListFilters): boolean {
  return FILTER_KEYS.every((key) => JSON.stringify(a[key]) === JSON.stringify(b[key]));
}

const FILTER_KEYS: readonly (keyof ClaimsListFilters)[] = [
  'statuses',
  'dateFrom',
  'dateTo',
  'assignedHandler',
  'causeOfLossCode',
  'search',
];

/** Any filter change returns to page 1 (spec 008 story 5). */
export function withFilters(
  state: ClaimsListState,
  filters: Partial<ClaimsListFilters>,
): ClaimsListState {
  return { ...state, ...filters, page: 1 };
}

/** A page-size change returns to page 1; otherwise the requested page is kept. */
export function withPage(state: ClaimsListState, page: number, pageSize: number): ClaimsListState {
  return { ...state, page: pageSize === state.pageSize ? page : 1, pageSize };
}

export function toApiQuery(state: ClaimsListState): ClaimListQuery {
  return {
    statuses: state.statuses,
    dateFrom: state.dateFrom ?? undefined,
    dateTo: state.dateTo ?? undefined,
    assignedHandler: state.assignedHandler ?? undefined,
    causeOfLossCode: state.causeOfLossCode ?? undefined,
    search: state.search ?? undefined,
    page: state.page,
    pageSize: state.pageSize,
  };
}

function isClaimStatus(value: string): value is ClaimStatus {
  return (CLAIM_STATUSES as readonly string[]).includes(value);
}

function isoDateOrNull(value: string | null): string | null {
  return value !== null && fromIsoDate(value) !== null ? value : null;
}

/** Trimmed text, or null when blank — how free-text filters are normalised everywhere. */
export function textOrNull(value: string | null): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed === '' ? null : trimmed;
}

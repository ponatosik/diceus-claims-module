import { convertToParamMap } from '@angular/router';
import {
  DEFAULT_LIST_STATE,
  PAGE_SIZE_OPTIONS,
  fromQueryParams,
  isFiltered,
  toApiQuery,
  toQueryParams,
  withFilters,
  withPage,
} from './claims-list-query';

const params = (values: Record<string, string | string[]>) => convertToParamMap(values);

describe('claims list URL state', () => {
  it('defaults to page 1 of 20 with no filters', () => {
    expect(fromQueryParams(params({}))).toEqual(DEFAULT_LIST_STATE);
    expect(DEFAULT_LIST_STATE).toMatchObject({ page: 1, pageSize: 20, statuses: [] });
    expect(PAGE_SIZE_OPTIONS).toEqual([10, 20, 50, 100]);
  });

  it('reads repeated status params, the date range and text filters from the URL', () => {
    const state = fromQueryParams(
      params({
        status: ['Open', 'Closed'],
        dateFrom: '2026-01-01',
        dateTo: '2026-03-31',
        handler: 'morg',
        cause: 'COL-FIRE',
        search: 'CLM-2026',
        page: '3',
        pageSize: '50',
      }),
    );

    expect(state).toEqual({
      statuses: ['Open', 'Closed'],
      dateFrom: '2026-01-01',
      dateTo: '2026-03-31',
      assignedHandler: 'morg',
      causeOfLossCode: 'COL-FIRE',
      search: 'CLM-2026',
      page: 3,
      pageSize: 50,
    });
  });

  it('ignores hand-edited garbage instead of sending it to the API', () => {
    const state = fromQueryParams(
      params({ status: ['Open', 'Bogus'], dateFrom: 'yesterday', page: '-2', pageSize: '7' }),
    );

    expect(state.statuses).toEqual(['Open']);
    expect(state.dateFrom).toBeNull();
    expect(state.page).toBe(1);
    expect(state.pageSize).toBe(20);
  });

  it('round-trips through query params, omitting defaults and empty filters', () => {
    const state = {
      ...DEFAULT_LIST_STATE,
      statuses: ['Draft' as const],
      search: 'Meridian',
      page: 2,
    };

    const query = toQueryParams(state);

    expect(query).toEqual({
      status: ['Draft'],
      dateFrom: null,
      dateTo: null,
      handler: null,
      cause: null,
      search: 'Meridian',
      page: 2,
      pageSize: null,
    });
    const roundTripped = fromQueryParams(
      params(
        Object.fromEntries(Object.entries(query).filter(([, v]) => v !== null)) as Record<
          string,
          string | string[]
        >,
      ),
    );
    expect(roundTripped).toEqual(state);
  });

  it('resets to page 1 whenever a filter changes', () => {
    const onPage4 = { ...DEFAULT_LIST_STATE, page: 4 };

    expect(withFilters(onPage4, { statuses: ['Open'] })).toMatchObject({
      statuses: ['Open'],
      page: 1,
    });
    expect(withFilters(onPage4, { search: 'x' }).page).toBe(1);
  });

  it('resets to page 1 when the page size changes, keeps the page otherwise', () => {
    const onPage4 = { ...DEFAULT_LIST_STATE, page: 4 };

    expect(withPage(onPage4, 5, 20)).toMatchObject({ page: 5, pageSize: 20 });
    expect(withPage(onPage4, 4, 50)).toMatchObject({ page: 1, pageSize: 50 });
  });

  it('maps to the API query, translating names and dropping empty values', () => {
    const state = {
      ...DEFAULT_LIST_STATE,
      statuses: ['Open' as const, 'Reopened' as const],
      dateFrom: '2026-01-01',
      assignedHandler: 'priya',
      page: 2,
    };

    expect(toApiQuery(state)).toEqual({
      statuses: ['Open', 'Reopened'],
      dateFrom: '2026-01-01',
      dateTo: undefined,
      assignedHandler: 'priya',
      causeOfLossCode: undefined,
      search: undefined,
      page: 2,
      pageSize: 20,
    });
  });

  it('knows whether any filter is active — paging alone does not count', () => {
    expect(isFiltered(DEFAULT_LIST_STATE)).toBe(false);
    expect(isFiltered({ ...DEFAULT_LIST_STATE, page: 3, pageSize: 50 })).toBe(false);
    expect(isFiltered({ ...DEFAULT_LIST_STATE, statuses: ['Open'] })).toBe(true);
    expect(isFiltered({ ...DEFAULT_LIST_STATE, dateTo: '2026-01-01' })).toBe(true);
  });
});

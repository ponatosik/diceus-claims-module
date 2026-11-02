import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { render, screen } from '@testing-library/angular';
import userEvent from '@testing-library/user-event';
import { of } from 'rxjs';
import { ClaimsApi } from '../../api/claims-api';
import { ClaimListQuery, ClaimSummary, PagedResult } from '../../api/models';
import { ReferenceApi } from '../../api/reference-api';
import { ClaimsListPage } from './claims-list-page';

@Component({ template: '' })
class StubPage {}

const claim = (overrides: Partial<ClaimSummary> = {}): ClaimSummary => ({
  claimId: 'c-1',
  claimNumber: 'CLM-2026-0000001',
  clientName: 'Meridian Transport LLC',
  policyNumber: 'POL-2024-001001',
  lossDate: '2026-10-01T10:00:00+00:00',
  status: 'Open',
  totalReserves: 12500,
  causeOfLossCode: 'COL-FIRE',
  causeOfLossName: 'Fire',
  ...overrides,
});

async function renderPage(items: ClaimSummary[], totalCount = items.length) {
  const list = vi.fn((query: ClaimListQuery) =>
    of<PagedResult<ClaimSummary>>({
      items,
      totalCount,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    }),
  );
  const result = await render(ClaimsListPage, {
    providers: [
      provideRouter([
        { path: 'claims', component: StubPage },
        { path: 'claims/new', component: StubPage },
        { path: 'claims/:claimId', component: StubPage },
      ]),
      { provide: ClaimsApi, useValue: { list } },
      {
        provide: ReferenceApi,
        useValue: {
          causeOfLossCodes: () =>
            of([{ code: 'COL-FIRE', name: 'Fire', perilCategory: 'Property', sortOrder: 1 }]),
        },
      },
    ],
  });
  await result.fixture.whenStable();
  return { ...result, list, router: result.fixture.debugElement.injector.get(Router) };
}

describe('ClaimsListPage', () => {
  it('shows each claim with the §11.1 columns', async () => {
    await renderPage([claim()]);

    expect(screen.getByText('CLM-2026-0000001')).toBeTruthy();
    expect(screen.getByText('POL-2024-001001')).toBeTruthy();
    expect(screen.getByText('Meridian Transport LLC')).toBeTruthy();
    expect(screen.getByText('Fire')).toBeTruthy();
    expect(screen.getByText('$12,500.00')).toBeTruthy();
    expect(screen.getByText('Open')).toBeTruthy();
  });

  it('colours each status badge with its own status token', async () => {
    await renderPage([
      claim({ claimId: 'a', status: 'UnderInvestigation' }),
      claim({ claimId: 'b', status: 'Withdrawn', claimNumber: 'CLM-2026-0000002' }),
    ]);

    expect(screen.getByText('Under investigation').getAttribute('style')).toContain(
      'var(--status-under-investigation)',
    );
    expect(screen.getByText('Withdrawn').getAttribute('style')).toContain(
      'var(--status-withdrawn)',
    );
  });

  it('opens the claim when its row is clicked', async () => {
    const { router } = await renderPage([claim({ claimId: 'abc-123' })]);
    const navigate = vi.spyOn(router, 'navigate');

    await userEvent.click(screen.getByText('CLM-2026-0000001'));

    expect(navigate).toHaveBeenCalledWith(['/claims', 'abc-123']);
  });

  it('shows the total claim count', async () => {
    await renderPage([claim()], 57);

    expect(screen.getByText(/57 claims/)).toBeTruthy();
  });

  it('shows a friendly empty state with a way to clear active filters', async () => {
    const { router, fixture } = await renderPage([]);
    await router.navigate([], { queryParams: { status: 'Closed' } });
    await fixture.whenStable();

    expect(screen.getByText('No claims match these filters')).toBeTruthy();
    expect(screen.getAllByRole('button', { name: 'Clear filters' }).length).toBeGreaterThan(0);
  });

  it('suggests logging the first claim when there are no claims and no filters', async () => {
    await renderPage([]);

    expect(screen.getByText('Log the first claim to get started.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Clear filters' })).toBeNull();
  });

  it('asks the API for the filters and page in the URL', async () => {
    const { router, fixture, list } = await renderPage([claim()]);

    await router.navigate([], {
      queryParams: { status: ['Open', 'Reopened'], handler: 'morg', page: 2 },
    });
    await fixture.whenStable();

    expect(list).toHaveBeenLastCalledWith(
      expect.objectContaining({
        statuses: ['Open', 'Reopened'],
        assignedHandler: 'morg',
        page: 2,
        pageSize: 20,
      }),
    );
  });

  it('offers "Log New Claim" as the primary action, linking to the FNOL form', async () => {
    await renderPage([claim()]);

    expect(screen.getByRole('link', { name: /Log New Claim/ }).getAttribute('href')).toBe(
      '/claims/new',
    );
  });
});

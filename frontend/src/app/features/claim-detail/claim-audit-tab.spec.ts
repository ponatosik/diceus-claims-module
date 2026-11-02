import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { render, screen, within } from '@testing-library/angular';
import { AuditLogEntry } from '../../api/models';
import { AuditStore } from './audit.store';
import { ClaimAuditTab } from './claim-audit-tab';

const entry = (overrides: Partial<AuditLogEntry>): AuditLogEntry => ({
  auditLogId: 'a',
  createdAt: '2026-11-01T10:00:00Z',
  eventType: 'CLAIM_CREATED',
  description: 'Claim CLM-2026-0000001 created.',
  userId: 'u1',
  userDisplayName: 'Alex Morgan',
  relatedEntityType: null,
  relatedEntityId: null,
  ...overrides,
});

async function renderTab(entries: AuditLogEntry[]) {
  await render(ClaimAuditTab, {
    providers: [provideRouter([])],
    componentProviders: [
      {
        provide: AuditStore,
        useValue: {
          entries: signal(entries),
          totalCount: signal(entries.length),
          loaded: signal(true),
          loading: signal(false),
          page: signal(1),
          pageSize: signal(25),
          changePage: vi.fn(),
        },
      },
    ],
  });
}

describe('ClaimAuditTab (§11.3 Tab 5)', () => {
  it('shows each entry’s event type, description and user, in the order the API returns (newest first)', async () => {
    await renderTab([
      entry({
        auditLogId: '2',
        eventType: 'RESERVE_CREATED',
        description: 'Reserve transaction submitted.',
      }),
      entry({ auditLogId: '1' }),
    ]);

    const rows = screen.getAllByTestId(/^audit-/);
    expect(within(rows[0]).getByText('RESERVE_CREATED')).toBeTruthy();
    expect(within(rows[0]).getByText('Reserve transaction submitted.')).toBeTruthy();
    expect(within(rows[0]).getByText('Alex Morgan')).toBeTruthy();
    expect(within(rows[1]).getByText('CLAIM_CREATED')).toBeTruthy();
  });

  it('links an entry to its entity’s tab within the same claim, highlighting it', async () => {
    await renderTab([
      entry({
        auditLogId: 'r',
        eventType: 'RESERVE_APPROVED',
        relatedEntityType: 'ReserveHistory',
        relatedEntityId: 'txn-9',
      }),
      entry({ auditLogId: 'c' }),
    ]);

    const link = within(screen.getByTestId('audit-r')).getByRole('link', { name: /View reserve/ });
    expect(link.getAttribute('href')).toContain('tab=reserves');
    expect(link.getAttribute('href')).toContain('highlight=txn-9');
    expect(within(screen.getByTestId('audit-c')).queryByRole('link')).toBeNull();
  });

  it('offers no way to edit or delete entries (append-only, AUD rules)', async () => {
    await renderTab([entry({ auditLogId: '1' })]);

    expect(within(screen.getByTestId('audit-1')).queryByRole('button')).toBeNull();
    expect(screen.queryByRole('button', { name: /edit|delete|remove/i })).toBeNull();
  });
});

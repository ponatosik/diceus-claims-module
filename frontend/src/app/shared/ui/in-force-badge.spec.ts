import { render, screen } from '@testing-library/angular';
import { InForceBadge } from './in-force-badge';

const period = { effectiveDate: '2024-01-01', expirationDate: '2026-12-31' };

describe('InForceBadge (§11.2 in-force indicator)', () => {
  it('is green when an active policy covers the loss date', async () => {
    await render(InForceBadge, {
      inputs: {
        policy: { ...period, status: 'Active' },
        lossDate: new Date('2025-06-01T12:00:00Z'),
      },
    });

    const badge = screen.getByTestId('in-force-badge');
    expect(badge.textContent?.trim()).toBe('In force on loss date');
    expect(badge.classList).toContain('in-force');
  });

  it('is amber when the loss date is outside the effective period', async () => {
    await render(InForceBadge, {
      inputs: {
        policy: { ...period, status: 'Active' },
        lossDate: new Date('2027-02-01T12:00:00Z'),
      },
    });

    expect(screen.getByTestId('in-force-badge').textContent?.trim()).toBe(
      'Not in force on loss date',
    );
  });

  it('is amber when the policy is expired, even if its period covers the loss date (§11.2)', async () => {
    await render(InForceBadge, {
      inputs: {
        policy: { ...period, status: 'Expired' },
        lossDate: new Date('2025-06-01T12:00:00Z'),
      },
    });

    const badge = screen.getByTestId('in-force-badge');
    expect(badge.textContent?.trim()).toBe('Policy Expired');
    expect(badge.classList).not.toContain('in-force');
  });
});

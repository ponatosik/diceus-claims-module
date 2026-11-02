import { render, screen } from '@testing-library/angular';
import { AuthorityIndicator } from './authority-indicator';

describe('AuthorityIndicator (§11.2 / §11.3 real-time authority indicator)', () => {
  it.each([
    [10_000, '✓ Auto-approved (≤ $10,000)'],
    [10_000.01, '⚠ Supervisor approval required'],
    [100_000.01, '⚠ Manager approval required'],
  ])('shows the tier for %d', async (amount, text) => {
    await render(AuthorityIndicator, { inputs: { amount } });

    expect(screen.getByRole('status').textContent?.trim()).toBe(text);
  });

  it('marks tiers that need approval as a warning', async () => {
    await render(AuthorityIndicator, { inputs: { amount: 50_000 } });

    expect(screen.getByRole('status').classList).toContain('needs-approval');
  });

  it('shows nothing until there is a non-zero amount', async () => {
    await render(AuthorityIndicator, { inputs: { amount: null } });

    expect(screen.queryByRole('status')).toBeNull();
  });
});

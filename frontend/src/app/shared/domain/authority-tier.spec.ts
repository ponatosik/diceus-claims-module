import { authorityTier, authorityTierLabel } from './authority-tier';

// Boundaries from REQUIREMENTS.md §6.3, mirroring backend ReserveAuthority.GetTier (absolute value).
describe('authorityTier', () => {
  it.each([
    [0.01, 'auto'],
    [10_000, 'auto'],
    [10_000.01, 'supervisor'],
    [100_000, 'supervisor'],
    [100_000.01, 'manager'],
    [10_000_000, 'manager'],
    [-10_000, 'auto'],
    [-10_000.01, 'supervisor'],
    [-250_000, 'manager'],
  ] as const)('%d → %s', (amount, tier) => {
    expect(authorityTier(amount)).toBe(tier);
  });

  it('labels each tier with the §11.2 indicator text', () => {
    expect(authorityTierLabel('auto')).toBe('✓ Auto-approved (≤ $10,000)');
    expect(authorityTierLabel('supervisor')).toBe('⚠ Supervisor approval required');
    expect(authorityTierLabel('manager')).toBe('⚠ Manager approval required');
  });
});

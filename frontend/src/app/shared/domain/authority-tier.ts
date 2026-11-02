/** GLOSSARY.md "Authority Tier" — the approval level a reserve transaction needs, from its amount alone. */
export type AuthorityTier = 'auto' | 'supervisor' | 'manager';

const AUTO_APPROVAL_LIMIT = 10_000;
const SUPERVISOR_APPROVAL_LIMIT = 100_000;

/** §6.3 thresholds on `|amount|` — identical to the backend's ReserveAuthority.GetTier, so the indicator and the API never disagree. */
export function authorityTier(amount: number): AuthorityTier {
  const absoluteAmount = Math.abs(amount);
  if (absoluteAmount <= AUTO_APPROVAL_LIMIT) return 'auto';
  return absoluteAmount <= SUPERVISOR_APPROVAL_LIMIT ? 'supervisor' : 'manager';
}

const labels: Record<AuthorityTier, string> = {
  auto: '✓ Auto-approved (≤ $10,000)',
  supervisor: '⚠ Supervisor approval required',
  manager: '⚠ Manager approval required',
};

/** §11.2's real-time authority indicator text. */
export function authorityTierLabel(tier: AuthorityTier): string {
  return labels[tier];
}

import { AuthorityTier } from '../../shared/domain/authority-tier';
import { Role } from './demo-users';

// The frontend half of FR-ROLE-01's two-place role enforcement. Each rule mirrors a backend check so
// the UI gates exactly what the API enforces — the API stays the authority either way.

/** §3/FR-ROLE-01: Approve/Reject buttons on reserves. */
export function canApproveReserves(role: Role): boolean {
  return role === 'supervisor' || role === 'manager';
}

/** Backend ReserveAuthority.CanApprove — a Manager satisfies every tier. */
export function canApproveTier(role: Role, tier: AuthorityTier): boolean {
  if (role === 'manager') return true;
  if (tier === 'auto') return true;
  return tier === 'supervisor' && role === 'supervisor';
}

/** BR-ST-04: Closed → Reopened. */
export function canReopen(role: Role): boolean {
  return role === 'supervisor' || role === 'manager';
}

/** BR-R-05: the Aggregate Limit Override (GLOSSARY.md). */
export function canSetAggregateLimitOverride(role: Role): boolean {
  return role === 'manager';
}

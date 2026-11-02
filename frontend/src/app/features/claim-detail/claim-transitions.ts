import { ClaimStatusReference } from '../../api/models';
import { Role } from '../../core/auth/demo-users';
import { canReopen } from '../../core/auth/permissions';
import { ClaimStatus } from '../../shared/domain/claim-status';

/**
 * The statuses the header's transition menu offers (§11.3, spec 010 stories 2–3): the §4.2 reference
 * table's valid next statuses, minus Reopen for roles that can't (BR-ST-04 — the only role-gated
 * transition), and minus Reopened → Open, which the API performs automatically on reopen.
 */
export function availableTransitions(
  reference: readonly ClaimStatusReference[],
  current: ClaimStatus,
  role: Role,
): ClaimStatus[] {
  if (current === 'Reopened') return [];
  const next = reference.find((entry) => entry.status === current)?.validNextStatuses ?? [];
  return next.filter((status) => status !== 'Reopened' || canReopen(role));
}

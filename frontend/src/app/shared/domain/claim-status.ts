/** §4.1's claim statuses, as the API serializes them (JsonStringEnumConverter). */
export const CLAIM_STATUSES = [
  'Draft',
  'Open',
  'UnderInvestigation',
  'PendingPayment',
  'Closed',
  'Reopened',
  'Withdrawn',
] as const;

export type ClaimStatus = (typeof CLAIM_STATUSES)[number];

const labels: Record<ClaimStatus, string> = {
  Draft: 'Draft',
  Open: 'Open',
  UnderInvestigation: 'Under investigation',
  PendingPayment: 'Pending payment',
  Closed: 'Closed',
  Reopened: 'Reopened',
  Withdrawn: 'Withdrawn',
};

export function claimStatusLabel(status: ClaimStatus): string {
  return labels[status];
}

/**
 * The CSS custom property holding a status's §11.1 badge colour (defined once in styles.scss), e.g.
 * `--status-under-investigation`. One token per status keeps the dashboard and detail header identical.
 */
export function claimStatusToken(status: ClaimStatus): string {
  return `--status-${status.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()}`;
}

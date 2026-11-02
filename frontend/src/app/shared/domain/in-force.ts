/** The policy fields the In-force check needs (GLOSSARY.md "In-force"); dates are `YYYY-MM-DD` (backend DateOnly). */
export interface PolicyPeriod {
  effectiveDate: string;
  expirationDate: string;
}

/**
 * A policy is in force for a loss when the loss date falls within its effective period, inclusive —
 * using the loss date's UTC calendar day, exactly like the backend's BR-C-02 warning, so the FNOL
 * badge and the server-side validation agree.
 */
export function isInForce(policy: PolicyPeriod, lossDate: string | Date): boolean {
  const lossDay = new Date(lossDate).toISOString().slice(0, 10);
  return lossDay >= policy.effectiveDate && lossDay <= policy.expirationDate;
}

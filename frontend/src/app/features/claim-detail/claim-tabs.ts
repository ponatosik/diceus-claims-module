/** Claim Detail tab keys, deep-linkable as `?tab=` (spec 010; Slice L's audit links target `?tab=…&highlight=<id>`). */
export const CLAIM_TABS = ['overview', 'parties', 'reserves', 'documents', 'audit'] as const;
export type ClaimTab = (typeof CLAIM_TABS)[number];

/** A `?tab=` value as a known tab; anything else (missing, hand-edited) means Overview. */
export function parseClaimTab(value: string | undefined): ClaimTab {
  return (CLAIM_TABS as readonly string[]).includes(value ?? '') ? (value as ClaimTab) : 'overview';
}

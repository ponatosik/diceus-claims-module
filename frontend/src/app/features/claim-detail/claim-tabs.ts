/** Claim Detail tab keys, deep-linkable as `?tab=` (spec 010; Slice L's audit links target `?tab=…&highlight=<id>`). */
export const CLAIM_TABS = ['overview', 'parties', 'reserves', 'documents', 'audit'] as const;
export type ClaimTab = (typeof CLAIM_TABS)[number];

/** A `?tab=` value as a known tab; anything else (missing, hand-edited) means Overview. */
export function parseClaimTab(value: string | undefined): ClaimTab {
  return (CLAIM_TABS as readonly string[]).includes(value ?? '') ? (value as ClaimTab) : 'overview';
}

/**
 * Query params for a tab-group selection, or null when it names the tab the URL already has. The tab
 * group also reports *programmatic* switches (e.g. arriving via an audit link with `?highlight=`), and
 * rewriting the URL then would throw that highlight away.
 */
export function tabChangeQueryParams(
  currentTabParam: string | undefined,
  selectedIndex: number,
): { tab: ClaimTab | null; highlight: null } | null {
  const selected = CLAIM_TABS[selectedIndex];
  if (selected === parseClaimTab(currentTabParam)) return null;
  return { tab: selected === 'overview' ? null : selected, highlight: null };
}

import { CLAIM_TABS, parseClaimTab } from './claim-tabs';

describe('parseClaimTab', () => {
  it('accepts each known tab key', () => {
    for (const tab of CLAIM_TABS) expect(parseClaimTab(tab)).toBe(tab);
  });

  it('falls back to Overview for a missing or unknown ?tab=', () => {
    expect(parseClaimTab(undefined)).toBe('overview');
    expect(parseClaimTab('bogus')).toBe('overview');
  });
});

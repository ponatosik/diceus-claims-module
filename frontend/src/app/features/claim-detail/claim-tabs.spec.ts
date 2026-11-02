import { CLAIM_TABS, parseClaimTab, tabChangeQueryParams } from './claim-tabs';

describe('parseClaimTab', () => {
  it('accepts each known tab key', () => {
    for (const tab of CLAIM_TABS) expect(parseClaimTab(tab)).toBe(tab);
  });

  it('falls back to Overview for a missing or unknown ?tab=', () => {
    expect(parseClaimTab(undefined)).toBe('overview');
    expect(parseClaimTab('bogus')).toBe('overview');
  });
});

describe('tabChangeQueryParams', () => {
  it('leaves the URL alone when the tab group reports the tab the URL already names (e.g. after an audit link)', () => {
    expect(tabChangeQueryParams('documents', CLAIM_TABS.indexOf('documents'))).toBeNull();
    expect(tabChangeQueryParams(undefined, 0)).toBeNull();
  });

  it('switching tab by hand writes the new tab and drops any highlight', () => {
    expect(tabChangeQueryParams('documents', CLAIM_TABS.indexOf('reserves'))).toEqual({
      tab: 'reserves',
      highlight: null,
    });
  });

  it('keeps Overview out of the URL', () => {
    expect(tabChangeQueryParams('audit', 0)).toEqual({ tab: null, highlight: null });
  });
});

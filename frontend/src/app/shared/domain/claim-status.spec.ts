import { CLAIM_STATUSES, claimStatusLabel, claimStatusToken } from './claim-status';

describe('claim status presentation', () => {
  it('knows all seven §4.1 statuses', () => {
    expect(CLAIM_STATUSES).toEqual([
      'Draft',
      'Open',
      'UnderInvestigation',
      'PendingPayment',
      'Closed',
      'Reopened',
      'Withdrawn',
    ]);
  });

  it('labels multi-word statuses readably', () => {
    expect(claimStatusLabel('UnderInvestigation')).toBe('Under investigation');
    expect(claimStatusLabel('PendingPayment')).toBe('Pending payment');
    expect(claimStatusLabel('Open')).toBe('Open');
  });

  it('maps every status to its own §11.1 colour token', () => {
    expect(claimStatusToken('Draft')).toBe('--status-draft');
    expect(claimStatusToken('UnderInvestigation')).toBe('--status-under-investigation');
    expect(new Set(CLAIM_STATUSES.map(claimStatusToken)).size).toBe(7);
  });
});

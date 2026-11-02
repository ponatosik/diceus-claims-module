import {
  canApproveReserves,
  canApproveTier,
  canReopen,
  canSetAggregateLimitOverride,
} from './permissions';

describe('role permissions (mirroring the backend checks)', () => {
  it('only supervisors and managers approve or reject reserves (FR-ROLE-01)', () => {
    expect(canApproveReserves('handler')).toBe(false);
    expect(canApproveReserves('supervisor')).toBe(true);
    expect(canApproveReserves('manager')).toBe(true);
  });

  it('matches ReserveAuthority.CanApprove: supervisors up to the supervisor tier, managers every tier', () => {
    expect(canApproveTier('supervisor', 'supervisor')).toBe(true);
    expect(canApproveTier('supervisor', 'manager')).toBe(false);
    expect(canApproveTier('manager', 'manager')).toBe(true);
    expect(canApproveTier('handler', 'supervisor')).toBe(false);
  });

  it('only supervisors and managers reopen a claim (BR-ST-04)', () => {
    expect(canReopen('handler')).toBe(false);
    expect(canReopen('supervisor')).toBe(true);
    expect(canReopen('manager')).toBe(true);
  });

  it('only managers set the Aggregate Limit Override (BR-R-05)', () => {
    expect(canSetAggregateLimitOverride('supervisor')).toBe(false);
    expect(canSetAggregateLimitOverride('manager')).toBe(true);
  });
});
